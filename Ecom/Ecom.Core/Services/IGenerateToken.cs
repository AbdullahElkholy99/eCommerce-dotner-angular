using Ecom.Core.Entity.Account;

namespace Ecom.Core.Services;
public interface IGenerateToken
{
    string GetAndCreateToken(User user);
}
