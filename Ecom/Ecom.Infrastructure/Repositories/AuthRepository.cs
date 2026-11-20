
using Ecom.Core.DTO.Account;
using Ecom.Core.Entity.Account;
using Ecom.Core.Sharing;
using Microsoft.AspNetCore.Identity;

namespace Ecom.Infrastructure.Repositories;

public class AuthRepository : IAuth
{
    private readonly UserManager<User> _userManager;
    private readonly IEmailService emailService;
    private readonly SignInManager<User> _signInManager;
    private readonly IGenerateToken generateToken;
    private readonly AppDbContext context;
    public AuthRepository(UserManager<User> userManager, IEmailService emailService, SignInManager<User> signInManager, IGenerateToken generateToken, AppDbContext context)
    {
        this._userManager = userManager;
        this.emailService = emailService;
        this._signInManager = signInManager;
        this.generateToken = generateToken;
        this.context = context;
    }

    public async Task<string> RegisterUserAsync(RegisterDTO registerDTO)
    {
        if (registerDTO == null)
        {
            return "Invalid registration request.";
        }

        try
        {
            // Basic email format guard before account creation (in addition to model validation)
            _ = new System.Net.Mail.MailAddress(registerDTO.Email);
        }
        catch
        {
            return "Invalid email address.";
        }

        if (await _userManager.FindByNameAsync(registerDTO.UserName) is not null)
        {
            return "this UserName is already registerd";
        }
        if (await _userManager.FindByEmailAsync(registerDTO.Email) is not null)
        {
            return "this email is already registerd";
        }

        User user = new()
        {
            Email = registerDTO.Email,
            UserName = registerDTO.UserName,
            DisplayName = registerDTO.DisplayName,
        };

        var result = await _userManager.CreateAsync(user, registerDTO.Password);
        if (result.Succeeded is not true)
        {
            return result.Errors.ToList()[0].Description;
        }
        // Send Active Email
        try
        {
            string token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

            await SendEmail(user.Email!, token, "active", "ActiveEmail", "Please active your email, click on button to active");
        }
        catch
        {
            // Account was created but confirmation email could not be delivered.
            // Do not report success and do not expose provider/internal error details.
            return "Registration could not be completed because the confirmation email could not be sent. Please try again later or request account activation.";
        }

        return "done";
    }
    public async Task SendEmail(string email, string code, string component, string subject, string message)
    {
        var result = new EmailDTO(email,
            "abdullah.ali.elkholy@gmail.com",
            subject
            , EmailStringBody.send(email, code, component, message));
        await emailService.SendEmail(result);
    }

    public async Task<string> LoginAsync(LoginDTO login)
    {
        if (login == null)
        {
            return "please check your email and password, something went wrong";
        }
        var finduser = await _userManager.FindByEmailAsync(login.Email);

        if (finduser == null)
        {
            return "please check your email and password, something went wrong";
        }

        if (!finduser.EmailConfirmed)
        {
            try
            {
                string token = await _userManager.GenerateEmailConfirmationTokenAsync(finduser);

                await SendEmail(finduser.Email!, token, "active", "ActiveEmail", "Please active your email, click on button to active");
            }
            catch
            {
                // Do not expose email delivery details; still block unconfirmed login.
            }

            return "Please confirem your email first, we have send activat to your E-mail";
        }

        var result = await _signInManager.CheckPasswordSignInAsync(finduser, login.Password, true);

        if (result.Succeeded)
        {
            return generateToken.GetAndCreateToken(finduser);
        }

        return "please check your email and password, something went wrong";
    }


    public async Task<bool> SendEmailForForgetPassword(string email)
    {
        var findUser = await _userManager.FindByEmailAsync(email);
        if (findUser is null)
        {
            return false;
        }
        try
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(findUser);
            await SendEmail(findUser.Email!, token, "Reset-Password", "Rest pssword", "click on button to Reset your password");

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> ResetPassword(RestPasswordDTO restPassword)
    {
        var findUser = await _userManager.FindByEmailAsync(restPassword.Email);
        if (findUser is null)
        {
            return null;
        }

        var result = await _userManager.ResetPasswordAsync(findUser, restPassword.Token, restPassword.Password);

        if (result.Succeeded)
        {
            return "done";
        }
        return result.Errors.ToList()[0].Description;
    }
    public async Task<bool> ActiveAccount(ActiveAccountDTO accountDTO)
    {
        var findUser = await _userManager.FindByEmailAsync(accountDTO.Email);
        if (findUser is null)
        {
            return false;
        }

        var reslt = await _userManager.ConfirmEmailAsync(findUser, accountDTO.Token);
        if (reslt.Succeeded)
            return true;

        try
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(findUser);

            await SendEmail(findUser.Email!, token, "active", "ActiveEmail", "Please active your email, click on button to active");
        }
        catch
        {
            // Ignore resend failures; activation attempt already failed.
        }

        return false;
    }

    public async Task<bool> UpdateAddress(string email, Address address)
    {
        var findUser = await _userManager.FindByEmailAsync(email);
        if (findUser is null)
        {
            return false;
        }
        var Myaddress = await context.Addresses.AsNoTracking()
            .FirstOrDefaultAsync(m => m.UserId == findUser.Id);

        if (Myaddress is null)
        {
            address.UserId = findUser.Id;
            await context.Addresses.AddAsync(address);
        }
        else
        {
            context.Entry(Myaddress).State = EntityState.Detached;

            address.Id = Myaddress.Id;

            address.UserId = Myaddress.UserId;

            context.Addresses.Update(address);

        }
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<Address> getUserAddress(string email)
    {
        var User = await _userManager.FindByEmailAsync(email);
        if (User is null)
            return null;

        var address = await context.Addresses.FirstOrDefaultAsync(m => m.UserId == User.Id);
        if (address is null)
            return null;

        return address;
    }
}
