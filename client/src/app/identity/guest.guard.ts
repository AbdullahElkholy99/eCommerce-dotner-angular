import { Injectable } from '@angular/core';
import { CanActivate, Router, UrlTree } from '@angular/router';
import { Observable, map } from 'rxjs';
import { AuthStateService } from './auth-state.service';

@Injectable({ providedIn: 'root' })
export class GuestGuard implements CanActivate {
  constructor(private authState: AuthStateService, private router: Router) {}

  canActivate(): Observable<boolean | UrlTree> {
    return this.authState.checkAuth(true).pipe(
      map((isAuth) => (isAuth ? this.router.createUrlTree(['/']) : true))
    );
  }
}
