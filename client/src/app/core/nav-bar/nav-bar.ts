import { Component, OnInit } from '@angular/core';
import { BasketService } from '../../basket/basket-service';
import { Observable } from 'rxjs';
import { IBasket } from '../../shared/Models/Basket';
import { AuthStateService } from '../../identity/auth-state.service';
import { IdentityService } from '../../identity/identity-service';
import { Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';

@Component({
  selector: 'app-nav-bar',
  standalone: false,
  templateUrl: './nav-bar.html',
  styleUrl: './nav-bar.scss',
})
export class NavBar implements OnInit {
  userName = '';
  visibale = false;
  count!: Observable<IBasket | null>;
  isAuthenticated = false;

  constructor(
    private basketService: BasketService,
    private authState: AuthStateService,
    private identityService: IdentityService,
    private router: Router,
    private toastr: ToastrService
  ) {}

  ngOnInit(): void {
    this.count = this.basketService.basket$;
    this.basketService.loadCurrentBasket().subscribe({
      next: () => {},
      error(err) {
        console.log(err);
      },
    });

    this.authState.isAuthenticated$.subscribe((v) => (this.isAuthenticated = v));
    this.authState.userName$.subscribe((v) => (this.userName = v));
    this.authState.checkAuth(true).subscribe((authed) => {
      if (authed) {
        this.authState.loadUserName().subscribe({ next: () => {}, error: () => {} });
      }
    });
  }

  getBasketCount(basket: IBasket | null): number {
    if (!basket || !basket.basketItems) return 0;
    return basket.basketItems.reduce((sum, item) => sum + (item.quantity || 0), 0);
  }

  logout() {
    this.identityService.logout().subscribe({
      next: () => {
        this.authState.setAuthenticated(false);
        this.visibale = false;
        this.toastr.success('Logged out successfully');
        // reload basket as guest (server-resolved) after logout
        this.basketService.loadCurrentBasket().subscribe({ next: () => {}, error: () => {} });
        this.router.navigateByUrl('/');
      },
      error: () => {
        this.authState.setAuthenticated(false);
        this.basketService.loadCurrentBasket().subscribe({ next: () => {}, error: () => {} });
        this.router.navigateByUrl('/');
      }
    });
  }

  ToggleDropDown() {
    this.visibale = !this.visibale;
  }
}
