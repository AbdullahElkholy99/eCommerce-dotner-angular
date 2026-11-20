import { HttpErrorResponse, HttpEvent, HttpHandler, HttpInterceptor, HttpRequest } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, throwError } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import { AuthStateService } from '../../identity/auth-state.service';

@Injectable({ providedIn: 'root' })
export class AuthErrorInterceptor implements HttpInterceptor {
  constructor(
    private router: Router,
    private toastr: ToastrService,
    private authState: AuthStateService
  ) {}

  intercept(request: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    return next.handle(request).pipe(
      catchError((error: HttpErrorResponse) => {
        if (error.status === 401) {
          this.authState.setAuthenticated(false);
          const url = request.url || '';
          const isAuthRequest = /Account\/(Login|Register|active-account|reset-password|send-email-forget-password|IsUserAuth|get-user-name)/i.test(url);
          const isOnLogin = this.router.url.startsWith('/account/Login');
          if (!isAuthRequest && !isOnLogin) {
            this.toastr.warning('Your session has expired or you are not authenticated.', 'Authentication');
            this.router.navigate(['/account/Login'], { queryParams: { returnUrl: this.router.url } });
          }
        } else if (error.status === 403) {
          this.toastr.error('You do not have permission to access this resource.', 'Forbidden');
        }
        return throwError(() => error);
      })
    );
  }
}
