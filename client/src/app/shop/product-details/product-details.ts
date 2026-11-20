import { Component, OnInit } from '@angular/core';
import { IProduct } from '../../shared/Models/Product';
import { ShopService } from '../shop-service';
import { ActivatedRoute } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { BasketService } from '../../basket/basket-service';

@Component({
  selector: 'app-product-details',
  standalone: false,
  templateUrl: './product-details.html',
  styleUrl: './product-details.scss'
})
export class ProductDetails implements OnInit {
  constructor(
   private shopService: ShopService,
    private route: ActivatedRoute,
    private toast:ToastrService,
    private basketService:BasketService
  ) {}

  quantity: number = 1;
  product?: IProduct;
  loading:boolean=false;
  MainImage: string;
  ngOnInit(): void {
    this.loadProduct();
  }
  loadProduct() {
    this.shopService
      .getProductDetails(parseInt(this.route.snapshot.paramMap.get('id')))
      .subscribe({
        next: (value: IProduct) => {
          this.product = value;
          this.MainImage = this.product.photos[0].name;
        },
      });
  }
  ReplaceImage(src: string) {
    this.MainImage = src;
  }

  incrementBasket() {
    if (this.quantity < 10) {
      this.quantity++;
      this.toast.success("Quantity increased", "SUCCESS");
    } else {
      this.toast.warning("You can't add more than 10 items", "Enough");
    }
  }
  DecrementBasket() {
    if (this.quantity > 1) {
      this.quantity--;
      this.toast.warning("Quantity decreased", "SUCCESS");
    } else {
      this.toast.error("You can't decrement below 1 item", "ERROR");
    }
  }
  AddToBasket() {
    if (!this.product) return;
    this.basketService.addItemToBasket(this.product, this.quantity);
    this.toast.success("Item has been added to basket", "SUCCESS");
  }
  CalucateDiscount(oldPrice:number,newPrice:number):number{
    return parseFloat(
    Math.round(((oldPrice-newPrice)/oldPrice)*100).toFixed(1)      
    )
  }
  // showReview(id:number){
  //   this.loading=true
  //   this.shopService.getProductRating(id).subscribe({
  //     next:res=>{
  //       this.loading=false
  //       this.reviews=res
  //     },error(err) {
  //       console.log(err);
        
  //     },
  //   })
  // }


}
 


