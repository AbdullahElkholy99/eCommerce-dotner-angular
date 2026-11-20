using System.Security.Claims;
using AutoMapper;
using Ecom.API.Controllers;
using Ecom.API.Helper;
using Ecom.Core.DTO.Account;
using Ecom.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Ecom.Tests;

public class AuthenticationRegressionTests
{
    [Fact]
    public async Task Login_UnknownEmail_ReturnsBadRequestGenericMessage()
    {
        var auth = new Mock<IAuth>();
        auth.Setup(a => a.LoginAsync(It.IsAny<LoginDTO>()))
            .ReturnsAsync("please check your email and password, something went wrong");

        var controller = CreateController(auth.Object);

        var result = await controller.login(new LoginDTO { Email = "unknown@example.test", Password = "Wrong!1234a" });

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        var body = Assert.IsType<ResponseAPI>(bad.Value);
        Assert.Equal(400, body.StatusCode);
        Assert.Equal("please check your email and password, something went wrong", body.Message);
    }

    [Fact]
    public async Task Login_UnconfirmedAccount_ReturnsBadRequest_NotSuccess()
    {
        var auth = new Mock<IAuth>();
        auth.Setup(a => a.LoginAsync(It.IsAny<LoginDTO>()))
            .ReturnsAsync("Please confirem your email first, we have send activat to your E-mail");

        var controller = CreateController(auth.Object);

        var result = await controller.login(new LoginDTO { Email = "user@example.test", Password = "Test!1234a" });

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        var body = Assert.IsType<ResponseAPI>(bad.Value);
        Assert.Equal(400, body.StatusCode);
        Assert.Contains("Please", body.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", controller.Response.Headers.SetCookie.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_Success_SetsCookieAndReturnsOk()
    {
        var auth = new Mock<IAuth>();
        auth.Setup(a => a.LoginAsync(It.IsAny<LoginDTO>()))
            .ReturnsAsync("header.payload.signature");

        var controller = CreateController(auth.Object);

        var result = await controller.login(new LoginDTO { Email = "user@example.test", Password = "Test!1234a" });

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ResponseAPI>(ok.Value);
        Assert.Equal(200, body.StatusCode);
        Assert.Contains("token=", controller.Response.Headers.SetCookie.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ActiveAccount_Failure_ReturnsBadRequest400Body()
    {
        var auth = new Mock<IAuth>();
        auth.Setup(a => a.ActiveAccount(It.IsAny<ActiveAccountDTO>())).ReturnsAsync(false);
        var controller = CreateController(auth.Object);

        var result = await controller.Active(new ActiveAccountDTO { Email = "user@example.test", Token = "bad" });

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var body = Assert.IsType<ResponseAPI>(bad.Value);
        Assert.Equal(400, body.StatusCode);
    }

    [Fact]
    public async Task ForgetPassword_UnknownEmail_ReturnsBadRequest400Body()
    {
        var auth = new Mock<IAuth>();
        auth.Setup(a => a.SendEmailForForgetPassword(It.IsAny<string>())).ReturnsAsync(false);
        var controller = CreateController(auth.Object);

        var result = await controller.forget("unknown@example.test");

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        var body = Assert.IsType<ResponseAPI>(bad.Value);
        Assert.Equal(400, body.StatusCode);
    }

    [Fact]
    public void RegisterDto_InvalidEmail_FailsValidationAttribute()
    {
        var dto = new RegisterDTO
        {
            Email = "not-an-email",
            Password = "Test!1234a",
            UserName = "user1",
            DisplayName = "User One"
        };

        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var context = new System.ComponentModel.DataAnnotations.ValidationContext(dto);
        var valid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(dto, context, results, true);

        Assert.False(valid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(RegisterDTO.Email)));
    }

    [Fact]
    public void Logout_Get_Returns405_AndDoesNotClearAuthCookie()
    {
        var (controller, redisDb) = CreateControllerWithRedis(new Mock<IAuth>().Object);
        controller.ControllerContext.HttpContext.Request.Headers.Cookie = "token=existing-token";

        var result = controller.logoutGetNotAllowed();

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status405MethodNotAllowed, status.StatusCode);
        var body = Assert.IsType<ResponseAPI>(status.Value);
        Assert.Equal(405, body.StatusCode);
        // No mutating cookie clear on GET rejection
        Assert.DoesNotContain("token=", controller.Response.Headers.SetCookie.ToString(), StringComparison.OrdinalIgnoreCase);
        redisDb.Verify(db => db.StringSetAsync(It.IsAny<StackExchange.Redis.RedisKey>(), It.IsAny<StackExchange.Redis.RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<StackExchange.Redis.When>(), It.IsAny<StackExchange.Redis.CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task Logout_Post_ClearsCookie_ReturnsOk_AndRevokesTokenJtiBestEffort()
    {
        var jti = Guid.NewGuid().ToString("N");
        var token = CreateJwtWithJti(jti, DateTime.UtcNow.AddMinutes(30));
        var (controller, redisDb) = CreateControllerWithRedis(new Mock<IAuth>().Object);
        controller.ControllerContext.HttpContext.Request.Headers.Cookie = $"token={token}";

        var result = await controller.logout();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ResponseAPI>(ok.Value);
        Assert.Equal(200, body.StatusCode);
        var setCookie = controller.Response.Headers.SetCookie.ToString();
        Assert.Contains("token=", setCookie, StringComparison.OrdinalIgnoreCase);
        // Expired/cleared cookie indicator (implementation sets past Expires)
        Assert.Contains("expires=", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(redisDb.Invocations, i =>
            i.Method.Name == nameof(StackExchange.Redis.IDatabaseAsync.StringSetAsync) &&
            i.Arguments.Count > 0 &&
            i.Arguments[0]?.ToString() == $"auth:revoked:jti:{jti}");
    }

    [Fact]
    public async Task Logout_Post_WithoutTokenCookie_StillClearsCookieAndReturnsOk()
    {
        var (controller, redisDb) = CreateControllerWithRedis(new Mock<IAuth>().Object);

        var result = await controller.logout();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ResponseAPI>(ok.Value);
        Assert.Equal(200, body.StatusCode);
        Assert.Contains("token=", controller.Response.Headers.SetCookie.ToString(), StringComparison.OrdinalIgnoreCase);
        redisDb.Verify(db => db.StringSetAsync(It.IsAny<StackExchange.Redis.RedisKey>(), It.IsAny<StackExchange.Redis.RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<StackExchange.Redis.When>(), It.IsAny<StackExchange.Redis.CommandFlags>()), Times.Never);
    }

    private static (AccountController controller, Mock<StackExchange.Redis.IDatabase> redisDb) CreateControllerWithRedis(IAuth auth)
    {
        var uow = new Mock<IUnitOfWork>();
        uow.SetupGet(x => x.Auth).Returns(auth);
        var mapper = new Mock<IMapper>();
        var redisDb = new Mock<StackExchange.Redis.IDatabase>();
        var redis = new Mock<StackExchange.Redis.IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(redisDb.Object);

        var controller = new AccountController(uow.Object, mapper.Object, redis.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        return (controller, redisDb);
    }

    private static string CreateJwtWithJti(string jti, DateTime expiresUtc)
    {
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            claims: new[]
            {
                new System.Security.Claims.Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti, jti)
            },
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: expiresUtc);
        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }

    private static AccountController CreateController(IAuth auth)
    {
        var uow = new Mock<IUnitOfWork>();
        uow.SetupGet(x => x.Auth).Returns(auth);
        var mapper = new Mock<IMapper>();
        var redis = new Mock<StackExchange.Redis.IConnectionMultiplexer>();

        var controller = new AccountController(uow.Object, mapper.Object, redis.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        // Minimal identity principal for endpoints that might inspect it
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        return controller;
    }
}