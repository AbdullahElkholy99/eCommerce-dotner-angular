import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { IdentityService } from '../identity-service';
import { Router } from '@angular/router';
import { ActivatedRoute } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { AuthStateService } from '../auth-state.service';
import { BasketService } from '../../basket/basket-service';

@Component({
  selector: 'app-login',
  standalone: false,
  templateUrl: './login.html',
  styleUrl: './login.scss'
})
export class Login implements OnInit {
  formGroup: FormGroup;
  emailModel: string = '';
  retrunUrl='/'
  isSubmitting = false;
  errorMessage = '';
  constructor(
    private fb: FormBuilder,
    private _service: IdentityService,
    private route: Router,
    private router: ActivatedRoute,
    private toastr: ToastrService,
    private authState: AuthStateService,
    private basketService: BasketService
  ) {}
  ngOnInit(): void {
    this.FormValidation();

    const myModal = document.getElementById('exampleModal');
    const myInput = document.getElementById('myInput');

    myModal?.addEventListener('shown.bs.modal', () => {
      (myInput as HTMLElement | null)?.focus();
    });
    this.router.queryParams.subscribe(param=>{
      this.retrunUrl=param["returnUrl"]||'/'
    })
  }
  FormValidation() {
    this.formGroup = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
      password: [
        '',
        [
          Validators.required,
          Validators.pattern(
            /^(?=.*[0-9])(?=.*[#$@!.\-])[A-Za-z\d#$@!.\-]{8,}$/
          ),
        ],
      ],
    });
  }

  get _email() {
    return this.formGroup.get('email');
  }
  get _password() {
    return this.formGroup.get('password');
  }

  Submit() {
    if (this.formGroup.invalid || this.isSubmitting) {
      return;
    }
    this.isSubmitting = true;
    this.errorMessage = '';
    this._service.Login(this.formGroup.value).subscribe({
      next: () => {
        this.authState.setAuthenticated(true);
        this.authState.loadUserName().subscribe({ next: () => {}, error: () => {} });
        // Idempotent server-side merge of guest basket into authenticated basket after login
        this.basketService.mergeGuestBasket().subscribe({
          next: () => {
            this.toastr.success('Login successful');
            this.route.navigateByUrl(this.retrunUrl || '/');
            this.isSubmitting = false;
          },
          error: () => {
            // Merge failure should not block successful authentication UX
            this.basketService.loadCurrentBasket().subscribe({ next: () => {}, error: () => {} });
            this.toastr.success('Login successful');
            this.route.navigateByUrl(this.retrunUrl || '/');
            this.isSubmitting = false;
          }
        });
      },
      error: (err) => {
        const msg = err?.error?.message || err?.error?.title || 'Login failed. Please check your credentials.';
        this.errorMessage = msg;
        this.toastr.error(msg, 'Login');
        this.isSubmitting = false;
      },
    });
  }

  SendEmailForgetpassword() {
    if (!this.emailModel) {
      this.toastr.warning('Please enter your email address.');
      return;
    }
    this._service.forgetPassword(this.emailModel).subscribe({
      next: () => {
        this.toastr.success('If the account exists, a password reset email will be processed.');
      },
      error: (err) => {
        const msg = err?.error?.message || 'Unable to process password reset request.';
        this.toastr.error(msg, 'Forget password');
      },
    });
  }
}
