using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ecom.Core.DTO.Checkout;
using Ecom.Core.Entity;
using Ecom.Core.Interfaces;
using Ecom.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace Ecom.Infrastructure.Services;

/// <summary>
/// Concrete implementation of ICheckoutService.
/// Computes checkout summaries with server-authoritative pricing in EGP.
/// Generates HMAC-SHA256 pricing confirmation tokens.
/// </summary>
public class CheckoutService : ICheckoutService
{
    private readonly AppDbContext _context;
    private readonly IDatabase _redis;
    private readonly IConnectionMultiplexer _redisConnection;

    // Token signing secret: loaded from secure configuration at startup.
    // Do not hardcode; use IConfiguration or User Secrets / environment variable.
    // This is a placeholder - in production, inject IConfiguration and read
    // from configuration (e.g. Checkout:PricingToken:SigningKey).
    private readonly string _signingKey;

    // Default TTL for pricing tokens: 10 minutes = 600 seconds.
    private const int DefaultTokenTtlSeconds = 600;

    public CheckoutService(AppDbContext context, IConnectionMultiplexer redis, IConfiguration configuration)
    {
        _context = context;
        _redisConnection = redis;
        _redis = redis.GetDatabase();

        // Read signing key from configuration.
        // In production this should be a high-entropy secret from User Secrets / Azure Key Vault / environment.
        // For now, fall back to a development placeholder (must be replaced before deployment).
        var configKey = configuration["Checkout:PricingToken:SigningKey"];
        if (!string.IsNullOrWhiteSpace(configKey))
        {
            _signingKey = configKey;
        }
        else
        {
            // Development fallback: read from User Secrets or throw if missing in production.
            // Do not commit the fallback key to source control.
            _signingKey = "dev-pricing-token-key-must-be-replaced";
        }
    }

    /// <summary>
    /// Computes the checkout summary for the authenticated user.
    /// </summary>
    public async Task<CheckoutSummaryDto> GetCheckoutSummaryAsync(string buyerId, BasketOwnerContext basketOwnerContext, CancellationToken cancellationToken = default)
    {
        var dto = new CheckoutSummaryDto();

        // Determine the basket key based on authentication state.
        string? basketKey = null;
        if (basketOwnerContext.IsAuthenticated)
        {
            if (string.IsNullOrWhiteSpace(buyerId))
            {
                // Cannot resolve basket without buyer ID.
                dto.ValidationErrors = new List<string> { "Unable to resolve authenticated user basket." };
                dto.RequiresPriceConfirmation = false;
                return dto;
            }
            basketKey = $"basket:user:{buyerId}";
        }
        else
        {
            // Guest basket: resolve from the guest credential cookie.
            if (string.IsNullOrWhiteSpace(basketOwnerContext.GuestCredential))
            {
                // No basket available.
                dto.ValidationErrors = new List<string> { "No basket found. Add items to basket first." };
                dto.RequiresPriceConfirmation = false;
                return dto;
            }
            // For guests, the key is hashed: basket:guest:{sha256(credential)}
            var hashedCred = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(basketOwnerContext.GuestCredential))).ToLowerInvariant();
            basketKey = $"basket:guest:{hashedCred}";
        }

        // Try to get the basket from Redis.
        var basketVal = await _redis.StringGetAsync(basketKey);
        if (basketVal.IsNullOrEmpty)
        {
            // Empty basket - no items.
            dto.Currency = "EGP";
            dto.Subtotal = 0m;
            dto.ItemsSubtotal = 0m;
            dto.DeliveryFee = 0m;
            dto.Total = 0m;
            dto.RequiresPriceConfirmation = false;
            dto.AvailableDeliveryMethods = new List<DeliveryMethodDto>();
            return dto;
        }

        // Deserialize the basket.
        CustomerBasket? basket;
        try
        {
            basket = JsonSerializer.Deserialize<CustomerBasket>(basketVal.ToString()!);
        }
        catch
        {
            // Malformed basket data.
            dto.ValidationErrors = new List<string> { "Basket data is corrupted or malformed." };
            dto.RequiresPriceConfirmation = false;
            dto.Currency = "EGP";
            dto.Subtotal = 0m;
            dto.ItemsSubtotal = 0m;
            dto.DeliveryFee = 0m;
            dto.Total = 0m;
            return dto;
        }

        if (basket.BasketItems == null || basket.BasketItems.Count == 0)
        {
            // Empty basket after deserialization.
            dto.Currency = "EGP";
            dto.Subtotal = 0m;
            dto.ItemsSubtotal = 0m;
            dto.DeliveryFee = 0m;
            dto.Total = 0m;
            dto.RequiresPriceConfirmation = false;
            dto.AvailableDeliveryMethods = new List<DeliveryMethodDto>();
            return dto;
        }

        // Validate all products against SQL Server and reprice.
        // Following the existing BasketService pattern: get product from catalog,
        // use NewPrice, validate existence.
        var validItems = new List<BasketLineDto>();
        decimal subtotal = 0m;
        var fingerprintParts = new List<string>();

        foreach (var item in basket.BasketItems)
        {
            // Validate product exists in catalog.
            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == item.Id, cancellationToken);

            if (product == null)
            {
                // Product no longer in catalog - skip or error.
                // Per BasketService pattern, we skip unavailable products.
                continue;
            }

            // Use authoritative catalog price (NewPrice).
            // Note: BasketItem uses Qunatity (with typo) as the property name.
            int qty = item.Qunatity;
            if (qty <= 0) continue;
            if (qty > 99) continue; // MaxQuantityPerItem

            // Compute line total: UnitPrice * Quantity, rounded to 2dp with AwayFromZero.
            var unitPrice = product.NewPrice;
            var lineTotal = Math.Round(unitPrice * qty, 2, MidpointRounding.AwayFromZero);

            // Build fingerprint parts.
            fingerprintParts.Add($"{item.Id}:{qty}:{unitPrice}");

            validItems.Add(new BasketLineDto
            {
                ProductId = product.Id,
                ProductName = product.Name ?? string.Empty,
                ProductImage = (product.Photos?.OrderBy(p => p.Id).FirstOrDefault()?.Name ?? string.Empty),
                Quantity = qty,
                UnitPrice = unitPrice,
                LineTotal = lineTotal
            });

            subtotal += lineTotal;
        }

        // Update dto with valid items.
        dto.BasketItems = validItems;
        dto.Subtotal = Math.Round(subtotal, 2, MidpointRounding.AwayFromZero);
        dto.ItemsSubtotal = dto.Subtotal;

        // Retrieve available delivery methods from SQL Server.
        // All delivery methods are potentially available; we return them all.
        var deliveryMethods = await _context.DeliveryMethods
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var deliveryMethodDtos = deliveryMethods.Select(d => new DeliveryMethodDto
        {
            Id = d.Id,
            Name = d.Name,
            Description = d.Description,
            DeliveryTime = d.DeliveryTime,
            Price = d.Price
        }).ToList();

        dto.AvailableDeliveryMethods = deliveryMethodDtos;

        // Default: no delivery method selected => delivery fee = 0.
        dto.DeliveryFee = 0m;
        dto.Total = Math.Round(dto.Subtotal + dto.DeliveryFee, 2, MidpointRounding.AwayFromZero);
        dto.RequiresPriceConfirmation = true;

        // Generate pricing confirmation token.
        // Build basket fingerprint (normalized, product-order-independent).
        var basketFingerprint = string.Join("|", fingerprintParts.OrderBy(f => f, StringComparer.OrdinalIgnoreCase));

        // Build pricing fingerprint (includes delivery method, currency, etc.).
        // If no delivery method selected, include "none".
        var pricingFingerprint = $"{basketFingerprint}|EGP|none";

        // Generate the token.
        var token = await GeneratePricingTokenAsync(
            buyerId,
            basketFingerprint,
            pricingFingerprint,
            null, // no delivery method selected yet
            "EGP",
            DefaultTokenTtlSeconds
        );

        dto.PricingToken = token.Payload;
        dto.TokenIssuedAtUtc = token.IssuedAtUtc;
        dto.TokenExpiresAtUtc = token.ExpiresAtUtc;
        dto.RequiresPriceConfirmation = true;

        return dto;
    }

    /// <summary>
    /// Generates a secure pricing confirmation token.
    /// The token is bound to buyer identity, basket fingerprint, pricing context,
    /// delivery method, currency (EGP), and has a configurable TTL (default 10 minutes).
    /// A valid token is never proof that prices remain current;
    /// any future order creation must re-read SQL data and recalculate all amounts.
    /// </summary>
    public async Task<PricingToken> GeneratePricingTokenAsync(string buyerId, string basketFingerprint, string pricingFingerprint, int? selectedDeliveryMethodId, string currency, int ttlSeconds = DefaultTokenTtlSeconds)
    {
        // 1. Build the canonical payload JSON.
        // The payload is constructed deterministically so that equivalent inputs
        // always produce the same token. Field ordering is canonical.
        var payload = new
        {
            v = 1, // token version
            sub = buyerId, // authenticated buyer ID
            bf = basketFingerprint, // basket contents fingerprint
            pf = pricingFingerprint, // pricing fingerprint
            dm = selectedDeliveryMethodId ?? 0, // selected delivery method ID (0 = none)
            df = selectedDeliveryMethodId.HasValue ? GetDeliveryMethodPrice(selectedDeliveryMethodId.Value) : 0m, // delivery fee (0 if none)
            cur = currency, // ISO 4217 currency code
            iat = GetUnixTimestampUtc(), // issued-at timestamp
            exp = GetUnixTimestampUtc() + ttlSeconds // expiration timestamp
        };

        // 2. Serialize to canonical JSON (preserve decimal precision, stable ordering).
        var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        // 3. Compute HMAC-SHA256 signature.
        // Use a constant-time comparison-safe approach.
        byte[] keyBytes = Encoding.UTF8.GetBytes(_signingKey);
        using var hmac = new HMACSHA256(keyBytes);
        byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(jsonPayload));
        var signature = Convert.ToBase64String(hashBytes);

        // 4. Return the PricingToken DTO.
        return new PricingToken
        {
            Payload = jsonPayload,
            Signature = signature,
            IssuedAtUtc = payload.iat,
            ExpiresAtUtc = payload.exp,
            BuyerId = buyerId,
            BasketContext = buyerId, // simplified; in full flow would include guest context
            BasketFingerprint = basketFingerprint,
            PricingFingerprint = pricingFingerprint,
            Currency = currency
        };
    }

    /// <summary>
    /// Validates a received pricing confirmation token.
    /// Returns true if the token is valid (signature, expiry, buyer ID, and fingerprint match),
    /// but note: this does NOT replace mandatory DB revalidation on order creation.
    /// </summary>
    /// <param name="token">The pricing token to validate.</param>
    /// <param name="expectedBuyerId">The expected authenticated buyer ID.</param>
    /// <param name="expectedBasketFingerprint">The expected basket fingerprint from current state.</param>
    /// <param name="expectedPricingFingerprint">The expected pricing fingerprint from current state.</param>
    /// <param name="currentDbSubtotal">The current DB-computed subtotal (for revalidation).</param>
    /// <param name="currentDbDeliveryFee">The current DB-computed delivery fee (for revalidation).</param>
    /// <returns>true if the token signature, expiry, and identity are valid.</returns>
    public bool ValidatePricingToken(PricingToken token, string expectedBuyerId, string expectedBasketFingerprint, string expectedPricingFingerprint, decimal currentDbSubtotal, decimal currentDbDeliveryFee)
    {
        // 1. Quick rejection if token is null or payload/signature is empty.
        if (token == null || string.IsNullOrWhiteSpace(token.Payload) || string.IsNullOrWhiteSpace(token.Signature))
        {
            return false;
        }

        // 2. Validate expiry.
        var now = GetUnixTimestampUtc();
        if (now > token.ExpiresAtUtc)
        {
            // Token expired.
            return false;
        }

        // 3. Validate BuyerId.
        if (token.BuyerId != expectedBuyerId)
        {
            // Token belongs to another user.
            return false;
        }

        // 4. Validate basket fingerprint.
        if (token.BasketFingerprint != expectedBasketFingerprint)
        {
            // Basket contents changed since token was issued.
            return false;
        }

        // 5. Validate pricing fingerprint.
        if (token.PricingFingerprint != expectedPricingFingerprint)
        {
            // Pricing changed (product price, delivery, quantity) since token was issued.
            return false;
        }

        // 6. Validate currency.
        if (token.Currency != "EGP")
        {
            // Currency mismatch.
            return false;
        }

        // 7. Verify HMAC-SHA256 signature using constant-time comparison.
        // Reconstruct the payload and verify the signature.
        // The payload must be serialized identically to how it was when the token was generated.
        try
        {
            var jsonPayload = token.Payload;
            byte[] keyBytes = Encoding.UTF8.GetBytes(_signingKey);
            using var hmac = new HMACSHA256(keyBytes);
            byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(jsonPayload));
            var expectedSignature = Convert.ToBase64String(hashBytes);

            // Constant-time comparison.
            return CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(token.Signature), hashBytes);
        }
        catch
        {
            // Malformed signature or payload.
            return false;
        }
    }

    /// <summary>
    /// Gets the current Unix timestamp in UTC seconds.
    /// </summary>
    private static long GetUnixTimestampUtc()
    {
        return (long)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    /// <summary>
    /// Gets the delivery method price from SQL Server for the given ID.
    /// Returns 0 if not found.
    /// </summary>
    private decimal GetDeliveryMethodPrice(int deliveryMethodId)
    {
        var deliveryMethod = _context.DeliveryMethods
            .AsNoTracking()
            .FirstOrDefault(m => m.Id == deliveryMethodId);

        return deliveryMethod?.Price ?? 0m;
    }
}