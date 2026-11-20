using Ecom.Core.Entity;
using Ecom.Core.Entity.Order;


namespace Ecom.Core.Services;

public interface IPaymentService
{
    Task<CustomerBasket> CreateOrUpdatePaymentAsync(string basketId, int? deliveryId);
    Task<Orders> UpdateOrderSuccess(string PaymentInten);
    Task<Orders> UpdateOrderFaild(string PaymentInten);
}