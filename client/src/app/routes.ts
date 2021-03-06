import {Route} from '@angular/router';
import {ConfirmComponent} from './components/confirm/confirm.component';
import {ForgotComponent} from './components/forgot/forgot.component';
import {LoginComponent} from './components/login/login.component';
import {LogoutComponent} from './components/logout/logout.component';
import {OverviewComponent} from './components/overview/overview.component';
import {PageNotFoundComponent} from './components/page-not-found/page-not-found.component';
import {RegisterComponent} from './components/register/register.component';
import {ResetComponent} from './components/reset/reset.component';
import {AuthGuard} from './guards/auth-guard';

export const routes: Route[] = [
  {path: 'start', pathMatch: 'full', redirectTo: '/'},
  {path: 'login', component: LoginComponent},
  {path: 'logout', component: LogoutComponent},
  {path: 'register', component: RegisterComponent},
  {path: 'confirm', component: ConfirmComponent},
  {path: 'forgot', component: ForgotComponent},
  {path: 'reset', component: ResetComponent},
  {path: '', pathMatch: 'full', component: OverviewComponent, canActivate: [AuthGuard]},
  {path: '**', component: PageNotFoundComponent},
];
