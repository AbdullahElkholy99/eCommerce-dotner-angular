using Ecom.Core.DTO.Account;
using Ecom.Core.Entity.Account;


namespace Ecom.Core.Interfaces;

public interface IAuth
{
    Task<string> RegisterUserAsync(RegisterDTO registerDto);
    Task<string> LoginAsync(LoginDTO loginDTO);


    Task<bool> SendEmailForForgetPassword(string email);
    Task<string> ResetPassword(RestPasswordDTO restPassword);
    Task<bool> ActiveAccount(ActiveAccountDTO accountDTO);
    Task<bool> UpdateAddress(string email, Address address);
    Task<Address> getUserAddress(string email);
}
