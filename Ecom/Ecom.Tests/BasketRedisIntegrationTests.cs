using System.Linq.Expressions;
using Ecom.Core.Entity;
using Ecom.Core.Entity.Product;
using Ecom.Core.Interfaces;
using Ecom.Infrastructure.Services;
using Moq;
using StackExchange.Redis;

namespace Ecom.Tests;

/// <summary>
/// Real-Redis integration tests for basket merge/idempotency and basic concurrency.
/// Uses isolated, test-owned keys only and cleans them up. Skips if Redis is unavailable.
/// Does NOT simulate Redis outages against shared infrastructure.
/// </summary>
public class BasketRedisIntegrationTests
{
    private const string RedisConnection = "localhost:6379";

    private static Product MakeProduct(int id, decimal price, string name)
    {
        return new Product
        {
            Id = id,
            Name = name,
            Description = "desc",
            NewPrice = price,
            OldPrice = price,
            CategoryId = 1,
            Category = new Category { Name = "Cat", Description = "Cat" },
            Photos = new List<Photo> { new Photo { Name = $"img{id}.jpg", ProductId = id } }
        };
    }

    private static async Task<(IConnectionMultiplexer mux, IDatabase db, BasketService svc)?> TryCreateAsync()
    {
        try
        {
            var mux = await ConnectionMultiplexer.ConnectAsync(new ConfigurationOptions
            {
                EndPoints = { RedisConnection },
                AbortOnConnectFail = false,
                ConnectTimeout = 1500,
                SyncTimeout = 1500
            });
            if (!mux.IsConnected) { mux.Dispose(); return null; }
            var db = mux.GetDatabase();

            var products = new Dictionary<int, Product>
            {
                [1] = MakeProduct(1, 10.00m, "P1"),
                [2] = MakeProduct(2, 20.00m, "P2"),
            };

            var prodRepo = new Mock<IProductRepository>();
            prodRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<Expression<Func<Product, object>>[]>()))
                .ReturnsAsync((int id, Expression<Func<Product, object>>[] _) => products.TryGetValue(id, out var p) ? p : null);
            prodRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
                .ReturnsAsync((int id) => products.TryGetValue(id, out var p) ? p : null);
            var uow = new Mock<IUnitOfWork>();
            uow.SetupGet(x => x.ProductRepository).Returns(prodRepo.Object);

            var svc = new BasketService(mux, uow.Object);
            return (mux, db, svc);
        }
        catch
        {
            return null;
        }
    }

    [Fact]
    public async Task Merge_IsIdempotent_AndConcurrentMergeAttempts_DoNotDuplicate()
    {
        var env = await TryCreateAsync();
        if (env is null) return; // Redis unavailable in this environment

        var (mux, db, svc) = env.Value;
        var runId = Guid.NewGuid().ToString("N");
        var userId = $"qa-merge-user-{runId}";
        var guestCred = $"qa-merge-guest-{runId}-0123456789abcdef0123456789abcdef";
        var guestCtx = new BasketOwnerContext(null, guestCred, false);

        try
        {
            // Prepare guest basket (isolated test key)
            var add = await svc.AddItemAsync(guestCtx, 1, 2);
            Assert.True(add.Success);
            Assert.NotNull(add.Basket);
            Assert.Single(add.Basket!.BasketItems); // add returns saved basket
            Assert.Null(add.NewGuestCredential); // using provided valid credential
            var guestKey = $"basket:guest:{ComputeSha256(guestCred)}";
            Assert.True(await db.KeyExistsAsync(guestKey));
            var raw = await db.StringGetAsync(guestKey);
            Assert.False(raw.IsNullOrEmpty);
            var guestBeforeMerge = await svc.GetBasketAsync(guestCtx);
            Assert.True(guestBeforeMerge.Success);
            Assert.NotNull(guestBeforeMerge.Basket);
            Assert.True(guestBeforeMerge.Basket!.BasketItems.Count == 1, $"raw={raw.ToString()} dtoCount={guestBeforeMerge.Basket.BasketItems.Count} addCount={add.Basket.BasketItems.Count}");

            // Initial merge must succeed
            var firstMerge = await svc.MergeGuestBasketAsync(userId, guestCred);
            Assert.True(firstMerge.Success);
            Assert.NotNull(firstMerge.Basket);
            Assert.Single(firstMerge.Basket!.BasketItems);

            // Repeated merge calls (including concurrent) must be idempotent and not duplicate
            var tasks = Enumerable.Range(0, 8).Select(_ => svc.MergeGuestBasketAsync(userId, guestCred));
            var results = await Task.WhenAll(tasks);
            Assert.All(results, r => Assert.True(r.Success, r.Message ?? $"status={r.StatusCode}"));

            var userBasket = await svc.GetBasketAsync(new BasketOwnerContext(userId, null, true));
            Assert.True(userBasket.Success);
            Assert.NotNull(userBasket.Basket);
            var item = Assert.Single(userBasket.Basket!.BasketItems);
            Assert.Equal(1, item.Id);
            Assert.Equal(2, item.Quantity); // no duplication from repeated/concurrent merges

            // Guest key should be gone and merge marker should exist (idempotency)
            var markerKey = $"basket:merge:{userId}:{ComputeSha256(guestCred)}";
            Assert.False(await db.KeyExistsAsync(guestKey));
            Assert.True(await db.KeyExistsAsync(markerKey));
        }
        finally
        {
            await svc.ClearBasketAsync(new BasketOwnerContext(userId, null, true));
            await db.KeyDeleteAsync($"basket:merge:{userId}:{ComputeSha256(guestCred)}");
            await db.KeyDeleteAsync($"basket:guest:{ComputeSha256(guestCred)}");
            mux.Dispose();
        }
    }

    private static string ComputeSha256(string s)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
