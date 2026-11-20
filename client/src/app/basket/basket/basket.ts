import { Component, OnInit } from '@angular/core';
import { IBasket, IBasketItem } from '../../shared/Models/Basket';
import { BasketService } from '../basket-service';

@Component({
  selector: 'app-basket',
  standalone: false,
  templateUrl: './basket.html',
  styleUrl: './basket.scss'
})
export class Basket implements OnInit {
  constructor(private basketService: BasketService) {}

  basket: IBasket | null = null;

  ngOnInit(): void {
    this.basketService.basket$.subscribe({
      next: (value) => {
        this.basket = value;
      },
      error(err) {
        console.log(err);
      },
    });
    // ensure latest server-resolved basket
    this.basketService.loadCurrentBasket().subscribe({ next: () => {}, error: () => {} });
  }

  RemoveBasket(item: IBasketItem) {
    this.basketService.removeItemFormBasket(item).subscribe({ next: () => {}, error: () => {} });
  }

  incrementQuantity(item: IBasketItem) {
    this.basketService.incrementBasketItemQuantity(item);
  }

  DecrementQuantity(item: IBasketItem) {
    this.basketService.DecrementBasketItemQuantity(item);
  }
}

