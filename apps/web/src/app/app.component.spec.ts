import { MediaMatcher } from '@angular/cdk/layout';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatSidenav } from '@angular/material/sidenav';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter, Router } from '@angular/router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AppComponent } from './app.component';
import { AuthService } from './core/auth/auth.service';

describe('AppComponent navigation shell', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('pins the desktop drawer open and does not render a menu toggle', () => {
    const fixture = createFixture(false);
    const component = fixture.componentInstance as unknown as { toggleNavigation: () => void };
    const drawer = fixture.debugElement.query(By.directive(MatSidenav)).componentInstance as MatSidenav;

    expect(drawer.mode).toBe('side');
    expect(drawer.opened).toBe(true);
    expect(fixture.nativeElement.querySelector('mat-toolbar')).toBeNull();
    expect(fixture.nativeElement.querySelector('[aria-label="Open navigation"], [aria-label="Close navigation"]')).toBeNull();

    component.toggleNavigation();
    fixture.detectChanges();

    expect(drawer.opened).toBe(true);
  });

  it('uses an overlay drawer and a menu toggle on handsets', () => {
    const fixture = createFixture(true);
    const drawer = fixture.debugElement.query(By.directive(MatSidenav)).componentInstance as MatSidenav;

    expect(drawer.mode).toBe('over');
    expect(drawer.opened).toBe(false);
    expect(fixture.nativeElement.querySelector('mat-toolbar')).not.toBeNull();

    (fixture.nativeElement.querySelector('[aria-label="Open navigation"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(drawer.opened).toBe(true);
    expect(fixture.nativeElement.querySelector('[aria-label="Close navigation"]')).not.toBeNull();
  });

  it('keeps the route-selected navigation item active after it loses focus', async () => {
    const fixture = createFixture(false, true);
    const router = TestBed.inject(Router);

    await router.navigateByUrl('/suppliers');
    fixture.detectChanges();

    const supplierLink = fixture.nativeElement.querySelector('a[routerlink="/suppliers"]') as HTMLAnchorElement;
    supplierLink.focus();
    supplierLink.blur();

    expect(supplierLink.classList.contains('drawer-link-active')).toBe(true);
  });
});

@Component({ standalone: true, template: '' })
class RouteStubComponent {}

function createFixture(isHandset: boolean, withSupplierRoute = false) {
  const mediaQuery = {
    matches: isHandset,
    addEventListener: vi.fn(),
    removeEventListener: vi.fn()
  };

  TestBed.configureTestingModule({
    imports: [AppComponent],
    providers: [
      provideNoopAnimations(),
      provideRouter(withSupplierRoute ? [{ path: 'suppliers', component: RouteStubComponent }] : []),
      { provide: AuthService, useValue: { hasValidSession: () => true, logout: vi.fn() } },
      { provide: MediaMatcher, useValue: { matchMedia: vi.fn(() => mediaQuery) } }
    ]
  });

  const fixture = TestBed.createComponent(AppComponent);
  fixture.detectChanges();
  return fixture;
}
