
namespace Ecom.Core.Entity;

public class CustomerBasket
{
    public CustomerBasket()
    {
        
    }
    public CustomerBasket(string id )
    {
        Id = id;
    }
    public string Id { get; set; }
    public string PaymentIntentId { get; set; }
    public string ClientSecret { get; set; }
    public List<BasketItem> BasketItems { get; set; } = new List<BasketItem>();

}
