import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  HostListener,
  OnDestroy,
  OnInit,
} from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { TranslateService } from '@ngx-translate/core';
import { Subscription } from 'rxjs';

import { SubheaderService } from 'src/app/_metronic/partials/layout';
import {
  LayoutUtilsService,
  MessageType,
} from '../../../_core/utils/layout-utils.service';
import {
  GroupingState,
  PaginatorState,
  SortState,
} from 'src/app/_metronic/shared/crud-table';
import { AuthService } from 'src/app/modules/auth';
import { showSearchFormModel } from '../../../_shared/jee-search-form/jee-search-form.model';

import { NhanVienManagementService } from '../Services/nhan-vien-management.service';
import { NhanVienManagementEditDialogComponent } from '../nhan-vien-management-edit-dialog/nhan-vien-management-edit-dialog.component';
import { NhanVienImportDialogComponent } from '../nhan-vien-import-dialog/nhan-vien-import-dialog.component';

@Component({
  selector: 'app-nhan-vien-management-list',
  templateUrl: './nhan-vien-management-list.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NhanVienManagementListComponent implements OnInit, OnDestroy {
  paginator!: PaginatorState;
  sorting!: SortState;
  grouping!: GroupingState;
  isLoading = false;

  private subscriptions: Subscription[] = [];

  displayedColumns: string[] = [
    'manv',
    'hoten',
    'sdt',
    'cccd',
    'email',
    'phongban',
    'chucvu',
    'trangthai',
    'ThaoTac',
  ];

  showSearch = new showSearchFormModel();

  constructor(
    private changeDetect: ChangeDetectorRef,
    public nhanVienManagementService: NhanVienManagementService,
    private translate: TranslateService,
    public subheaderService: SubheaderService,
    private layoutUtilsService: LayoutUtilsService,
    public dialog: MatDialog,
    private auth: AuthService
  ) {}

  ngOnInit(): void {
    this.nhanVienManagementService.fetch();

    this.grouping = this.nhanVienManagementService.grouping;
    this.paginator = this.nhanVienManagementService.paginator;
    this.sorting = this.nhanVienManagementService.sorting;

    const sb = this.nhanVienManagementService.isLoading$.subscribe((res) => {
      this.isLoading = res;
      this.changeDetect.detectChanges();
    });

    this.subscriptions.push(sb);
    this.configShowSearch();
  }

  ngOnDestroy(): void {
    this.subscriptions.forEach((sb) => sb.unsubscribe());
  }

  sort(column: string): void {
    const sorting = this.sorting;

    if (sorting.column !== column) {
      sorting.column = column;
      sorting.direction = 'asc';
    } else {
      sorting.direction = sorting.direction === 'asc' ? 'desc' : 'asc';
    }

    this.nhanVienManagementService.patchState({ sorting });
  }

  paginate(paginator: PaginatorState): void {
    this.nhanVienManagementService.patchState({ paginator });
  }

  getHeight(): string {
    return window.innerHeight - 236 + 'px';
  }

  create(): void {
    const dialogRef = this.dialog.open(NhanVienManagementEditDialogComponent, {
      data: {},
      width: '1150px',
      maxWidth: '95vw',
      disableClose: true,
    });

    dialogRef.afterClosed().subscribe((res) => {
      if (res) {
        this.layoutUtilsService.showActionNotification(
          this.translate.instant('Thêm thành công'),
          MessageType.Create,
          4000,
          true,
          false
        );
      }

      this.nhanVienManagementService.fetch();
    });
  }

  edit(item: any): void {
    const dialogRef = this.dialog.open(NhanVienManagementEditDialogComponent, {
      data: { item },
      width: '1150px',
      maxWidth: '95vw',
      disableClose: true,
    });

    dialogRef.afterClosed().subscribe((res) => {
      if (res) {
        this.layoutUtilsService.showActionNotification(
          this.translate.instant('Cập nhật thành công'),
          MessageType.Update,
          4000,
          true,
          false
        );
      }

      this.nhanVienManagementService.fetch();
    });
  }

  delete(item: any): void {
    const id = item?.Id || item?.RowID;
    const dialog = this.layoutUtilsService.deleteElement(
      '',
      'Bạn có muốn xoá nhân viên này không?'
    );

    dialog.afterClosed().subscribe((res) => {
      if (!res) {
        return;
      }

      this.nhanVienManagementService.deleteNhanVien(id).subscribe(
        () => {
          this.layoutUtilsService.showActionNotification(
            'Xoá thành công',
            MessageType.Delete,
            4000,
            true,
            false
          );
          this.nhanVienManagementService.fetch();
        },
        (error) => {
          this.layoutUtilsService.showActionNotification(
            error?.error?.message || 'Xoá thất bại',
            MessageType.Read,
            4000,
            true,
            false
          );
        }
      );
    });
  }

  lockNhanVien(item: any): void {
    const id = item?.Id || item?.RowID;
    const dialog = this.layoutUtilsService.deleteElement(
      '',
      'Bạn có muốn khoá nhân viên này không?'
    );

    dialog.afterClosed().subscribe((res) => {
      if (!res) {
        return;
      }

      this.nhanVienManagementService.lockNhanVien(id).subscribe(
        () => {
          this.layoutUtilsService.showActionNotification(
            'Khoá thành công',
            MessageType.Update,
            4000,
            true,
            false
          );
          this.nhanVienManagementService.fetch();
        },
        (error) => {
          this.layoutUtilsService.showActionNotification(
            error?.error?.message || 'Khoá thất bại',
            MessageType.Read,
            4000,
            true,
            false
          );
        }
      );
    });
  }

  unLockNhanVien(item: any): void {
    const id = item?.Id || item?.RowID;
    const dialog = this.layoutUtilsService.deleteElement(
      '',
      'Bạn có muốn mở khoá nhân viên này không?'
    );

    dialog.afterClosed().subscribe((res) => {
      if (!res) {
        return;
      }

      this.nhanVienManagementService.unlockNhanVien(id).subscribe(
        () => {
          this.layoutUtilsService.showActionNotification(
            'Mở khoá thành công',
            MessageType.Update,
            4000,
            true,
            false
          );
          this.nhanVienManagementService.fetch();
        },
        (error) => {
          this.layoutUtilsService.showActionNotification(
            error?.error?.message || 'Mở khoá thất bại',
            MessageType.Read,
            4000,
            true,
            false
          );
        }
      );
    });
  }

  filter(): void {
    this.nhanVienManagementService.patchState({ filter: {} });
  }

  filterDaKhoa(): void {
    this.nhanVienManagementService.patchState({
      filter: { dakhoa: '1' },
    });
  }

  filterDangSuDung(): void {
    this.nhanVienManagementService.patchState({
      filter: { dangsudung: '1' },
    });
  }

  filterAll(): void {
    this.nhanVienManagementService.patchState({
      filter: {},
    });
  }

  changeKeyword(val: string): void {
    this.search(val);
  }

  changeFilter(filter: any): void {
    this.nhanVienManagementService.patchState({ filter });
  }

  search(searchTerm: string): void {
    this.nhanVienManagementService.patchState({ searchTerm });
  }

  configShowSearch(): void {
    this.showSearch.dakhoa = true;
    this.showSearch.isAdmin = false;
    this.showSearch.username = false;
    this.showSearch.titlekeyword = 'Tìm kiếm nhân viên';

    this.changeDetect.detectChanges();
  }

  import(): void {
    const dialogRef = this.dialog.open(NhanVienImportDialogComponent, {
      data: {},
      width: '1150px',
      maxWidth: '95vw',
      disableClose: true,
    });
    dialogRef.afterClosed().subscribe((res) => {
      if (res) {
        this.layoutUtilsService.showActionNotification('Import nhân viên thành công', MessageType.Create, 4000, true, false);
        this.nhanVienManagementService.fetch();
      }
    });
  }

  @HostListener('window:beforeunload', ['$event'])
  beforeunloadHandler(e: any): void {
    this.auth.updateLastlogin().subscribe();
  }
}
