import { Component, Input, OnInit } from '@angular/core';
import { FormGroup } from '@angular/forms';
import { CheckoutService } from '../checkout-service';
import { DeliveryModel } from '../../shared/Models/DeliveryModel';
import { BasketService } from '../../basket/basket-service';
import { ToastrService } from 'ngx-toastr';
import { loadStripe } from '@stripe/stripe-js';

@Component({
  selector: 'app-delivery',
  standalone: false,
  templateUrl: './delivery.html',
  styleUrl: './delivery.scss'
})
export class Delivery implements OnInit {

  @Input() delivery: FormGroup;

  deliveries: DeliveryModel[] = [];

  constructor(
    private _service: CheckoutService,
    private basketService: BasketService,
    private toast: ToastrService
  ) {}

  ngOnInit(): void {
    this._service.getDeliveryMethod().subscribe({
      next: (value) => {
        this.deliveries = value;
      },
      error(err) {
        console.log(err);
      },
    });
  }

  // CreatePayment() {
  //   const id = this.deliveries.find(
  //     (m) => m.id == this.delivery.value.delivery
  //   ).id;
    
  //   this.basketService.CreatePaymentIntent(id).subscribe({
  //     next: (res) => {
  //       this.toast.success('Payment Intent Created', 'SUCCESS');
  //     },
  //     error(err) {
  //       console.log(err);
        
  //     },
  //   });
  // }
cardElement: any;
stripe: any;

async ngAfterViewInit() {
  this.stripe = await loadStripe('pk_test_51RZMsJE4FEMex1k36xp6CtQ6a08pbDluQO7yEaFv7dUCUCK9DQJqHpRCcmdIpvD9AIPdRMCzfjhLR2pB6aIbd8qL008x7crYDt'); // نفس المفتاح
  const elements = this.stripe.elements();
  this.cardElement = elements.create('card');
  this.cardElement.mount('#card-element');
}

  async CreatePayment() {
  const id = this.deliveries.find(
    (m) => m.id == this.delivery.value.delivery
  ).id;

  this.basketService.CreatePaymentIntent(id).subscribe({
    next: async (res: any) => {
      this.toast.success('Payment Intent Created', 'SUCCESS');

      // جِيب الـ clientSecret من الـAPI
      const clientSecret = res.clientSecret;

      // حمّل Stripe
      const stripe = await loadStripe('pk_test_51RZMsJE4FEMex1k36xp6CtQ6a08pbDluQO7yEaFv7dUCUCK9DQJqHpRCcmdIpvD9AIPdRMCzfjhLR2pB6aIbd8qL008x7crYDt'); // حط الـPublishableKey

      // نفّذ تأكيد الدفع
      const { error, paymentIntent } = await stripe!.confirmCardPayment(clientSecret, {
        payment_method: {
          card: this.cardElement, // هنعرفها تحت
        }
      });

      if (error) {
        this.toast.error(error.message, 'Payment Failed');
        console.log(error);
      } else if (paymentIntent && paymentIntent.status === 'succeeded') {
        this.toast.success('Payment Succeeded!', 'SUCCESS');
        console.log(paymentIntent);
        // هنا ممكن تبعت API علشان تعمل UpdateOrderSuccess
      }
    },
    error: (err) => {
      console.log(err);
    },
  });
}

  
  SetShipptingPrice() {
    const delivery = this.deliveries.find(
      (m) => m.id == this.delivery.value.delivery
    );
    this.basketService.SetShippingPrice(delivery);
  }
}
