namespace Ecom.Core.DTO.Basket;

public class AddBasketItemRequest
{
    public int ProductId { get; set; }
    public int Quantity { get; set; } = 1;
}

public class UpdateBasketItemRequest
{
    public int Quantity { get; set; }
}
