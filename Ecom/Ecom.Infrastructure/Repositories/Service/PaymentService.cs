using Ecom.Core.Entity;
using Ecom.Core.Entity.Order;
using Stripe;

namespace Ecom.Infrastructure.Repositories.Service;

internal class PaymentService : IPaymentService
{
    private readonly IUnitOfWork work;
    private readonly IConfiguration configuration;
    private readonly AppDbContext _context;
    public PaymentService(IUnitOfWork work, IConfiguration configuration, AppDbContext context)
    {
        this.work = work;
        this.configuration = configuration;
        _context = context;
    }
    public async Task<CustomerBasket> CreateOrUpdatePaymentAsync(string basketId, int? delivertMethodId)
    {
        CustomerBasket basket =
            await work.CustomerBasketRepository.GetBasketAsync(basketId);

        StripeConfiguration.ApiKey = configuration["StripSetting:secretKey"];

        decimal shippingPrice = 0m;

        if (delivertMethodId.HasValue)
        {
            var delivery = await _context.DeliveryMethods.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == delivertMethodId.Value);

            shippingPrice = delivery.Price;
        }

        //check product price is the same in product price at database
        foreach (var item in basket.BasketItems)
        {
            var product = await work.ProductRepository.GetByIdAsync(item.Id);

            item.Price = product.NewPrice;
        }

        PaymentIntentService paymentIntentService = new();

        PaymentIntent _intent;

        if (string.IsNullOrEmpty(basket.PaymentIntentId))
        {
            var option = new PaymentIntentCreateOptions
            {
                Amount = (long)basket.BasketItems
                .Sum(m => m.Qunatity * (m.Price * 100))
                   + (long)(shippingPrice * 100),
                
                Currency = "USD",
                PaymentMethodTypes = new List<string> { "card" }
            };
            _intent = await paymentIntentService.CreateAsync(option);

            basket.PaymentIntentId = _intent.Id;

            basket.ClientSecret = _intent.ClientSecret;
        }
        else
        {
            var option = new PaymentIntentUpdateOptions
            {
                Amount = (long)basket.BasketItems
                .Sum(m => m.Qunatity * (m.Price * 100)) + (long)(shippingPrice * 100),

            };
            await paymentIntentService.UpdateAsync(basket.PaymentIntentId, option);
        }
        await work.CustomerBasketRepository.UpdateBasketAsync(basket);
        return basket;
    }

    public async Task<Orders> UpdateOrderFaild(string PaymentInten)
    {
        var order = await _context.Orders
            .FirstOrDefaultAsync(m => m.PaymentIntentId == PaymentInten);
        if (order is null)
        {
            return null;
        }
        order.status = Status.PaymentFaild;
        _context.Orders.Update(order);
        await _context.SaveChangesAsync();
        return order;
    }

    public async Task<Orders> UpdateOrderSuccess(string PaymentInten)
    {
        var order = await _context
            .Orders.FirstOrDefaultAsync(m => m.PaymentIntentId == PaymentInten);
        if (order is null)
        {
            return null;
        }
        order.status = Status.PaymentReceived;
        _context.Orders.Update(order);
        await _context.SaveChangesAsync();
        return order;
    }
}