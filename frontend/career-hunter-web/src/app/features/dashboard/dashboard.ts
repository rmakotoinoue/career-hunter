import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import {
  DigestItemDetail,
  DigestSummary,
  EvaluationRecommendation,
  JobCompensation,
} from '../../core/models';

/**
 * Vagas do dia: lista do digest selecionado + detalhe da vaga com a descrição armazenada.
 * O fluxo termina em "abrir vaga original" — a interface não envia candidatura.
 */
@Component({
  selector: 'app-dashboard',
  imports: [FormsModule],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard implements OnInit {
  private readonly api = inject(ApiService);

  protected readonly digests = signal<DigestSummary[]>([]);
  protected readonly selectedDigestId = signal<string | null>(null);
  protected readonly items = signal<DigestItemDetail[]>([]);
  protected readonly selectedItemId = signal<string | null>(null);
  protected readonly query = signal('');
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly digest = computed(
    () => this.digests().find((item) => item.id === this.selectedDigestId()) ?? null,
  );

  protected readonly filteredItems = computed(() => {
    const text = this.query().trim().toLowerCase();
    const all = this.items();
    if (!text) {
      return all;
    }

    return all.filter((item) =>
      [item.jobTitle, item.employerName, item.location, item.description].some((value) =>
        (value ?? '').toLowerCase().includes(text),
      ),
    );
  });

  protected readonly selectedItem = computed(() => {
    const selected = this.selectedItemId();
    return (
      this.items().find((item) => item.id === selected) ?? this.filteredItems()[0] ?? null
    );
  });

  ngOnInit(): void {
    this.loadDigests();
  }

  protected loadDigests(): void {
    this.loading.set(true);
    this.error.set(null);

    this.api.listDigests().subscribe({
      next: (digests) => {
        this.digests.set(digests);
        const latest = digests[0];
        if (latest) {
          this.selectDigest(latest.id);
        } else {
          this.items.set([]);
          this.loading.set(false);
        }
      },
      error: (response) => this.fail(response),
    });
  }

  protected selectDigest(id: string): void {
    if (this.selectedDigestId() === id && this.items().length > 0) {
      return;
    }

    this.selectedDigestId.set(id);
    this.selectedItemId.set(null);
    this.loading.set(true);
    this.error.set(null);

    this.api.getDigestItems(id).subscribe({
      next: (items) => {
        this.items.set(items);
        this.loading.set(false);
      },
      error: (response) => this.fail(response),
    });
  }

  protected selectItem(id: string): void {
    this.selectedItemId.set(id);
  }

  protected openPreview(): void {
    const id = this.selectedDigestId();
    if (id) {
      window.open(`/api/digests/${id}/preview`, '_blank', 'noopener,noreferrer');
    }
  }

  protected formatDate(dateOnly: string): string {
    const [year, month, day] = dateOnly.split('-');
    return year && month && day ? `${day}/${month}/${year}` : dateOnly;
  }

  protected formatPublishedAt(iso: string | null): string | null {
    if (!iso) {
      return null;
    }

    const date = new Date(iso);
    return Number.isNaN(date.getTime())
      ? null
      : date.toLocaleDateString('pt-BR', {
          day: '2-digit',
          month: '2-digit',
          year: 'numeric',
          timeZone: 'America/Sao_Paulo',
        });
  }

  protected salary(item: DigestItemDetail): string | null {
    const compensation = item.compensation;
    if (!compensation || !compensation.isKnown) {
      return null;
    }

    const range = formatRange(compensation);
    if (!range) {
      return null;
    }

    const suffix = compensation.isEstimated ? ' (estimado)' : '';
    return `${range}${suffix}`;
  }

  protected scoreClass(item: DigestItemDetail): string {
    if (item.score >= 80) {
      return 'score strong';
    }
    if (item.score >= 65) {
      return 'score match';
    }
    if (item.score >= 55) {
      return 'score partial';
    }
    return 'score weak';
  }

  protected recommendationLabel(recommendation: EvaluationRecommendation): string {
    switch (recommendation) {
      case 'StrongMatch':
        return 'Ótimo fit';
      case 'Match':
        return 'Compatível';
      case 'PartialMatch':
        return 'Parcial';
      case 'WeakMatch':
        return 'Fraco';
      default:
        return 'Sem fit';
    }
  }

  protected statusLabel(status: DigestSummary['status']): string {
    switch (status) {
      case 'Sent':
        return 'enviado';
      case 'Failed':
        return 'falhou';
      default:
        return 'pendente';
    }
  }

  protected modalityLabel(modality: string): string {
    return MODALITY[modality] ?? modality;
  }

  protected contractLabel(contract: string): string {
    return CONTRACT[contract] ?? contract;
  }

  protected seniorityLabel(seniority: string): string {
    return SENIORITY[seniority] ?? seniority;
  }

  protected categoryLabel(category: string): string {
    return CATEGORY[category] ?? category;
  }

  private fail(response: unknown): void {
    const status = (response as { status?: number } | null)?.status;
    this.error.set(
      status === 401
        ? 'A API recusou a chave (401). Confira o X-Api-Key no proxy.conf.json.'
        : status === 404
          ? 'Digest não encontrado (404). Gere o digest do dia antes de abrir o dashboard.'
          : 'Não foi possível falar com a API. A API está rodando na porta 5080?',
    );
    this.loading.set(false);
  }
}

function formatRange(compensation: JobCompensation): string | null {
  const { minAmount, maxAmount, currency } = compensation;
  const period = PERIOD[compensation.period] ?? '';

  if (minAmount !== null && maxAmount !== null) {
    return `${currency} ${money(minAmount)} – ${money(maxAmount)}${period}`;
  }

  const only = minAmount ?? maxAmount;
  return only !== null ? `${currency} ${money(only)}${period}` : null;
}

function money(value: number): string {
  return value.toLocaleString('pt-BR', { maximumFractionDigits: 0 });
}

const PERIOD: Record<string, string> = {
  Monthly: '/mês',
  Yearly: '/ano',
  Daily: '/dia',
  Hourly: '/h',
};

const MODALITY: Record<string, string> = {
  Remote: 'Remoto',
  Hybrid: 'Híbrido',
  Onsite: 'Presencial',
};

const CONTRACT: Record<string, string> = { Clt: 'CLT', Pj: 'PJ' };

const SENIORITY: Record<string, string> = {
  Intern: 'Estágio',
  Junior: 'Júnior',
  MidLevel: 'Pleno',
  Senior: 'Sênior',
  Lead: 'Liderança técnica',
  Principal: 'Principal',
  Architect: 'Arquiteto',
};

const CATEGORY: Record<string, string> = {
  Technology: 'Tecnologia',
  Experience: 'Experiência',
  Education: 'Formação',
  Language: 'Idioma',
  Certification: 'Certificação',
  Other: 'Outro',
};
