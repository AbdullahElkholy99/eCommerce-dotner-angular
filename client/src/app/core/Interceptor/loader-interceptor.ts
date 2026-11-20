import {
  HttpInterceptorFn,
  HttpEvent,
  HttpHandler,
  HttpInterceptor,
  HttpRequest,
} from '@angular/common/http';
import { Injectable } from '@angular/core';
import { delay, finalize, Observable } from 'rxjs';
import { Loading } from '../Services/loading';

@Injectable()
export class loaderInterceptor implements HttpInterceptor {
  constructor(private _service: Loading) {}
  intercept(request: HttpRequest<any>, next: HttpHandler):
  
  Observable<HttpEvent<any>> {
    this._service.loading();

    return next.handle(request).pipe(
      finalize(() => {
        this._service.hideLoader();
      })
    );
  }
}

// export const loaderInterceptor: HttpInterceptorFn = (req, next) => {

//   return next(req);
// };
