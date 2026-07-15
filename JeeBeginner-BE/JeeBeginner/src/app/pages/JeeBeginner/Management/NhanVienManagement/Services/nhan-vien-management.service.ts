import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Injectable, OnDestroy } from '@angular/core';

import { HttpUtilsService } from '../../../_core/utils/http-utils.service';
import { environment } from '../../../../../../environments/environment';
import {
  GroupingState,
  ITableState,
  PaginatorState,
  SortState,
} from 'src/app/_metronic/shared/crud-table';

import { ITableService } from 'src/app/_metronic/core/services/itable.service';
import { NhanVienModelDTO, NhanVienModel } from '../Model/nhan-vien-management.model';

const API_PRODUCTS_URL = environment.ApiRoot + '/nhanvienmanagement';

const DEFAULT_STATE: ITableState = {
  filter: {},
  paginator: new PaginatorState(),
  sorting: new SortState(),
  searchTerm: '',
  grouping: new GroupingState(),
  entityId: undefined,
};

@Injectable()
export class NhanVienManagementService
  extends ITableService<NhanVienModelDTO[]>
  implements OnDestroy
{
  API_URL_FIND = API_PRODUCTS_URL + '/Get_DSNhanVien';
  API_URL_CTEATE = API_PRODUCTS_URL + '/CreateNhanVien';
  API_URL_EDIT = API_PRODUCTS_URL + '/UpdateNhanVien';
  API_URL_DELETE = API_PRODUCTS_URL + '/DeleteNhanVien';

  constructor(
    http: HttpClient,
    httpUtils: HttpUtilsService
  ) {
    super(http, httpUtils);
  }

  ngOnDestroy(): void {
    this.subscriptions.forEach((sb) => sb.unsubscribe());
  }

  getNhanVienById(id: number): Observable<any> {
    const httpHeaders = this.httpUtils.getHTTPHeaders();
    const url = API_PRODUCTS_URL + `/GetNhanVienById?id=${id}`;
    return this.http.get<any>(url, { headers: httpHeaders });
  }

  createNhanVien(item: NhanVienModel): Observable<any> {
    const httpHeaders = this.httpUtils.getHTTPHeaders();
    const url = API_PRODUCTS_URL + '/CreateNhanVien';
    return this.http.post<any>(url, item, { headers: httpHeaders });
  }

  importNhanVien(item: NhanVienModel): Observable<any> {
    const httpHeaders = this.httpUtils.getHTTPHeaders();
    const url = API_PRODUCTS_URL + '/ImportNhanVien';
    return this.http.post<any>(url, item, { headers: httpHeaders });
  }

  importNhanVienFromExcel(file: File): Observable<any> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    const headers = this.httpUtils.getHTTPHeaders().delete('Content-Type');
    return this.http.post<any>(API_PRODUCTS_URL + '/ImportNhanVienFromExcel', formData, { headers });
  }

  downloadNhanVienImportTemplate(): Observable<Blob> {
    const headers = this.httpUtils.getHTTPHeaders();
    return this.http.get(API_PRODUCTS_URL + '/DownloadNhanVienImportTemplate', { headers, responseType: 'blob' });
  }

  updateNhanVien(item: NhanVienModel): Observable<any> {
    const httpHeaders = this.httpUtils.getHTTPHeaders();
    const url = API_PRODUCTS_URL + '/UpdateNhanVien';
    return this.http.post<any>(url, item, { headers: httpHeaders });
  }

  deleteNhanVien(id: number): Observable<any> {
    const httpHeaders = this.httpUtils.getHTTPHeaders();
    const url = API_PRODUCTS_URL + `/DeleteNhanVien/${id}`;
    return this.http.get<any>(url, { headers: httpHeaders });
  }

  lockNhanVien(id: number): Observable<any> {
    const httpHeaders = this.httpUtils.getHTTPHeaders();
    const url = API_PRODUCTS_URL + `/UpdateLock/${id}`;
    return this.http.get<any>(url, { headers: httpHeaders });
  }

  unlockNhanVien(id: number): Observable<any> {
    const httpHeaders = this.httpUtils.getHTTPHeaders();
    const url = API_PRODUCTS_URL + `/UpdateUnLock/${id}`;
    return this.http.get<any>(url, { headers: httpHeaders });
  }
}
