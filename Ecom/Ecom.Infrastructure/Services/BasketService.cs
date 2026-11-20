using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ecom.Core.DTO.Basket;
using Ecom.Core.Entity;
using Ecom.Core.Interfaces;
using StackExchange.Redis;

namespace Ecom.Infrastructure.Services;

public class BasketService : IBasketService
{
    private const int BasketTtlDays = 3;
    private const int MaxQuantityPerItem = 99;
    private static readonly TimeSpan BasketTtl = TimeSpan.FromDays(BasketTtlDays);

    private readonly IDatabase _redis;
    private readonly IUnitOfWork _unitOfWork;

    public BasketService(IConnectionMultiplexer redis, IUnitOfWork unitOfWork)
    {
        _redis = redis.GetDatabase();
        _unitOfWork = unitOfWork;
    }

    public static TimeSpan GetBasketTtl() => BasketTtl;
    public static string GuestCookieNameConst => "guest_basket_id";

    public async Task<BasketOperationResult> GetBasketAsync(BasketOwnerContext owner, CancellationToken ct = default)
    {
        var (key, error) = ResolveKey(owner);
        if (error is not null) return error;
        if (key is null)
        {
            return OkResult(EmptyBasket());
        }

        var basket = await GetBasketByKeyAsync(key);
        return OkResult(ToDto(basket) ?? EmptyBasket());
    }

    public async Task<BasketOperationResult> AddItemAsync(BasketOwnerContext owner, int productId, int quantity, CancellationToken ct = default)
    {
        if (quantity <= 0) return BadRequest("Quantity must be a positive integer.");
        if (quantity > MaxQuantityPerItem) return BadRequest($"Quantity cannot exceed {MaxQuantityPerItem}.");

        var product = await GetProductForBasketAsync(productId);
        if (product is null) return BadRequest("Product not found.");

        var keyResult = ResolveOrPrepareKey(owner);
        if (keyResult.Error is not null) return keyResult.Error;
        var key = keyResult.Key!;
        var newGuestCredential = keyResult.NewGuestCredential;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var basket = await GetBasketByKeyAsync(key) ?? NewBasket();
            var existing = basket.BasketItems.FirstOrDefault(i => i.Id == productId);
            var newQty = (existing?.Qunatity ?? 0) + quantity;
            if (newQty > MaxQuantityPerItem) return BadRequest($"Quantity cannot exceed {MaxQuantityPerItem}.");
            if (existing is null)
            {
                basket.BasketItems.Add(MapFromProduct(product, newQty));
            }
            else
            {
                UpdateItemFromProduct(existing, product, newQty);
            }
            NormalizeBasket(basket);
            if (await TrySaveAsync(key, basket)) return OkResult(ToDto(basket)!, newGuestCredential: newGuestCredential);
        }
        return Conflict();
    }

    public async Task<BasketOperationResult> UpdateItemQuantityAsync(BasketOwnerContext owner, int productId, int quantity, CancellationToken ct = default)
    {
        if (quantity <= 0) return BadRequest("Quantity must be a positive integer.");
        if (quantity > MaxQuantityPerItem) return BadRequest($"Quantity cannot exceed {MaxQuantityPerItem}.");

        var (key, error) = ResolveKey(owner);
        if (error is not null) return error;
        if (key is null) return NotFoundBasket();

        var product = await GetProductForBasketAsync(productId);
        if (product is null) return BadRequest("Product not found.");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var basket = await GetBasketByKeyAsync(key);
            if (basket is null) return NotFoundBasket();
            var existing = basket.BasketItems.FirstOrDefault(i => i.Id == productId);
            if (existing is null) return BadRequest("Item not found in basket.");
            UpdateItemFromProduct(existing, product, quantity);
            NormalizeBasket(basket);
            if (await TrySaveAsync(key, basket)) return OkResult(ToDto(basket)!);
        }
        return Conflict();
    }

    public async Task<BasketOperationResult> RemoveItemAsync(BasketOwnerContext owner, int productId, CancellationToken ct = default)
    {
        var (key, error) = ResolveKey(owner);
        if (error is not null) return error;
        if (key is null) return NotFoundBasket();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var basket = await GetBasketByKeyAsync(key);
            if (basket is null) return NotFoundBasket();
            var removed = basket.BasketItems.RemoveAll(i => i.Id == productId);
            if (removed == 0) return BadRequest("Item not found in basket.");
            if (basket.BasketItems.Count == 0)
            {
                await _redis.KeyDeleteAsync(key);
                return OkResult(EmptyBasket());
            }
            NormalizeBasket(basket);
            if (await TrySaveAsync(key, basket)) return OkResult(ToDto(basket)!);
        }
        return Conflict();
    }

    public async Task<BasketOperationResult> ClearBasketAsync(BasketOwnerContext owner, CancellationToken ct = default)
    {
        var (key, error) = ResolveKey(owner);
        if (error is not null) return error;
        if (key is null) return OkResult(EmptyBasket());
        await _redis.KeyDeleteAsync(key);
        return OkResult(EmptyBasket());
    }

    public async Task<BasketOperationResult> ReplaceItemsAsync(BasketOwnerContext owner, IEnumerable<(int ProductId, int Quantity)> items, CancellationToken ct = default)
    {
        var itemList = items?.ToList() ?? new List<(int ProductId, int Quantity)>();
        if (itemList.Count == 0)
        {
            return await ClearBasketAsync(owner, ct);
        }

        // validate and build
        var built = new List<BasketItem>();
        foreach (var g in itemList.GroupBy(x => x.ProductId))
        {
            var qty = g.Sum(x => x.Quantity);
            if (qty <= 0) return BadRequest("Quantity must be a positive integer.");
            if (qty > MaxQuantityPerItem) return BadRequest($"Quantity cannot exceed {MaxQuantityPerItem}.");
            var product = await GetProductForBasketAsync(g.Key);
            if (product is null) return BadRequest("One or more products are invalid or unavailable.");
            built.Add(MapFromProduct(product, qty));
        }

        var keyResult = ResolveOrPrepareKey(owner);
        if (keyResult.Error is not null) return keyResult.Error;
        var key = keyResult.Key!;
        var newGuestCredential = keyResult.NewGuestCredential;

        var basket = NewBasket();
        basket.BasketItems = built;
        NormalizeBasket(basket);
        if (await TrySaveAsync(key, basket)) return OkResult(ToDto(basket)!, newGuestCredential: newGuestCredential);
        return Conflict();
    }

    public async Task<BasketOperationResult> MergeGuestBasketAsync(string userId, string? guestCredential, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        var userKey = UserKey(userId);
        if (string.IsNullOrWhiteSpace(guestCredential) || !IsValidCredential(guestCredential))
        {
            var userBasket = await GetBasketByKeyAsync(userKey);
            return OkResult(ToDto(userBasket) ?? EmptyBasket(), clearGuestCookie: !string.IsNullOrWhiteSpace(guestCredential));
        }

        var guestKey = GuestKey(guestCredential);
        var mergeMarkerKey = MergeMarkerKey(userId, guestCredential);

        if (await _redis.KeyExistsAsync(mergeMarkerKey))
        {
            await _redis.KeyDeleteAsync(guestKey);
            var userBasket = await GetBasketByKeyAsync(userKey);
            return OkResult(ToDto(userBasket) ?? EmptyBasket(), clearGuestCookie: true);
        }

        var guestBasket = await GetBasketByKeyAsync(guestKey);
        if (guestBasket is null || guestBasket.BasketItems.Count == 0)
        {
            await _redis.StringSetAsync(mergeMarkerKey, "1", BasketTtl);
            await _redis.KeyDeleteAsync(guestKey);
            var userBasket = await GetBasketByKeyAsync(userKey);
            return OkResult(ToDto(userBasket) ?? EmptyBasket(), clearGuestCookie: true);
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            if (await _redis.KeyExistsAsync(mergeMarkerKey))
            {
                await _redis.KeyDeleteAsync(guestKey);
                var ub = await GetBasketByKeyAsync(userKey);
                return OkResult(ToDto(ub) ?? EmptyBasket(), clearGuestCookie: true);
            }

            var currentGuest = await GetBasketByKeyAsync(guestKey);
            if (currentGuest is null || currentGuest.BasketItems.Count == 0)
            {
                await _redis.StringSetAsync(mergeMarkerKey, "1", BasketTtl);
                await _redis.KeyDeleteAsync(guestKey);
                var ub = await GetBasketByKeyAsync(userKey);
                return OkResult(ToDto(ub) ?? EmptyBasket(), clearGuestCookie: true);
            }

            var userBasket = await GetBasketByKeyAsync(userKey) ?? NewBasket();
            var merged = await BuildMergedBasketAsync(userBasket, currentGuest);

            var script = @"
if redis.call('EXISTS', KEYS[3]) == 1 then return 0 end
redis.call('SET', KEYS[1], ARGV[1], 'EX', ARGV[2])
redis.call('DEL', KEYS[2])
redis.call('SET', KEYS[3], '1', 'EX', ARGV[2])
return 1";
            var result = (int)(long)await _redis.ScriptEvaluateAsync(script,
                new RedisKey[] { userKey, guestKey, mergeMarkerKey },
                new RedisValue[] { JsonSerializer.Serialize(merged), (int)BasketTtl.TotalSeconds });
            if (result == 1)
            {
                return OkResult(ToDto(merged)!, clearGuestCookie: true);
            }
        }

        return Conflict();
    }

    private async Task<Ecom.Core.Entity.Product.Product?> GetProductForBasketAsync(int productId)
    {
        return await _unitOfWork.ProductRepository.GetByIdAsync(productId, p => p.Photos, p => p.Category);
    }

    private async Task<CustomerBasket> BuildMergedBasketAsync(CustomerBasket userBasket, CustomerBasket guestBasket)
    {
        var merged = new CustomerBasket
        {
            Id = userBasket.Id ?? "user",
            PaymentIntentId = userBasket.PaymentIntentId ?? string.Empty,
            ClientSecret = userBasket.ClientSecret ?? string.Empty,
            BasketItems = new List<BasketItem>()
        };

        // Start with validated user basket items repriced from catalog
        foreach (var item in userBasket.BasketItems)
        {
            var product = await _unitOfWork.ProductRepository.GetByIdAsync(item.Id);
            if (product is null) continue;
            var qty = ClampQuantity(item.Qunatity);
            if (qty <= 0) continue;
            merged.BasketItems.Add(MapFromProduct(product, qty));
        }

        // Merge guest items: sum quantities for same product, add distinct products
        foreach (var g in guestBasket.BasketItems)
        {
            var product = await _unitOfWork.ProductRepository.GetByIdAsync(g.Id);
            if (product is null) continue; // product deleted/unavailable in catalog
            var gQty = ClampQuantity(g.Qunatity);
            if (gQty <= 0) continue;
            var existing = merged.BasketItems.FirstOrDefault(i => i.Id == g.Id);
            if (existing is null)
            {
                merged.BasketItems.Add(MapFromProduct(product, gQty));
            }
            else
            {
                existing.Qunatity = Math.Min(existing.Qunatity + gQty, MaxQuantityPerItem);
                UpdateItemFromProduct(existing, product, existing.Qunatity);
            }
        }

        NormalizeBasket(merged);
        return merged;
    }

    private async Task<CustomerBasket?> GetBasketByKeyAsync(string key)
    {
        var val = await _redis.StringGetAsync(key);
        if (val.IsNullOrEmpty) return null;
        try
        {
            return JsonSerializer.Deserialize<CustomerBasket>(val.ToString()!);
        }
        catch
        {
            // malformed/corrupted data: treat as missing to avoid breaking clients; caller may overwrite
            return null;
        }
    }

    private async Task<bool> TrySaveAsync(string key, CustomerBasket basket)
    {
        // simple atomic set (last validated state wins per-attempt; callers retry on race-sensitive flows)
        var json = JsonSerializer.Serialize(basket);
        return await _redis.StringSetAsync(key, json, BasketTtl);
    }

    private (string? Key, BasketOperationResult? Error) ResolveKey(BasketOwnerContext owner)
    {
        if (owner.IsAuthenticated)
        {
            if (string.IsNullOrWhiteSpace(owner.UserId)) return (null, Unauthorized());
            return (UserKey(owner.UserId), null);
        }
        if (string.IsNullOrWhiteSpace(owner.GuestCredential))
        {
            return (null, null);
        }
        if (!IsValidCredential(owner.GuestCredential)) return (null, null); // treat invalid credential as no basket access
        return (GuestKey(owner.GuestCredential), null);
    }

    private (string? Key, BasketOperationResult? Error, string? NewGuestCredential) ResolveOrPrepareKey(BasketOwnerContext owner)
    {
        if (owner.IsAuthenticated)
        {
            if (string.IsNullOrWhiteSpace(owner.UserId)) return (null, Unauthorized(), null);
            return (UserKey(owner.UserId), null, null);
        }
        var cred = owner.GuestCredential;
        if (string.IsNullOrWhiteSpace(cred) || !IsValidCredential(cred))
        {
            cred = GenerateCredential();
            return (GuestKey(cred), null, cred);
        }
        return (GuestKey(cred), null, null);
    }

    private static string GenerateCredential()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static bool IsValidCredential(string cred)
    {
        if (cred.Length < 20 || cred.Length > 200) return false;
        foreach (var c in cred)
        {
            if (!(char.IsLetterOrDigit(c) || c == '-' || c == '_')) return false;
        }
        return true;
    }

    private static string GuestKey(string credential)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(credential))).ToLowerInvariant();
        return $"basket:guest:{hash}";
    }

    private static string UserKey(string userId) => $"basket:user:{userId}";
    private static string MergeMarkerKey(string userId, string credential)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(credential))).ToLowerInvariant();
        return $"basket:merge:{userId}:{hash}";
    }

    private static BasketItem MapFromProduct(Ecom.Core.Entity.Product.Product product, int quantity)
    {
        var image = (product.Photos ?? new List<Photo>()).OrderBy(p => p.Id).FirstOrDefault()?.Name ?? string.Empty;
        return new BasketItem
        {
            Id = product.Id,
            Name = product.Name ?? string.Empty,
            Description = product.Description ?? string.Empty,
            Image = image,
            Qunatity = ClampQuantity(quantity),
            Price = product.NewPrice,
            Category = product.Category?.Name ?? string.Empty
        };
    }

    private static void UpdateItemFromProduct(BasketItem item, Ecom.Core.Entity.Product.Product product, int quantity)
    {
        var image = (product.Photos ?? new List<Photo>()).OrderBy(p => p.Id).FirstOrDefault()?.Name ?? string.Empty;
        item.Name = product.Name ?? string.Empty;
        item.Description = product.Description ?? string.Empty;
        item.Image = image;
        item.Price = product.NewPrice;
        item.Category = product.Category?.Name ?? string.Empty;
        item.Qunatity = ClampQuantity(quantity);
    }

    private static void NormalizeBasket(CustomerBasket basket)
    {
        basket.PaymentIntentId ??= string.Empty;
        basket.ClientSecret ??= string.Empty;
        basket.BasketItems ??= new List<BasketItem>();
        var grouped = new List<BasketItem>();
        foreach (var g in basket.BasketItems.GroupBy(i => i.Id))
        {
            var first = g.First();
            first.Qunatity = Math.Min(g.Sum(x => Math.Max(0, x.Qunatity)), MaxQuantityPerItem);
            if (first.Qunatity > 0) grouped.Add(first);
        }
        basket.BasketItems = grouped;
    }

    private static int ClampQuantity(int q) => q <= 0 ? 0 : (q > MaxQuantityPerItem ? MaxQuantityPerItem : q);

    private static CustomerBasket NewBasket() => new CustomerBasket { Id = string.Empty, PaymentIntentId = string.Empty, ClientSecret = string.Empty, BasketItems = new List<BasketItem>() };
    private static CustomerBasketDto EmptyBasket() => new CustomerBasketDto { Id = null, BasketItems = new List<BasketItemDto>() };

    private static CustomerBasketDto? ToDto(CustomerBasket? basket)
    {
        if (basket is null) return null;
        return new CustomerBasketDto
        {
            Id = null,
            BasketItems = (basket.BasketItems ?? new List<BasketItem>()).Select(i => new BasketItemDto
            {
                Id = i.Id,
                Name = i.Name ?? string.Empty,
                Description = i.Description ?? string.Empty,
                Image = i.Image ?? string.Empty,
                Quantity = i.Qunatity,
                Price = i.Price,
                Category = i.Category ?? string.Empty
            }).ToList()
        };
    }

    private static BasketOperationResult OkResult(CustomerBasketDto basket, string? newGuestCredential = null, bool clearGuestCookie = false)
        => new(true, 200, null, basket, newGuestCredential, clearGuestCookie);

    private static BasketOperationResult BadRequest(string message) => new(false, 400, message, null);
    private static BasketOperationResult NotFoundBasket() => new(false, 404, "Basket not found.", null);
    private static BasketOperationResult Unauthorized() => new(false, 401, "Unauthorized.", null);
    private static BasketOperationResult Conflict() => new(false, 409, "Basket update conflict. Please retry.", null);
}
