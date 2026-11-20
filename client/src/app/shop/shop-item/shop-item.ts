import { Component, Input } from '@angular/core';
import { IProduct } from '../../shared/Models/Product';
import { BasketService } from '../../basket/basket-service';

@Component({
  selector: 'app-shop-item',
  standalone: false,
  templateUrl: './shop-item.html',
  styleUrl: './shop-item.scss'
})
export class ShopItem {

  constructor(private _service: BasketService) {}
  
  @Input() Product: IProduct;

  SetBasketValue() {
    this._service.addItemToBasket(this.Product);
  }
  getArrayofRating(rateOfnumber:number):number[]{
    return Array(rateOfnumber).fill(0).map((x,i)=>i);
  }
}

