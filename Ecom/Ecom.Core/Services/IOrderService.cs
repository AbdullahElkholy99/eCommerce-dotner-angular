using Ecom.Core.DTO;
using Ecom.Core.Entity.Order;

namespace Ecom.Core.Services;
public interface IOrderService
{
    // --------------------------- Create Operations : ---------------------------
    Task<Orders> CreateOrdersAsync(OrderDTO orderDTO, string BuyerEmail);

    // --------------------------- Read Operations : ---------------------------
    Task<IReadOnlyList<OrderToReturnDTO>> GetAllOrdersForUserAsync(string BuyerEmail);
    Task<OrderToReturnDTO> GetOrderByIdAsync(int Id, string BuyerEmail);
    Task<IReadOnlyList<DeliveryMethod>> GetDeliveryMethodAsync();




}