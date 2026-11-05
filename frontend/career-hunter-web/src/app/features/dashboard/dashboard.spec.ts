import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { Dashboard } from './dashboard';
import { DigestItemDetail, DigestSummary } from '../../core/models';

const DIGEST: DigestSummary = {
  id: 'digest-1',
  digestDate: '2026-11-05',
  status: 'Pending',
  itemCount: 2,
  attemptCount: 0,
  sentAt: null,
};

function item(overrides: Partial<DigestItemDetail>): DigestItemDetail {
  return {
    id: 'item-1',
    jobEvaluationId: 'eval-1',
    jobOpportunityId: 'job-1',
    rank: 1,
    jobTitle: 'Tech Lead .NET',
    employerName: 'Empresa Exemplo',
    score: 86,
    recommendation: 'StrongMatch',
    jobUrl: 'https://example.com/jobs/1',
    location: 'São Paulo, SP',
    modality: 'Remote',
    contractType: 'Clt',
    seniority: 'Lead',
    compensation: {
      minAmount: 10000,
      maxAmount: 12000,
      currency: 'BRL',
      period: 'Monthly',
      isEstimated: false,
      isKnown: true,
    },
    description: 'Vaga para liderar o time de modernização .NET na nuvem.',
    publishedAt: '2026-11-04T12:00:00+00:00',
    sourceName: 'Adzuna BR',
    requirements: [{ name: '.NET', category: 'Technology', isRequired: true }],
    ...overrides,
  };
}

describe('Dashboard', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Dashboard],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function render(items: DigestItemDetail[]): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(Dashboard);
    fixture.detectChanges();

    http.expectOne('/api/digests?take=20').flush([DIGEST]);
    fixture.detectChanges();

    http.expectOne(`/api/digests/${DIGEST.id}/items`).flush(items);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    return fixture.nativeElement as HTMLElement;
  }

  it('renders the digest items with the stored job detail', async () => {
    const compiled = await render([item({}), item({ id: 'item-2', rank: 2, jobTitle: 'Engenheiro .NET Sênior', score: 81 })]);

    const cards = compiled.querySelectorAll('.job-card');
    expect(cards.length).toBe(2);

    // Detail pane: score, title, salary from the API and the original link.
    expect(compiled.querySelector('h2')?.textContent).toContain('Tech Lead .NET');
    expect(compiled.querySelector('.score.big')?.textContent?.trim()).toBe('86');
    expect(compiled.querySelector('.meta-grid')?.textContent).toContain('BRL 10.000 – 12.000/mês');
    expect(compiled.querySelector('.description-body')?.textContent).toContain('modernização .NET');

    const link = compiled.querySelector<HTMLAnchorElement>('a.primary');
    expect(link?.href).toBe('https://example.com/jobs/1');
    expect(link?.target).toBe('_blank');
  });

  it('keeps null fields empty instead of inventing values', async () => {
    const compiled = await render([
      item({
        modality: null,
        contractType: null,
        seniority: null,
        compensation: null,
        publishedAt: null,
        description: '',
      }),
    ]);

    const labels = Array.from(compiled.querySelectorAll('.meta-grid dt')).map((node) => node.textContent);
    expect(labels).not.toContain('Modelo');
    expect(labels).not.toContain('Contrato');
    expect(labels).not.toContain('Faixa salarial');
    expect(labels).not.toContain('Publicada em');
    expect(compiled.querySelector('.description-body')?.textContent).toContain('Sem descrição armazenada');
  });

  it('filters the list by free text', async () => {
    const fixture = TestBed.createComponent(Dashboard);
    fixture.detectChanges();
    http.expectOne('/api/digests?take=20').flush([DIGEST]);
    fixture.detectChanges();
    http.expectOne(`/api/digests/${DIGEST.id}/items`).flush([
      item({}),
      item({ id: 'item-2', jobTitle: 'Engenheiro Mobile Júnior', employerName: 'Outra Empresa' }),
    ]);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const input = fixture.nativeElement as HTMLElement;
    const search = input.querySelector<HTMLInputElement>('input.search')!;
    search.value = 'mobile';
    search.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    const titles = Array.from(input.querySelectorAll('.card-title')).map((node) => node.textContent);
    expect(titles.length).toBe(1);
    expect(titles[0]).toContain('Mobile');
  });

  it('shows the empty-digest message when the day has no opportunity', async () => {
    const compiled = await render([]);

    expect(compiled.querySelector('.empty')?.textContent).toContain(
      'Nenhuma oportunidade atingiu a pontuação mínima',
    );
    expect(compiled.querySelector('.placeholder')).not.toBeNull();
  });
});
