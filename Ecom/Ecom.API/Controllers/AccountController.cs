using Ecom.Core.DTO.Account;
using Ecom.Core.Entity.Account;
using Microsoft.AspNetCore.Authorization;
using StackExchange.Redis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Ecom.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AccountController : BaseController
{
    private readonly IConnectionMultiplexer _redis;
    public AccountController(IUnitOfWork work, IMapper mapper, IConnectionMultiplexer redis) : base(work, mapper)
    {
        _redis = redis;
    }

    [HttpGet("get-address-for-user")]
    public async Task<IActionResult> getAddress()
    {
        var address = await _unitOfWork.Auth.getUserAddress(User.FindFirst(ClaimTypes.Email).Value);
        var result = _mapper.Map<ShipAddressDTO>(address);
        return Ok(result);
    }

    [HttpPost("Logout")]
    public async Task<IActionResult> logout()
    {
        return await PerformLogoutAsync();
    }

    // Explicitly non-mutating: GET must not trigger logout (breaking change from prior GET-based logout; documented).
    [HttpGet("Logout")]
    public IActionResult logoutGetNotAllowed()
    {
        return StatusCode(StatusCodes.Status405MethodNotAllowed, new ResponseAPI(405, "Use POST /api/Account/Logout."));
    }

    private async Task<IActionResult> PerformLogoutAsync()
    {
        var token = Request.Cookies["token"];
        if (!string.IsNullOrWhiteSpace(token))
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(token);
                var jti = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
                if (!string.IsNullOrWhiteSpace(jti))
                {
                    var ttl = jwt.ValidTo - DateTime.UtcNow;
                    if (ttl <= TimeSpan.Zero)
                    {
                        ttl = TimeSpan.FromMinutes(1);
                    }

                    var db = _redis.GetDatabase();
                    await db.StringSetAsync($"auth:revoked:jti:{jti}", "1", ttl);
                }
            }
            catch
            {
                // Best-effort revocation; still clear cookie.
            }
        }

        Response.Cookies.Append("token", "", new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            IsEssential = true,
            Expires = DateTime.Now.AddDays(-1)
        });

        return Ok(new ResponseAPI(200));
    }

    [Authorize]
    [HttpGet("get-user-name")]
    public IActionResult GetUserName()
    {
        return Ok(new ResponseAPI(200, User.Identity.Name));
    }


    [HttpGet("IsUserAuth")]
    public async Task<IActionResult> IsUserAuth()
    {

        return User.Identity.IsAuthenticated ? Ok() : BadRequest();
    }

    [Authorize]
    [HttpPut("update-address")]
    public async Task<IActionResult> updateAddress(ShipAddressDTO addressDTO)
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        var address = _mapper.Map<Address>(addressDTO);
        var result = await _unitOfWork.Auth.UpdateAddress(email, address);
        return result ? Ok() : BadRequest();
    }

    [HttpPost("Register")]
    public async Task<ActionResult<RegisterDTO>> register(RegisterDTO registerDTO)
    {
        string result = await _unitOfWork.Auth.RegisterUserAsync(registerDTO);
        if (result != "done")
        {
            return BadRequest(new ResponseAPI(400, result));
        }
        return Ok(new ResponseAPI(200, result));
    }

    [HttpPost("Login")]
    public async Task<IActionResult> login(LoginDTO loginDTO)
    {


        string result = await _unitOfWork.Auth.LoginAsync(loginDTO);

        if (string.IsNullOrWhiteSpace(result) ||
            result.StartsWith("please", StringComparison.OrdinalIgnoreCase) ||
            result.StartsWith("Please", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ResponseAPI(400, result ?? "Bad Request"));
        }

        Response.Cookies.Append("token", result, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            IsEssential = true,
            Expires = DateTime.Now.AddDays(1)
        });

        return Ok(new ResponseAPI(200));
    }

    [HttpPost("active-account")]
    public async Task<ActionResult<ActiveAccountDTO>> Active(ActiveAccountDTO accountDTO)
    {
        var result = await _unitOfWork.Auth.ActiveAccount(accountDTO);

        return result ?
            Ok(new ResponseAPI(200)) : BadRequest(new ResponseAPI(400));
    }


    [HttpGet("send-email-forget-password")]
    public async Task<IActionResult> forget(string email)
    {
        var result = await _unitOfWork.Auth.SendEmailForForgetPassword(email);

        return result ?
            Ok(new ResponseAPI(200)) : BadRequest(new ResponseAPI(400));
    }


    [HttpPost("reset-password")]
    public async Task<IActionResult> reset(RestPasswordDTO restPasswordDTO)
    {
        var result = await _unitOfWork.Auth.ResetPassword(restPasswordDTO);
        
        if (result == "done")
        {
            return Ok(new ResponseAPI(200));
        }

        return BadRequest(new ResponseAPI(400));
    }
}