using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Ecom.API.Helper;
using Ecom.Core.DTO.Basket;
using Ecom.Core.DTO.Checkout;
using Ecom.Core.Interfaces;
using Ecom.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace Ecom.API.Controllers;

/// <summary>
/// Secure Checkout Summary API endpoint.
/// 
/// GET /api/Checkout
/// 
/// Requires authentication.
/// Returns the current user's checkout summary with server-validated prices,
/// delivery options, and a secure pricing confirmation token.
/// 
/// Does NOT create an order, clear the basket, or modify any state.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CheckoutController : BaseController
{
    private readonly ICheckoutService _checkoutService;
    private readonly IBasketService _basketService;

    public CheckoutController(IBasketService basketService, ICheckoutService checkoutService, IUnitOfWork unitOfWork, IMapper mapper) : base(unitOfWork, mapper)
    {
        _basketService = basketService;
        _checkoutService = checkoutService;
    }

    /// <summary>
    /// Returns the checkout summary for the authenticated user.
    /// 
    /// This endpoint:
    /// - Resolves the authenticated user's basket server-side via IBasketService.
    /// - Validates all basket products against SQL Server catalog.
    /// - Reprices all items using authoritative catalog prices (Product.NewPrice).
    /// - Computes server-side subtotal, delivery fee, and total (EGP).
    /// - Returns available delivery methods.
    /// - Generates a secure HMAC-SHA256 pricing confirmation token.
    /// - Requires explicit customer confirmation of latest pricing.
    /// 
    /// The pricing token must be submitted with any future order creation
    /// and will be revalidated (with mandatory DB re-read and repricing)
    /// before any order is created.
    /// 
    /// Place Order remains disabled until a real payment method is approved
    /// (Phase 4 / Q5 design decision).
    /// 
    /// CSRF: GET is a safe method; no antiforgery validation required.
    /// Authentication: [Authorize] enforces JWT/bearer cookie auth.
    /// </summary>
    /// <param name="buyerId">Authenticated user ID from Claims (set by [Authorize] middleware).</param>
    /// <param name="basketOwnerContext">Resolved by GetOwnerContext() - user or guest.</param>
    /// <param name="deliveryMethodId">Optional query parameter to preselect a delivery method.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>CheckoutSummaryDto with items, prices, delivery options, and pricing token.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(CheckoutSummaryDto), 200)]
    [ProducesResponseType(typeof(ResponseAPI), 401)]
    [ProducesResponseType(typeof(ResponseAPI), 400)]
    public async Task<IActionResult> GetCheckoutSummary([FromQuery] int? deliveryMethodId, CancellationToken cancellationToken = default)
    {
        // 1. Resolve the authenticated user ID from claims.
        // [Authorize] ensures the user is authenticated; we get the ID from Claims.
        var userId = User?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            // Should not happen due to [Authorize], but defensive check.
            return Unauthorized(new ResponseAPI(401, "Unauthenticated access."));
        }

        // 2. Resolve the basket owner context.
        // We use the existing BasketsController pattern: get owner from claims + guest cookie.
        var ownerContext = GetOwnerContext();

        // 3. If the user is authenticated but has no guest cookie,
        // the basket should be the authenticated user's basket.
        // If the user is a guest, we rely on the guest basket cookie.

        // 4. Compute the checkout summary using the service.
        // The service validates products, reprices, computes totals, and generates token.
        var summary = await _checkoutService.GetCheckoutSummaryAsync(userId, ownerContext, cancellationToken);

        // 5. If a delivery method ID was selected via query string,
        // we can compute the delivery fee and total with that selection.
        // However, per the implementation scope, we do not trust client-supplied
        // pricing or validity. We only use the selection to identify which delivery
        // method the customer is viewing, but the backend recomputes/validates.

        // In this initial phase, we do not apply a selected delivery method
        // to change the summary totals; the summary returns all delivery methods
        // and the customer must make a selection and reconfirm pricing.

        // 6. Return the summary.
        // The response includes:
        // - Basket items with server-repriced unit prices and line totals (EGP).
        // - Subtotal, delivery fee (0 if none selected), total (EGP).
        // - Available delivery methods (all from catalog).
        // - Pricing confirmation token (HMAC-SHA256 signed).
        // - Token expiration (default 10 minutes).
        // - Currency = EGP.
        // - RequiresPriceConfirmation = true (customer must confirm latest prices).

        return Ok(summary);
    }

    /// <summary>
    /// Resolves the basket owner context from the current HTTP request.
    /// Uses the same pattern as BasketsController.GetOwnerContext().
    /// </summary>
    /// <returns>BasketOwnerContext with UserId, GuestCredential, and IsAuthenticated.</returns>
    private BasketOwnerContext GetOwnerContext()
    {
        var userId = User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAuthenticated = !string.IsNullOrWhiteSpace(userId);
        var guestCredential = string.Empty;

        if (!isAuthenticated)
        {
            // Try to read the guest basket cookie for non-authenticated users.
            guestCredential = Request.Cookies[BasketService.GuestCookieNameConst];
        }

        return new BasketOwnerContext(userId, guestCredential, isAuthenticated);
    }
}