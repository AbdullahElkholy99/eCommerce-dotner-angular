import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { OrderItem } from './order-item/order-item';
import { Order } from './order/order';

const routes: Routes = [
  { path: '', component: Order },
  { path: 'item', component: OrderItem },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class OrdersRoutingModule { }
