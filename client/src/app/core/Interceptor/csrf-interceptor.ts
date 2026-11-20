import { HttpEvent, HttpHandler, HttpInterceptor, HttpRequest } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, catchError, from, switchMap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment.development';

@Injectable({
  providedIn: 'root'
})
export class CsrfInterceptor implements HttpInterceptor {
  private token: string | null = null;
  private tokenPromise: Promise<string | null> | null = null;

  intercept(request: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    if (!this.requiresCsrf(request)) {
      return next.handle(request);
    }

    return from(this.ensureToken()).pipe(
      switchMap((token) => {
        const req = token
          ? request.clone({ setHeaders: { 'X-XSRF-TOKEN': token } })
          : request;
        return next.handle(req).pipe(
          catchError((err) => {
            // If token became invalid, clear and retry once for 400 antiforgery failures.
            if (err?.status === 400 && typeof err?.error?.message === 'string' && err.error.message.toLowerCase().includes('antiforgery')) {
              this.token = null;
              return from(this.ensureToken(true)).pipe(
                switchMap((newToken) => {
                  const retryReq = newToken
                    ? request.clone({ setHeaders: { 'X-XSRF-TOKEN': newToken } })
                    : request;
                  return next.handle(retryReq);
                })
              );
            }
            return throwError(() => err);
          })
        );
      })
    );
  }

  private requiresCsrf(request: HttpRequest<any>): boolean {
    const method = (request.method || 'GET').toUpperCase();
    if (method === 'GET' || method === 'HEAD' || method === 'OPTIONS' || method === 'TRACE') {
      return false;
    }
    // Apply to API requests only
    return request.url.startsWith(environment.baseURL) || request.url.startsWith('/api');
  }

  private async ensureToken(forceRefresh = false): Promise<string | null> {
    if (!forceRefresh && this.token) {
      return this.token;
    }
    if (!forceRefresh && this.tokenPromise) {
      return this.tokenPromise;
    }

    this.tokenPromise = this.fetchToken()
      .then((token) => {
        this.token = token;
        return token;
      })
      .catch(() => {
        this.token = null;
        return null;
      })
      .finally(() => {
        this.tokenPromise = null;
      });

    return this.tokenPromise;
  }

  private async fetchToken(): Promise<string | null> {
    try {
      const url = `${environment.baseURL}Antiforgery/token`;
      const res = await fetch(url, {
        method: 'GET',
        credentials: 'include',
        headers: { 'Accept': 'application/json' }
      });
      if (!res.ok) return null;
      const data = await res.json();
      return typeof data?.token === 'string' ? data.token : null;
    } catch {
      return null;
    }
  }
}
