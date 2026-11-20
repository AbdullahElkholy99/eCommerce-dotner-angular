import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { Register } from './register/register';
import { Active } from './active/active';
import { Login } from './login/login';
import { ResetPassword } from './reset-password/reset-password';
import { GuestGuard } from './guest.guard';

const routes: Routes = [
  {path:'register',component:Register, canActivate: [GuestGuard]},
  {path:'active',component:Active},
  {path:'Login',component:Login, canActivate: [GuestGuard]},
  {path:'Reset-Password',component:ResetPassword},
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class IdentityRoutingModule { }
