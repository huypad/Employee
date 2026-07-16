import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Routes } from '@angular/router';
import { MAT_DIALOG_DEFAULT_OPTIONS } from '@angular/material/dialog';
import { NgxMatSelectSearchModule } from 'ngx-mat-select-search';
import { InlineSVGModule } from 'ng-inline-svg';
import { JeeCustomerModule } from 'src/app/pages/jee-customer.module';
import { TranslateModule } from '@ngx-translate/core';

import { NhanVienManagementService } from './Services/nhan-vien-management.service';
import { NhanVienManagementListComponent } from './nhan-vien-management-list/nhan-vien-management-list.component';
import { NhanVienManagementEditDialogComponent } from './nhan-vien-management-edit-dialog/nhan-vien-management-edit-dialog.component';
import { NhanVienImportDialogComponent } from './nhan-vien-import-dialog/nhan-vien-import-dialog.component';
import { NhanVienManagementComponent } from './nhan-vien-management.component';

const routes: Routes = [
  {
    path: '',
    component: NhanVienManagementComponent,
    children: [
      {
        path: '',
        component: NhanVienManagementListComponent,
      },
    ],
  },
];

@NgModule({
  declarations: [
    NhanVienManagementEditDialogComponent,
    NhanVienImportDialogComponent,
    NhanVienManagementListComponent,
    NhanVienManagementComponent,
  ],
  imports: [
    CommonModule,
    RouterModule.forChild(routes),
    JeeCustomerModule,
    NgxMatSelectSearchModule,
    InlineSVGModule,
    TranslateModule,
  ],
  providers: [
    NhanVienManagementService,
    {
      provide: MAT_DIALOG_DEFAULT_OPTIONS,
      useValue: { hasBackdrop: true, height: 'auto', width: '900px' },
    },
  ],
  entryComponents: [
    NhanVienManagementEditDialogComponent,
    NhanVienImportDialogComponent,
    NhanVienManagementListComponent,
    NhanVienManagementComponent,
  ],
})
export class NhanVienManagementModule {}
