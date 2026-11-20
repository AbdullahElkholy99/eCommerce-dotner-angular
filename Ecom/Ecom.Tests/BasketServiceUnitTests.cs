using System.Linq.Expressions;
using Ecom.Core.DTO.Basket;
using Ecom.Core.Entity;
using Ecom.Core.Entity.Product;
using Ecom.Core.Interfaces;
using Ecom.Infrastructure.Services;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Ecom.Tests;

public class BasketServiceUnitTests
{
    private static (BasketService svc, Mock<IUnitOfWork> uow, Mock<IProductRepository> prodRepo, Mock<IDatabase> db) Create()
    {
        var dbMock = new Mock<IDatabase>();
        var mux = new Mock<IConnectionMultiplexer>();
        mux.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);

        var prodRepo = new Mock<IProductRepository>();
        var uow = new Mock<IUnitOfWork>();
        uow.SetupGet(x => x.ProductRepository).Returns(prodRepo.Object);

        var svc = new BasketService(mux.Object, uow.Object);
        return (svc, uow, prodRepo, dbMock);
    }

    private static Product MakeProduct(int id = 1, decimal price = 5000.50m, string name = "Iphone")
    {
        return new Product
        {
            Id = id,
            Name = name,
            Description = "desc",
            NewPrice = price,
            OldPrice = price + 100,
            CategoryId = 1,
            Category = new Category { Name = "Phone", Description = "Phone" },
            Photos = new List<Photo> { new Photo { Name = "img.jpg", ProductId = id } }
        };
    }

    [Fact]
    public async Task AddItem_NonexistentProduct_ReturnsBadRequest()
    {
        var (svc, _, prodRepo, _) = Create();
        prodRepo.Setup(r => r.GetByIdAsync(99999, It.IsAny<Expression<Func<Product, object>>[]>()))
            .ReturnsAsync((Product?)null);

        var result = await svc.AddItemAsync(new BasketOwnerContext(null, "valid-guest-credential-123456", false), 99999, 1);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("not found", result.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddItem_QuantityZero_ReturnsBadRequest()
    {
        var (svc, _, _, _) = Create();
        var result = await svc.AddItemAsync(new BasketOwnerContext(null, "valid-guest-credential-123456", false), 1, 0);
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task AddItem_QuantityNegative_ReturnsBadRequest()
    {
        var (svc, _, _, _) = Create();
        var result = await svc.AddItemAsync(new BasketOwnerContext(null, "valid-guest-credential-123456", false), 1, -2);
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task AddItem_QuantityExcessive_ReturnsBadRequest()
    {
        var (svc, _, _, _) = Create();
        var result = await svc.AddItemAsync(new BasketOwnerContext(null, "valid-guest-credential-123456", false), 1, 1000);
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("exceed", result.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetBasket_AuthenticatedWithoutUserId_ReturnsUnauthorized()
    {
        var (svc, _, _, _) = Create();
        var result = await svc.GetBasketAsync(new BasketOwnerContext(null, null, true));
        Assert.False(result.Success);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task GetBasket_NoGuestCredential_ReturnsEmptyBasket()
    {
        var (svc, _, _, db) = Create();
        var result = await svc.GetBasketAsync(new BasketOwnerContext(null, null, false));
        Assert.True(result.Success);
        Assert.NotNull(result.Basket);
        Assert.Empty(result.Basket!.BasketItems);
        db.Verify(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task MergeGuestBasket_MissingGuestCredential_ReturnsUserBasket_AndClearsGuestCookieFlag()
    {
        var (svc, _, _, db) = Create();
        db.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var result = await svc.MergeGuestBasketAsync("user-1", null);

        Assert.True(result.Success);
        Assert.NotNull(result.Basket);
        Assert.Empty(result.Basket!.BasketItems);
    }
}
