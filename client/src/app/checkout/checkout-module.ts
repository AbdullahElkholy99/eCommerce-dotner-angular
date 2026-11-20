import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';

import { CheckoutRoutingModule } from './checkout-routing-module';
import { Checkout } from './checkout/checkout';
import { SharedModule } from '../shared/shared-module';
import { Stepper } from './stepper/stepper';
import {FormBuilder, Validators, FormsModule, ReactiveFormsModule} from '@angular/forms';
import {MatInputModule} from '@angular/material/input';
import {MatFormFieldModule} from '@angular/material/form-field';
import {MatStepperModule} from '@angular/material/stepper';
import {MatButtonModule} from '@angular/material/button';
import { Address } from './address/address';
import { Delivery } from './delivery/delivery';
import { Payment } from './payment/payment';
import { Success } from './success/success';
import {MatRadioModule} from '@angular/material/radio';
import { loadStripe } from '@stripe/stripe-js';

const stripePromise = loadStripe('pk_test_51RZMsJE4FEMex1k36xp6CtQ6a08pbDluQO7yEaFv7dUCUCK9DQJqHpRCcmdIpvD9AIPdRMCzfjhLR2pB6aIbd8qL008x7crYDt');


@NgModule({
  declarations: [
    Checkout,
    Stepper,
    Address,
    Delivery,
    Payment,
    Success,
  ],
  imports: [
    CommonModule,
    CheckoutRoutingModule,
    SharedModule,
    MatButtonModule,
    MatStepperModule,
    FormsModule,
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatRadioModule
  ],
  exports:[
   Stepper,
   Address,
   Delivery,
   Payment
  ]   
})

export class CheckoutModule { }
