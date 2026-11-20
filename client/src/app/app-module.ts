import { NgModule, provideBrowserGlobalErrorListeners } from '@angular/core';
import { BrowserModule, provideClientHydration, withEventReplay } from '@angular/platform-browser';

import { AppRoutingModule } from './app-routing-module';
import { App } from './app';
import { CoreModule } from './core/core-module';
import { SharedModule } from './shared/shared-module';
import { HTTP_INTERCEPTORS, provideHttpClient, withInterceptors, withInterceptorsFromDi } from '@angular/common/http';
import { ShopModule } from './shop/shop-module';
import { PaginationModule } from 'ngx-bootstrap/pagination';
import { HomeModule } from './home/home-module';
import { NgxSpinnerModule } from 'ngx-spinner';
import { loaderInterceptor } from './core/Interceptor/loader-interceptor';
import { ToastrModule } from 'ngx-toastr';
import { MatBadgeModule } from '@angular/material/badge';
import { credentialsInterceptor } from './core/Interceptor/credentials-interceptor';
import { AuthErrorInterceptor } from './core/Interceptor/auth-error-interceptor';
import { CsrfInterceptor } from './core/Interceptor/csrf-interceptor';
import { loadStripe } from '@stripe/stripe-js';

@NgModule({
  declarations: [
    App
  ],
  imports: [
    BrowserModule,
    AppRoutingModule,
    CoreModule,
    NgxSpinnerModule,
    ToastrModule.forRoot({
      closeButton: true,
      positionClass: 'toast-top-right',
      countDuplicates: true,
      timeOut:1500,
      progressBar:true,
      progressAnimation:'increasing',
    }),
    PaginationModule.forRoot()
  ],
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideClientHydration(withEventReplay()),
    provideHttpClient(withInterceptorsFromDi()),
    { provide: HTTP_INTERCEPTORS, useClass: credentialsInterceptor , multi: true },
    { provide: HTTP_INTERCEPTORS, useClass: CsrfInterceptor , multi: true },
    { provide: HTTP_INTERCEPTORS, useClass: loaderInterceptor , multi: true },
    { provide: HTTP_INTERCEPTORS, useClass: AuthErrorInterceptor , multi: true },
  ],
  bootstrap: [App]
})
export class AppModule { }
