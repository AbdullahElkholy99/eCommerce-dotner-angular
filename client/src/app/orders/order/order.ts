import { Component, OnInit } from '@angular/core';
import { IOrder, IOrderItem } from '../../shared/Models/Order';
import { OrderService } from '../order-service';
declare var bootstrap: any;

@Component({
  selector: 'app-order',
  standalone: false,
  templateUrl: './order.html',
  styleUrl: './order.scss'
})
export class Order implements OnInit {
  orders: IOrder[];
  UrlImageModal: string[] = [];
  constructor(private _service: OrderService) {}
  ngOnInit(): void {
    this._service.getAllOrderForUser().subscribe({
      next: (res) => {
        this.orders = res;
        console.log(res);
      },
      error: (err) => {
        console.log(err);
      },
    });
  }
  OpenModal(order: IOrderItem[]) {
    this.UrlImageModal = order.map((item) => item.mainImage);

    var modal = document.getElementById('ImageModal');
    var modalElement = new bootstrap.Modal(modal);
    modalElement.show();
  }
  CloseModal() {
    var modal=document.getElementById('ImageModal');
    var instance=bootstrap.Modal.getInstance(modal);
    instance.hide();
  }
  getFirstImageOrderItem(order: IOrderItem[]) {
    return order.length > 0 ? order[0].mainImage : null;
  }
}
