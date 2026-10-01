import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

export interface PaginationChange {
  pageIndex: number;
  pageSize: number;
}

@Component({
  selector: 'app-atlas-paginator',
  standalone: true,
  templateUrl: './atlas-paginator.component.html',
  styleUrl: './atlas-paginator.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AtlasPaginatorComponent {
  readonly totalCount = input.required<number>();
  readonly pageIndex = input.required<number>();
  readonly pageSize = input.required<number>();
  readonly pageSizeOptions = input<readonly number[]>([10, 25, 50]);
  readonly disabled = input(false);
  readonly loading = input(false);
  readonly pageChange = output<PaginationChange>();

  protected rangeLabel(): string {
    if (this.totalCount() === 0) {
      return 'Showing 0–0 of 0 results';
    }

    const start = this.pageIndex() * this.pageSize() + 1;
    const end = Math.min(start + this.pageSize() - 1, this.totalCount());
    return `Showing ${start}–${end} of ${this.totalCount()} results`;
  }

  protected canNavigate(pageIndex: number): boolean {
    return !this.disabled()
      && !this.loading()
      && pageIndex >= 0
      && pageIndex <= this.lastPageIndex()
      && pageIndex !== this.pageIndex();
  }

  protected goToPage(pageIndex: number): void {
    if (!this.canNavigate(pageIndex)) {
      return;
    }

    this.pageChange.emit({ pageIndex, pageSize: this.pageSize() });
  }

  protected selectPageSize(event: Event): void {
    if (this.disabled() || this.loading()) {
      return;
    }

    const pageSize = Number((event.target as HTMLSelectElement).value);
    if (!this.pageSizeOptions().includes(pageSize) || pageSize === this.pageSize()) {
      return;
    }

    const pageIndex = Math.floor((this.pageIndex() * this.pageSize()) / pageSize);
    this.pageChange.emit({ pageIndex, pageSize });
  }

  protected lastPageIndex(): number {
    return Math.max(Math.ceil(this.totalCount() / this.pageSize()) - 1, 0);
  }
}
