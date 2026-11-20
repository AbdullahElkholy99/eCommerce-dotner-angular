using Ecom.Core.Entity.Account;
using Ecom.Core.Entity.Order;

namespace Ecom.API.Mapping;

public class OrderMapping : Profile
{
    public OrderMapping()
    {
        CreateMap<Orders, OrderToReturnDTO>()
            .ForMember(d => d.deliveryMethod,
            o => o.MapFrom(s => s.deliveryMethod.Name))
            .ReverseMap();

        CreateMap<OrderItem, OrderItemDTO>().ReverseMap();
        CreateMap<ShippingAddress, ShipAddressDTO>().ReverseMap();
        CreateMap<Address, ShipAddressDTO>().ReverseMap();
    }
}