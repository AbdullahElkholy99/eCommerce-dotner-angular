import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../environments/environment.development';
import { DeliveryModel } from '../shared/Models/DeliveryModel';
import { ICreateOrder, IOrder } from '../shared/Models/Order';

@Injectable({
  providedIn: 'root'
})
export class CheckoutService {
  baseURL = environment.baseURL;

  constructor(private http: HttpClient) {}

  // Address
  updateAddress(form: any) {
    return this.http.put(this.baseURL + 'Account/update-address', form);
  }

  getAddress() {
    return this.http.get(this.baseURL + 'Account/get-address-for-user');
  }

 // Delivery
  getDeliveryMethod() {
    return this.http.get<DeliveryModel[]>(this.baseURL + 'Orders/get-delivery');
  }

  // Order
  CreateOrder(order: ICreateOrder) {
    return this.http.post<IOrder>(this.baseURL + 'Orders/create-order', order);
  }
  
}
