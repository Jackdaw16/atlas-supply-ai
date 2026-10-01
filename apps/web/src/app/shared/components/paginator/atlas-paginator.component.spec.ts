import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AtlasPaginatorComponent, PaginationChange } from './atlas-paginator.component';

describe('AtlasPaginatorComponent', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('announces the visible range and emits page navigation changes', () => {
    const fixture = createPaginator({ totalCount: 95, pageIndex: 1, pageSize: 25 });
    const changes: PaginationChange[] = [];
    fixture.componentInstance.pageChange.subscribe((change) => changes.push(change));

    expect(fixture.nativeElement.querySelector('[role="status"]')?.textContent).toContain('Showing 26–50 of 95 results');

    (fixture.nativeElement.querySelector('[aria-label="Next page"]') as HTMLButtonElement).click();
    (fixture.nativeElement.querySelector('[aria-label="Last page"]') as HTMLButtonElement).click();

    expect(changes).toEqual([
      { pageIndex: 2, pageSize: 25 },
      { pageIndex: 3, pageSize: 25 }
    ]);
  });

  it('preserves the first visible result when the page size changes', () => {
    const fixture = createPaginator({ totalCount: 200, pageIndex: 2, pageSize: 25 });
    const pageChange = vi.fn();
    fixture.componentInstance.pageChange.subscribe(pageChange);
    const select = fixture.nativeElement.querySelector('select') as HTMLSelectElement;

    select.value = '10';
    select.dispatchEvent(new Event('change'));

    expect(pageChange).toHaveBeenCalledWith({ pageIndex: 5, pageSize: 10 });
  });

  it('disables page-size and navigation controls while loading', () => {
    const fixture = createPaginator({ totalCount: 95, pageIndex: 0, pageSize: 25, loading: true });

    expect(fixture.nativeElement.querySelector('nav')?.getAttribute('aria-busy')).toBe('true');
    expect(fixture.nativeElement.querySelector('select')?.disabled).toBe(true);
    expect(fixture.nativeElement.querySelector('[aria-label="Next page"]')?.disabled).toBe(true);
  });
});

function createPaginator(inputs: { totalCount: number; pageIndex: number; pageSize: number; loading?: boolean }) {
  const fixture = TestBed.createComponent(AtlasPaginatorComponent);
  fixture.componentRef.setInput('totalCount', inputs.totalCount);
  fixture.componentRef.setInput('pageIndex', inputs.pageIndex);
  fixture.componentRef.setInput('pageSize', inputs.pageSize);
  fixture.componentRef.setInput('loading', inputs.loading ?? false);
  fixture.detectChanges();
  return fixture;
}
