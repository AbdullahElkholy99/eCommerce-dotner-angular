using Microsoft.AspNetCore.Antiforgery;

namespace Ecom.API.Middleware;

/// <summary>
/// Enforces antiforgery validation for unsafe requests where browser cookies may authenticate/mutate state.
/// Defense-in-depth alongside existing origin checks. Bearer-only requests without cookie credentials are exempt
/// because CSRF is not applicable when no ambient browser credentials are sent.
/// </summary>
public class CsrfProtectionMiddleware
{
    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "HEAD", "OPTIONS", "TRACE"
    };

    private readonly RequestDelegate _next;
    private readonly IAntiforgery _antiforgery;

    public CsrfProtectionMiddleware(RequestDelegate next, IAntiforgery antiforgery)
    {
        _next = next;
        _antiforgery = antiforgery;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (SafeMethods.Contains(context.Request.Method))
        {
            await _next(context);
            return;
        }

        // Apply to API routes only (avoid interfering with static files/dev tooling if any)
        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (!RequiresAntiforgeryValidation(context.Request))
        {
            await _next(context);
            return;
        }

        try
        {
            await _antiforgery.ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { statusCode = 400, message = "Invalid or missing antiforgery token." });
            return;
        }

        await _next(context);
    }

    private static bool RequiresAntiforgeryValidation(HttpRequest request)
    {
        // Cookie-authenticated contexts: auth JWT cookie and/or guest basket ownership cookie.
        var hasAuthCookie = request.Cookies.ContainsKey("token") && !string.IsNullOrWhiteSpace(request.Cookies["token"]);
        var hasGuestCookie = request.Cookies.ContainsKey("guest_basket_id") && !string.IsNullOrWhiteSpace(request.Cookies["guest_basket_id"]);
        if (hasAuthCookie || hasGuestCookie)
        {
            return true;
        }

        // No ambient cookie credentials present. If client uses Bearer authorization header,
        // CSRF is not applicable (browsers do not attach Authorization headers cross-site automatically).
        if (request.Headers.TryGetValue("Authorization", out var authValues))
        {
            var auth = authValues.ToString();
            if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Default: require antiforgery for unsafe API requests to protect cookie-based flows
        // (including cases where browser may send cookies in some contexts).
        return true;
    }
}
