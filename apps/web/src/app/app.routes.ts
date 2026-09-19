import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { LoginComponent } from './features/auth/components/login/login.component';
import { ChatComponent } from './features/chat/components/chat/chat.component';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'chat', component: ChatComponent, canActivate: [authGuard] },
  { path: '', pathMatch: 'full', redirectTo: 'chat' },
  { path: '**', redirectTo: 'chat' }
];
