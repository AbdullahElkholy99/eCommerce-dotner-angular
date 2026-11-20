using Ecom.API.Middleware;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Moq;

namespace Ecom.Tests;

public class CsrfProtectionMiddlewareTests
{
    private static DefaultHttpContext CreateContext(string method, string path = "/api/Baskets/items", string? bearer = null, bool authCookie = false, bool guestCookie = false)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = method;
        ctx.Request.Path = path;
        ctx.Response.Body = new MemoryStream();
        if (!string.IsNullOrWhiteSpace(bearer))
        {
            ctx.Request.Headers.Authorization = $"Bearer {bearer}";
        }
        var cookies = new List<string>();
        if (authCookie) cookies.Add("token=auth-token");
        if (guestCookie) cookies.Add("guest_basket_id=guest-cred");
        if (cookies.Count > 0)
        {
            ctx.Request.Headers.Cookie = string.Join("; ", cookies);
        }
        return ctx;
    }

    private static (CsrfProtectionMiddleware middleware, Mock<IAntiforgery> antiforgery) Build(bool validateThrows = false)
    {
        var antiforgery = new Mock<IAntiforgery>();
        if (validateThrows)
        {
            antiforgery.Setup(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()))
                .ThrowsAsync(new AntiforgeryValidationException("invalid"));
        }
        else
        {
            antiforgery.Setup(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()))
                .Returns(Task.CompletedTask);
        }

        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new CsrfProtectionMiddleware(next, antiforgery.Object);
        return (middleware, antiforgery);
    }

    [Fact]
    public async Task SafeGet_SkipsValidation()
    {
        var ctx = CreateContext("GET", "/api/Baskets", guestCookie: true);
        var antiforgery = new Mock<IAntiforgery>();
        var called = false;
        var mw = new CsrfProtectionMiddleware(_ => { called = true; return Task.CompletedTask; }, antiforgery.Object);

        await mw.InvokeAsync(ctx);

        Assert.True(called);
        antiforgery.Verify(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()), Times.Never);
    }

    [Fact]
    public async Task Unsafe_WithGuestCookie_RequiresValidAntiforgery()
    {
        var ctx = CreateContext("POST", "/api/Baskets/items", guestCookie: true);
        var antiforgery = new Mock<IAntiforgery>();
        antiforgery.Setup(a => a.ValidateRequestAsync(It.IsAny<HttpContext>())).Returns(Task.CompletedTask);
        var called = false;
        var mw = new CsrfProtectionMiddleware(_ => { called = true; return Task.CompletedTask; }, antiforgery.Object);

        await mw.InvokeAsync(ctx);

        Assert.True(called);
        antiforgery.Verify(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()), Times.Once);
    }

    [Fact]
    public async Task Unsafe_WithAuthCookie_RequiresValidAntiforgery()
    {
        var ctx = CreateContext("DELETE", "/api/Baskets", authCookie: true);
        var antiforgery = new Mock<IAntiforgery>();
        antiforgery.Setup(a => a.ValidateRequestAsync(It.IsAny<HttpContext>())).Returns(Task.CompletedTask);
        var called = false;
        var mw = new CsrfProtectionMiddleware(_ => { called = true; return Task.CompletedTask; }, antiforgery.Object);

        await mw.InvokeAsync(ctx);

        Assert.True(called);
        antiforgery.Verify(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()), Times.Once);
    }

    [Fact]
    public async Task Unsafe_MissingOrInvalidToken_ReturnsBadRequest()
    {
        var ctx = CreateContext("POST", "/api/Baskets/items", guestCookie: true);
        var antiforgery = new Mock<IAntiforgery>();
        antiforgery.Setup(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()))
            .ThrowsAsync(new AntiforgeryValidationException("bad token"));
        var called = false;
        var mw = new CsrfProtectionMiddleware(_ => { called = true; return Task.CompletedTask; }, antiforgery.Object);

        await mw.InvokeAsync(ctx);

        Assert.False(called);
        Assert.Equal(StatusCodes.Status400BadRequest, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task Unsafe_BearerOnly_NoCookies_SkipsValidation()
    {
        var ctx = CreateContext("POST", "/api/Baskets/items", bearer: "abc.def.ghi", authCookie: false, guestCookie: false);
        var antiforgery = new Mock<IAntiforgery>();
        var called = false;
        var mw = new CsrfProtectionMiddleware(_ => { called = true; return Task.CompletedTask; }, antiforgery.Object);

        await mw.InvokeAsync(ctx);

        Assert.True(called);
        antiforgery.Verify(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()), Times.Never);
    }

    [Fact]
    public async Task Unsafe_BearerHeader_ButCookiesPresent_RequiresValidation()
    {
        var ctx = CreateContext("POST", "/api/Baskets/items", bearer: "abc.def.ghi", guestCookie: true);
        var antiforgery = new Mock<IAntiforgery>();
        antiforgery.Setup(a => a.ValidateRequestAsync(It.IsAny<HttpContext>())).Returns(Task.CompletedTask);
        var called = false;
        var mw = new CsrfProtectionMiddleware(_ => { called = true; return Task.CompletedTask; }, antiforgery.Object);

        await mw.InvokeAsync(ctx);

        Assert.True(called);
        antiforgery.Verify(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()), Times.Once);
    }

    [Fact]
    public async Task Unsafe_LogoutPost_WithAuthCookie_MissingOrInvalidToken_IsRejected()
    {
        var ctx = CreateContext("POST", "/api/Account/Logout", authCookie: true);
        var antiforgery = new Mock<IAntiforgery>();
        antiforgery.Setup(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()))
            .ThrowsAsync(new AntiforgeryValidationException("missing"));
        var called = false;
        var mw = new CsrfProtectionMiddleware(_ => { called = true; return Task.CompletedTask; }, antiforgery.Object);

        await mw.InvokeAsync(ctx);

        Assert.False(called);
        Assert.Equal(StatusCodes.Status400BadRequest, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task Unsafe_LogoutPost_WithAuthCookie_ValidToken_IsAllowedThroughMiddleware()
    {
        var ctx = CreateContext("POST", "/api/Account/Logout", authCookie: true);
        var antiforgery = new Mock<IAntiforgery>();
        antiforgery.Setup(a => a.ValidateRequestAsync(It.IsAny<HttpContext>())).Returns(Task.CompletedTask);
        var called = false;
        var mw = new CsrfProtectionMiddleware(_ => { called = true; return Task.CompletedTask; }, antiforgery.Object);

        await mw.InvokeAsync(ctx);

        Assert.True(called);
        antiforgery.Verify(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()), Times.Once);
    }

    [Fact]
    public async Task Safe_GetLogout_DoesNotRequireAntiforgery_AndDoesNotTriggerMutatingMiddlewareBlock()
    {
        // GET is safe by method; middleware will not enforce CSRF. Controller-level 405 prevents mutation.
        var ctx = CreateContext("GET", "/api/Account/Logout", authCookie: true);
        var antiforgery = new Mock<IAntiforgery>();
        var called = false;
        var mw = new CsrfProtectionMiddleware(_ => { called = true; return Task.CompletedTask; }, antiforgery.Object);

        await mw.InvokeAsync(ctx);

        Assert.True(called);
        antiforgery.Verify(a => a.ValidateRequestAsync(It.IsAny<HttpContext>()), Times.Never);
    }
}
