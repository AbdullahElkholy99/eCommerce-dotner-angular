namespace Ecom.Core.DTO.Basket;

public class BasketItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public string Category { get; set; } = string.Empty;
}

public class CustomerBasketDto
{
    public string? Id { get; set; }
    public List<BasketItemDto> BasketItems { get; set; } = new();
}
