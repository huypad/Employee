import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { LayoutComponent } from './_layout/layout.component';

const routes: Routes = [
  {
    path: '',
    component: LayoutComponent,
    children: [
      {
        path: 'builder',
        loadChildren: () => import('./builder/builder.module').then((m) => m.BuilderModule),
      },
      {
        path: 'dashboard',
        loadChildren: () =>
          import('./JeeBeginner/page-girdters-dashboard/page-girdters-dashboard.module').then((m) => m.PageGirdtersDashboardModule),
      },
      {
        path: 'Management/CustomerManagement',
        loadChildren: () =>
          import('./JeeBeginner/Management/CustomerManagement/customer-management.module').then((m) => m.CustomerManagementModule),
      },
      {
        path: 'Management/NhanVienManagement',
        loadChildren: () =>
          import('./JeeBeginner/Management/NhanVienManagement/nhan-vien-management.module').then(
            (m) => m.NhanVienManagementModule
          ),
      },
      {
        path: 'Management/PartnerManagement',
        loadChildren: () =>
          import('./JeeBeginner/Management/PartnerManagement/partner-management.module').then((m) => m.PartnerManagementModule),
      },
      {
        path: 'Management/AccountManagement',
        loadChildren: () =>
          import('./JeeBeginner/Management/AccountManagement/account-management.module').then((m) => m.AccountManagementModule),
      },
      {
        path: 'Abc',
        loadChildren: () =>
          import('./JeeBeginner/Management/AccountManagement/account-management.module').then((m) => m.AccountManagementModule),
      },
      {
        path: '',
        redirectTo: '/Management/CustomerManagement',
        pathMatch: 'full',
      },
      {
        path: '**',
        redirectTo: 'error/404',
      },
    ],
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class PagesRoutingModule { }
