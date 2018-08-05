import {Route} from '@angular/router';
import {LoginComponent} from './components/login/login.component';
import {LogoutComponent} from './components/logout/logout.component';
import {OverviewComponent} from './components/overview/overview.component';
import {PageNotFoundComponent} from './components/page-not-found-component/page-not-found.component';
import {ForgotComponent} from './components/forgot/forgot.component';
import {RegisterComponent} from './components/register/register.component';
import {AuthGuard} from './guards/auth-guard';

export const routes: Route[] = [
  { path: 'start', pathMatch: 'full', redirectTo: '/' },
  { path: 'login', component: LoginComponent },
  { path: 'logout', component: LogoutComponent },
  { path: 'register', component: RegisterComponent },
  { path: 'forgot', component: ForgotComponent },
  { path: '', pathMatch: 'full', component: OverviewComponent, canActivate: [AuthGuard]},
  { path: '**', component: PageNotFoundComponent }
];