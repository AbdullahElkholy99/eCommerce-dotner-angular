using Ecom.Core.Entity;


namespace Ecom.Core.Interfaces;

public interface ICustomerBasketRepository
{
    // ---------------- Read Operations ----------------
    Task<CustomerBasket> GetBasketAsync(string basketId);

    // ---------------- Update Operations ----------------
    Task<CustomerBasket> UpdateBasketAsync(CustomerBasket basket);

    // ---------------- Delete Operations ----------------
    Task<bool> DeleteBasketAsync(string basketId);

}
