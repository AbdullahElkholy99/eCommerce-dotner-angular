namespace Ecom.Core.DTO.Checkout;

/// <summary>
/// HMAC-SHA256 authenticated pricing confirmation token.
/// Binds to buyer identity, basket fingerprint, pricing context, delivery, currency, and timestamps.
/// Must not be modified by the client; any tampering is detected on validation.
/// A valid token is never proof that prices remain current;
/// any future order creation must re-read SQL data and recalculate all amounts.
/// </summary>
public class PricingToken
{
    /// <summary>
    /// The signed payload (base64url-encoded or compact JSON).
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// The HMAC-SHA256 signature (base64url-encoded).
    /// </summary>
    public string Signature { get; set; } = string.Empty;

    /// <summary>
    /// Token issuance timestamp (UTC, Unix epoch seconds).
    /// </summary>
    public long IssuedAtUtc { get; set; }

    /// <summary>
    /// Token expiration timestamp (UTC, Unix epoch seconds).
    /// After this time the token is considered expired.
    /// </summary>
    public long ExpiresAtUtc { get; set; }

    /// <summary>
    /// Authenticated buyer identifier (from verified server-side claims, e.g. ClaimTypes.NameIdentifier).
    /// </summary>
    public string BuyerId { get; set; } = string.Empty;

    /// <summary>
    /// Server-derived basket ownership context (authenticated user or guest credential).
    /// </summary>
    public string BasketContext { get; set; } = string.Empty;

    /// <summary>
    /// Normalized basket contents fingerprint (product IDs, quantities, canonical ordering).
    /// Changes if items are added/removed/quantities change.
    /// </summary>
    public string BasketFingerprint { get; set; } = string.Empty;

    /// <summary>
    /// Normalized pricing fingerprint (product IDs, quantities, unit prices, delivery method, currency).
    /// Changes if product prices change, quantity changes, or delivery method/fee changes.
    /// </summary>
    public string PricingFingerprint { get; set; } = string.Empty;

    /// <summary>
    /// Token version, for future key rotation or format evolution.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// ISO 4217 currency code (EGP).
    /// </summary>
    public string Currency { get; set; } = "EGP";
}