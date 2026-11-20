import { Component, OnInit } from '@angular/core';
import { IBasketTotal } from '../../Models/Basket';
import { BasketService } from '../../../basket/basket-service';

@Component({
  selector: 'app-order-total',
  standalone: false,
  templateUrl: './order-total.html',
  styleUrl: './order-total.scss'
})
export class OrderTotal implements OnInit {

  basketTotals:IBasketTotal

  constructor(private _basketService:BasketService) { }

  ngOnInit(): void {

    this._basketService.basketTotal$.subscribe({
      next:(value)=> {
        this.basketTotals=value
      },
      error(err) {
        console.log(err);
        
      },
    })
    
  }

}
