import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { LoginComponent } from './features/auth/components/login/login.component';
import { ChatComponent } from './features/chat/components/chat/chat.component';
import { SupplierListComponent } from './features/suppliers/components/supplier-list/supplier-list.component';
import { PurchaseOrderListComponent } from './features/orders/components/purchase-order-list/purchase-order-list.component';
import { IncidentListComponent } from './features/incidents/components/incident-list/incident-list.component';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'chat', component: ChatComponent, canActivate: [authGuard] },
  { path: 'suppliers', component: SupplierListComponent, canActivate: [authGuard] },
  { path: 'orders', component: PurchaseOrderListComponent, canActivate: [authGuard] },
  { path: 'incidents', component: IncidentListComponent, canActivate: [authGuard] },
  { path: '', pathMatch: 'full', redirectTo: 'chat' },
  { path: '**', redirectTo: 'chat' }
];
