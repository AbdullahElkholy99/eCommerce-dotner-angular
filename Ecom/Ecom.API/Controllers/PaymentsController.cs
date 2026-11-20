using Ecom.Core.Entity;
using Ecom.Core.Entity.Order;
using Ecom.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Stripe;

namespace Ecom.API.Controllers;

[Route("api/[controller]")]
[ApiController]

public class PaymentsController : ControllerBase
{
    private readonly IPaymentService paymentService;
    public PaymentsController(IPaymentService paymentService)
    {
        this.paymentService = paymentService;
    }

    [Authorize]
    [HttpPost("Create")]
    public async Task<ActionResult<CustomerBasket>> create(string basketId, int? deliveryId)
    {
        return await paymentService.CreateOrUpdatePaymentAsync(basketId, deliveryId);
    }

    //[HttpPost("webhook")]
    //public async Task<IActionResult> UpdateStatusWithStripe()
    //{
    //    var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
    //    try
    //    {
    //        var stripeEvent = EventUtility.ConstructEvent(json,
    //            Request.Headers["Stripe-Signature"], endpointSecret, throwOnApiVersionMismatch: false);
    //        PaymentIntent intent;
    //        Orders orders;
    //        // Handle the event
    //        if (stripeEvent.Type == Events.PaymentIntentPaymentFailed)
    //        {
    //            intent = stripeEvent.Data.Object as PaymentIntent;
    //            orders = await paymentService.UpdateOrderFaild(intent.Id);
    //        }
    //        else if (stripeEvent.Type == Events.PaymentIntentSucceeded)
    //        {
    //            intent = stripeEvent.Data.Object as PaymentIntent;
    //            orders = await paymentService.UpdateOrderSuccess(intent.Id);
    //        }
    //        // ... handle other event types
    //        else
    //        {
    //            Console.WriteLine("Unhandled event type: {0}", stripeEvent.Type);
    //        }

    //        return Ok();
    //    }
    //    catch (StripeException e)
    //    {
    //        return BadRequest();
    //    }
    //}
}