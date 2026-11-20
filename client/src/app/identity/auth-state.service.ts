import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, catchError, map, of, tap } from 'rxjs';
import { environment } from '../../environments/environment.development';

@Injectable({ providedIn: 'root' })
export class AuthStateService {
  private readonly baseURL = environment.baseURL;
  private readonly authenticatedSubject = new BehaviorSubject<boolean>(false);
  private readonly userNameSubject = new BehaviorSubject<string>('');
  private checked = false;

  readonly isAuthenticated$ = this.authenticatedSubject.asObservable();
  readonly userName$ = this.userNameSubject.asObservable();

  constructor(private http: HttpClient) {}

  checkAuth(force = false): Observable<boolean> {
    if (this.checked && !force) {
      return of(this.authenticatedSubject.value);
    }
    return this.http.get(this.baseURL + 'Account/IsUserAuth').pipe(
      map(() => true),
      tap(() => {
        this.authenticatedSubject.next(true);
        this.checked = true;
      }),
      catchError(() => {
        this.authenticatedSubject.next(false);
        this.userNameSubject.next('');
        this.checked = true;
        return of(false);
      })
    );
  }

  loadUserName(): Observable<string> {
    return this.http.get<any>(this.baseURL + 'Account/get-user-name').pipe(
      map((res) => res?.message ?? ''),
      tap((name) => this.userNameSubject.next(name)),
      catchError(() => {
        this.userNameSubject.next('');
        return of('');
      })
    );
  }

  setAuthenticated(authenticated: boolean): void {
    this.authenticatedSubject.next(authenticated);
    this.checked = true;
    if (!authenticated) {
      this.userNameSubject.next('');
    }
  }

  get isAuthenticated(): boolean {
    return this.authenticatedSubject.value;
  }
}
