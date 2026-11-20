namespace Ecom.Core.Interfaces;

using Ecom.Core.DTO.Checkout;

/// <summary>
/// Interface for checkout summary computation and pricing token generation.
/// All monetary calculations are server-authoritative in EGP.
/// </summary>
public interface ICheckoutService
{
    /// <summary>
    /// Computes the checkout summary for the authenticated user.
    /// Returns a structured summary with items, prices, delivery options,
    /// and a secure pricing confirmation token.
    /// </summary>
    /// <param name="buyerId">Authenticated user ID from verified claims (ClaimTypes.NameIdentifier).</param>
    /// <param name="basketOwnerContext">Context resolving the basket (user or guest).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>CheckoutSummaryDto with validation, pricing, and token.</returns>
    Task<CheckoutSummaryDto> GetCheckoutSummaryAsync(string buyerId, BasketOwnerContext basketOwnerContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a secure pricing confirmation token for the given summary context.
    /// The token is bound to buyer identity, basket fingerprint, pricing context,
    /// delivery method, currency (EGP), and has a configurable TTL (default 10 minutes).
    /// A valid token is never proof that prices remain current; DB revalidation is always mandatory on order creation.
    /// </summary>
    /// <param name="buyerId">Authenticated user ID.</param>
    /// <param name="basketFingerprint">Normalized basket contents fingerprint.</param>
    /// <param name="pricingFingerprint">Normalized pricing fingerprint.</param>
    /// <param name="selectedDeliveryMethodId">Selected delivery method ID, or null.</param>
    /// <param name="currency">Currency code (EGP).</param>
    /// <param name="ttlSeconds">Token lifetime in seconds (default 600 = 10 minutes).</param>
    /// <returns>PricingToken with signed payload and signature.</returns>
    Task<PricingToken> GeneratePricingTokenAsync(string buyerId, string basketFingerprint, string pricingFingerprint, int? selectedDeliveryMethodId, string currency, int ttlSeconds = 600);
}