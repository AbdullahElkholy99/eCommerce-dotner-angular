import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, map, of, tap, catchError, throwError } from 'rxjs';
import { BasketModel, IBasket, IBasketItem, IBasketTotal } from '../shared/Models/Basket';
import { IProduct } from '../shared/Models/Product';
import { DeliveryModel } from '../shared/Models/DeliveryModel';
import { environment } from '../../environments/environment.development';

@Injectable({
  providedIn: 'root'
})
export class BasketService {
  constructor(private http: HttpClient) {}
  BaseURL = environment.baseURL;

  private basketSource = new BehaviorSubject<IBasket | null>(null);
  basket$ = this.basketSource.asObservable();

  private basketSourceTotal = new BehaviorSubject<IBasketTotal | null>(null);
  basketTotal$ = this.basketSourceTotal.asObservable();
  shipPrice = 0;

  // Checkout/payment-related methods retained for compatibility (out of scope for basket phase)
  CreatePaymentIntent(deliveryMethodId: number = 3) {
    const basket = this.GetCurrentValue();
    return this.http
      .post(
        this.BaseURL + `Payments/Create?basketId=${basket?.id ?? ''}&deliveryId=${deliveryMethodId}`,
        {}
      )
      .pipe(
        map((value: any) => {
          // keep current basket state; payment flow may return basket/payment data
          if (value && value.basketItems) {
            this.setBasketState(this.normalizeBasket(value));
          }
          return value;
        })
      );
  }

  SetShippingPrice(delivery: DeliveryModel) {
    this.shipPrice = delivery.price;
    this.calculateTotal();
    return this.CreatePaymentIntent(delivery.id).subscribe();
  }

  GetCurrentValue(): IBasket | null {
    return this.basketSource.value;
  }

  loadCurrentBasket(): Observable<IBasket | null> {
    return this.http.get<any>(this.BaseURL + 'Baskets').pipe(
      map((value) => this.normalizeBasket(value)),
      tap((basket) => this.setBasketState(basket)),
      catchError((err) => {
        console.log(err);
        return throwError(() => err);
      })
    );
  }

  // Legacy-compatible signature; id is ignored because server resolves ownership.
  GetBasket(_id?: string): Observable<IBasket | null> {
    return this.loadCurrentBasket();
  }

  addItemToBasket(product: IProduct, quantity: number = 1) {
    const payload = { productId: product.id, quantity };
    return this.http.post<any>(this.BaseURL + 'Baskets/items', payload).pipe(
      map((value) => this.normalizeBasket(value)),
      tap((basket) => this.setBasketState(basket)),
      catchError((err) => {
        console.log(err);
        return throwError(() => err);
      })
    ).subscribe({ next: () => {}, error: () => {} });
  }

  incrementBasketItemQuantity(item: IBasketItem) {
    const newQty = (item?.quantity ?? 0) + 1;
    this.updateItemQuantity(item.id, newQty).subscribe({ next: () => {}, error: () => {} });
  }

  DecrementBasketItemQuantity(item: IBasketItem) {
    const current = item?.quantity ?? 0;
    if (current <= 1) {
      this.removeItemFormBasket(item).subscribe({ next: () => {}, error: () => {} });
      return;
    }
    this.updateItemQuantity(item.id, current - 1).subscribe({ next: () => {}, error: () => {} });
  }

  updateItemQuantity(productId: number, quantity: number): Observable<IBasket | null> {
    return this.http.put<any>(this.BaseURL + `Baskets/items/${productId}`, { quantity }).pipe(
      map((value) => this.normalizeBasket(value)),
      tap((basket) => this.setBasketState(basket)),
      catchError((err) => {
        console.log(err);
        return throwError(() => err);
      })
    );
  }

  removeItemFormBasket(item: IBasketItem): Observable<IBasket | null> {
    return this.http.delete<any>(this.BaseURL + `Baskets/items/${item.id}`).pipe(
      map((value) => this.normalizeBasket(value)),
      tap((basket) => this.setBasketState(basket)),
      catchError((err) => {
        console.log(err);
        return throwError(() => err);
      })
    );
  }

  clearBasket(): Observable<IBasket | null> {
    return this.http.delete<any>(this.BaseURL + 'Baskets').pipe(
      map((value) => this.normalizeBasket(value)),
      tap((basket) => this.setBasketState(basket)),
      catchError((err) => {
        console.log(err);
        return throwError(() => err);
      })
    );
  }

  mergeGuestBasket(): Observable<IBasket | null> {
    return this.http.post<any>(this.BaseURL + 'Baskets/merge', {}).pipe(
      map((value) => this.normalizeBasket(value)),
      tap((basket) => this.setBasketState(basket)),
      catchError((err) => {
        console.log(err);
        return throwError(() => err);
      })
    );
  }

  // Legacy method retained; now performs server-validated replacement from provided basket items
  SetBasket(basket: IBasket) {
    const payload = {
      id: basket?.id ?? null,
      paymentIntentId: basket?.paymentIntentId ?? '',
      clientSecret: basket?.clientSecret ?? '',
      basketItems: (basket?.basketItems ?? []).map(i => ({
        id: i.id,
        name: i.name,
        description: i.description,
        image: i.image,
        qunatity: i.quantity,
        price: i.price,
        category: i.category
      }))
    };
    return this.http.post<any>(this.BaseURL + 'Baskets/update-basket', payload).subscribe({
      next: (value) => this.setBasketState(this.normalizeBasket(value)),
      error(err) {
        console.log(err);
      },
    });
  }

  // Legacy helpers retained where possible
  DeleteBasetItem(_basket: IBasket) {
    return this.clearBasket().subscribe({ next: () => {}, error: () => {} });
  }

  deleteBasket() {
    return this.clearBasket().subscribe({ next: () => {}, error: () => {} });
  }

  CreateBasket(): IBasket {
    return new BasketModel();
  }

  calculateTotal() {
    const basket = this.GetCurrentValue();
    if (!basket || !basket.basketItems) {
      this.basketSourceTotal.next({ shipping: this.shipPrice, subtotal: 0, total: this.shipPrice });
      return;
    }
    const shipping = this.shipPrice;
    const subtotal = basket.basketItems.reduce((a, c) => a + (c.price * c.quantity), 0);
    const total = shipping + subtotal;
    this.basketSourceTotal.next({ shipping, subtotal, total });
  }

  private setBasketState(basket: IBasket | null) {
    this.basketSource.next(basket);
    this.calculateTotal();
  }

  private normalizeBasket(value: any): IBasket | null {
    if (!value) return null;
    const items = Array.isArray(value.basketItems) ? value.basketItems : [];
    const basket: IBasket = {
      id: value.id ?? null,
      paymentIntentId: value.paymentIntentId ?? value.PaymentIntentId ?? null,
      clientSecret: value.clientSecret ?? value.clientSecret ?? null,
      basketItems: items.map((i: any) => ({
        id: Number(i.id),
        name: i.name ?? '',
        image: i.image ?? '',
        quantity: Number(i.quantity ?? i.qunatity ?? 0),
        price: Number(i.price ?? 0),
        category: i.category ?? '',
        description: i.description ?? ''
      }))
    };
    return basket;
  }
}
