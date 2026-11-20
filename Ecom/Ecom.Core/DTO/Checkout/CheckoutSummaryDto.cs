namespace Ecom.Core.DTO.Checkout;

/// <summary>
/// DTO representing a single line item in the checkout summary.
/// Prices and totals are from the authoritative server catalog (decimal, EGP).
/// </summary>
public class BasketLineDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductImage { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

/// <summary>
/// DTO for delivery method options returned in the checkout summary.
/// Prices are server-owned and in EGP.
/// </summary>
public class DeliveryMethodDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DeliveryTime { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

/// <summary>
/// Main checkout summary response returned by GET /api/Checkout.
/// All monetary values are server-calculated in EGP.
/// </summary>
public class CheckoutSummaryDto
{
    /// <summary>
    /// ISO 4217 currency code for the project (EGP).
    /// </summary>
    public string Currency { get; set; } = "EGP";

    /// <summary>
    /// Server-calculated subtotal of all basket items (EGP).
    /// </summary>
    public decimal Subtotal { get; set; }

    /// <summary>
    /// Server-calculated delivery fee for the selected delivery method (EGP).
    /// Zero if no delivery method selected or valid delivery not chosen.
    /// </summary>
    public decimal DeliveryFee { get; set; }

    /// <summary>
    /// Server-calculated final total = Subtotal + DeliveryFee (EGP).
    /// </summary>
    public decimal Total { get; set; }

    /// <summary>
    /// Server-calculated subtotal of all basket items before delivery (EGP).
    /// </summary>
    public decimal ItemsSubtotal { get; set; }

    /// <summary>
    /// True if the summary requires customer price confirmation (pricing token needed).
    /// </summary>
    public bool RequiresPriceConfirmation { get; set; }

    /// <summary>
    /// Secure pricing confirmation token (HMAC-SHA256 signed).
    /// Must be submitted with future order creation.
    /// </summary>
    public string? PricingToken { get; set; }

    /// <summary>
    /// Token issuance timestamp (UTC, Unix epoch seconds).
    /// </summary>
    public long TokenIssuedAtUtc { get; set; }

    /// <summary>
    /// Token expiration timestamp (UTC, Unix epoch seconds).
    /// Default TTL is 10 minutes from issuance.
    /// </summary>
    public long TokenExpiresAtUtc { get; set; }

    /// <summary>
    /// Server-derived fingerprint of the basket contents at summary time.
    /// Used to detect stale/changed confirmations.
    /// </summary>
    public string? BasketFingerprint { get; set; }

    /// <summary>
    /// Server-derived fingerprint of the pricing context at summary time.
    /// Includes product IDs, quantities, unit prices, delivery method, and currency.
    /// </summary>
    public string? PricingFingerprint { get; set; }

    /// <summary>
    /// List of validation errors encountered while computing the summary.
    /// </summary>
    public List<string>? ValidationErrors { get; set; }

    /// <summary>
    /// Available delivery methods for the current user/catalog.
    /// </summary>
    public List<DeliveryMethodDto>? AvailableDeliveryMethods { get; set; }

    /// <summary>
    /// Items in the current basket (server-validated and repriced).
    /// </summary>
    public List<BasketLineDto>? BasketItems { get; set; }
}