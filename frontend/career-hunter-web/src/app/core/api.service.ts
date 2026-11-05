import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { DigestItemDetail, DigestSummary } from './models';

/**
 * Chamadas usadas pelo dashboard. As chamadas são relativas (`/api/...`) e passam pelo proxy
 * do `ng serve`, que aponta para a API e injeta o `X-Api-Key` — a chave nunca entra no bundle.
 */
@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly base = '/api';

  constructor(private readonly http: HttpClient) {}

  listDigests(take = 20): Observable<DigestSummary[]> {
    return this.http.get<DigestSummary[]>(`${this.base}/digests`, { params: { take } });
  }

  getDigestItems(digestId: string): Observable<DigestItemDetail[]> {
    return this.http.get<DigestItemDetail[]>(`${this.base}/digests/${digestId}/items`);
  }
}
