export interface IBasket {
  id?: string | null;
  paymentIntentId?: string | null;
  clientSecret?: string | null;
  basketItems: IBasketItem[];
}

export interface IBasketItem {
  id: number;
  name: string;
  image: string;
  quantity: number;
  price: number;
  category: string;
  description: string;
}

export class BasketModel implements IBasket {
  id: string | null = null;
  paymentIntentId: string | null = null;
  clientSecret: string | null = null;
  basketItems: IBasketItem[] = [];
}

export interface IBasketTotal {
  shipping: number;
  subtotal: number;
  total: number;
}
