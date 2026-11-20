import { Component, Input, OnInit } from '@angular/core';
import { FormGroup } from '@angular/forms';
import { CheckoutService } from '../checkout-service';

@Component({
  selector: 'app-address',
  standalone: false,
  templateUrl: './address.html',
  styleUrl: './address.scss',
})
export class Address implements OnInit {
  @Input() address: FormGroup;
  canEdit = false;

  constructor(private _service: CheckoutService) {}

  ngOnInit(): void {
    this._service.getAddress().subscribe({
      next: (value) => {
        this.address.patchValue(value);
      },
    });
  }

  UpdateAddress() {
    if (this.address.valid) {
      this._service.updateAddress(this.address.value).subscribe({
        next(value) {
          console.log(value);
        },
        error(err) {
          console.log(err);
        },
      });
    }
  }
  
}
