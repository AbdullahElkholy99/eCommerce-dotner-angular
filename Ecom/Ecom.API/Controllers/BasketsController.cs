using System.Security.Claims;
using Ecom.Core.DTO.Basket;
using Ecom.Core.Entity;
using Ecom.Core.Interfaces;
using Ecom.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecom.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BasketsController : BaseController
{
    private readonly IBasketService _basketService;

    public BasketsController(IUnitOfWork unitOfWork, IMapper mapper, IBasketService basketService) : base(unitOfWork, mapper)
    {
        _basketService = basketService;
    }

    [HttpGet]
    public async Task<IActionResult> GetCurrentBasket()
    {
        var owner = GetOwnerContext();
        var result = await _basketService.GetBasketAsync(owner);
        return ApplyResult(result);
    }

    // Legacy route: secure behavior (ignores client-supplied basket id, resolves ownership server-side)
    [HttpGet("get-basket-item/{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var owner = GetOwnerContext();
        var result = await _basketService.GetBasketAsync(owner);
        return ApplyResult(result);
    }

    [HttpPost("items")]
    public async Task<IActionResult> AddItem([FromBody] AddBasketItemRequest request)
    {
        if (!IsSafeStateChangingRequest()) return BadRequest(new ResponseAPI(400, "Invalid request."));
        if (request is null) return BadRequest(new ResponseAPI(400, "Invalid request."));
        var owner = GetOwnerContext();
        var result = await _basketService.AddItemAsync(owner, request.ProductId, request.Quantity);
        return ApplyResult(result);
    }

    [HttpPut("items/{productId:int}")]
    public async Task<IActionResult> UpdateItemQuantity(int productId, [FromBody] UpdateBasketItemRequest request)
    {
        if (!IsSafeStateChangingRequest()) return BadRequest(new ResponseAPI(400, "Invalid request."));
        if (request is null) return BadRequest(new ResponseAPI(400, "Invalid request."));
        var owner = GetOwnerContext();
        var result = await _basketService.UpdateItemQuantityAsync(owner, productId, request.Quantity);
        return ApplyResult(result);
    }

    [HttpDelete("items/{productId:int}")]
    public async Task<IActionResult> RemoveItem(int productId)
    {
        if (!IsSafeStateChangingRequest()) return BadRequest(new ResponseAPI(400, "Invalid request."));
        var owner = GetOwnerContext();
        var result = await _basketService.RemoveItemAsync(owner, productId);
        return ApplyResult(result);
    }

    [HttpDelete]
    public async Task<IActionResult> ClearBasket()
    {
        if (!IsSafeStateChangingRequest()) return BadRequest(new ResponseAPI(400, "Invalid request."));
        var owner = GetOwnerContext();
        var result = await _basketService.ClearBasketAsync(owner);
        return ApplyResult(result);
    }

    // Legacy route: secure behavior (ignores client-supplied basket id, clears current resolved basket)
    [HttpDelete("delete-basket-item/{id}")]
    public async Task<IActionResult> DeleteBasket(string id)
    {
        if (!IsSafeStateChangingRequest()) return BadRequest(new ResponseAPI(400, "Invalid request."));
        var owner = GetOwnerContext();
        var result = await _basketService.ClearBasketAsync(owner);
        return ApplyResult(result);
    }

    // Legacy route: secure behavior (ignores client-supplied basket id/prices, rebuilds from product IDs/quantities)
    [HttpPost("update-basket")]
    public async Task<IActionResult> UpdateBasket([FromBody] LegacyBasketUpdateRequest? basket)
    {
        if (!IsSafeStateChangingRequest()) return BadRequest(new ResponseAPI(400, "Invalid request."));
        var owner = GetOwnerContext();
        var items = (basket?.BasketItems ?? new List<LegacyBasketItemRequest>())
            .Select(i => (i.Id, i.Qunatity))
            .ToList();
        var result = await _basketService.ReplaceItemsAsync(owner, items);
        return ApplyResult(result);
    }

    [Authorize]
    [HttpPost("merge")]
    public async Task<IActionResult> MergeGuestBasket()
    {
        if (!IsSafeStateChangingRequest()) return BadRequest(new ResponseAPI(400, "Invalid request."));
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(new ResponseAPI(401, "Unauthorized"));
        }
        var guestCredential = Request.Cookies[BasketService.GuestCookieNameConst];
        var result = await _basketService.MergeGuestBasketAsync(userId, guestCredential);
        return ApplyResult(result);
    }

    private BasketOwnerContext GetOwnerContext()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAuthenticated = !string.IsNullOrWhiteSpace(userId);
        var guestCredential = Request.Cookies[BasketService.GuestCookieNameConst];
        return new BasketOwnerContext(userId, guestCredential, isAuthenticated);
    }

    private IActionResult ApplyResult(BasketOperationResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.NewGuestCredential))
        {
            Response.Cookies.Append(BasketService.GuestCookieNameConst, result.NewGuestCredential, BuildGuestCookieOptions());
        }
        if (result.ClearGuestCookie)
        {
            Response.Cookies.Delete(BasketService.GuestCookieNameConst, BuildGuestCookieOptions(expired: true));
        }

        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new ResponseAPI(result.StatusCode, result.Message ?? "Request failed."));
        }

        return Ok(result.Basket);
    }

    private static CookieOptions BuildGuestCookieOptions(bool expired = false)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            IsEssential = true,
            Path = "/",
            Expires = expired ? DateTimeOffset.UtcNow.AddDays(-1) : DateTimeOffset.UtcNow.Add(BasketService.GetBasketTtl())
        };
    }

    private bool IsSafeStateChangingRequest()
    {
        // Basic CSRF hardening for cookie-authenticated/credentialed requests: require same-origin Origin/Referer
        // for unsafe methods. This is defense-in-depth (app also uses credentialed CORS); not a full antiforgery token system.
        var method = Request.Method?.ToUpperInvariant();
        if (method is "GET" or "HEAD" or "OPTIONS" or "TRACE") return true;

        var allowedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "http://localhost:4200",
            "http://localhost:52945",
            "https://localhost:4200",
            "https://localhost:52945",
            "https://e-commerce-platform-one-lyart.vercel.app"
        };

        var origin = Request.Headers.Origin.ToString();
        if (!string.IsNullOrWhiteSpace(origin))
        {
            return allowedOrigins.Contains(origin);
        }

        var referer = Request.Headers.Referer.ToString();
        if (!string.IsNullOrWhiteSpace(referer) && Uri.TryCreate(referer, UriKind.Absolute, out var refererUri))
        {
            var refererOrigin = $"{refererUri.Scheme}://{refererUri.Authority}";
            return allowedOrigins.Contains(refererOrigin);
        }

        // If no origin/referer provided (non-browser or unusual client), reject state-changing requests to reduce CSRF risk.
        return false;
    }
}

// Legacy request DTOs for backward compatibility with existing clients; server ignores ownership/prices and validates by product ID.
public class LegacyBasketUpdateRequest
{
    public string? Id { get; set; }
    public string? PaymentIntentId { get; set; }
    public string? ClientSecret { get; set; }
    public List<LegacyBasketItemRequest> BasketItems { get; set; } = new();
}

public class LegacyBasketItemRequest
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Image { get; set; }
    public int Qunatity { get; set; }
    public decimal Price { get; set; }
    public string? Category { get; set; }
}
