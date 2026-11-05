/**
 * Espelhos dos DTOs da API (docs/api-endpoints.md). Nada é inventado aqui: campos que a
 * API devolve como null continuam null na interface.
 */

export type DigestStatus = 'Pending' | 'Sent' | 'Failed';

export type EvaluationRecommendation =
  | 'StrongMatch'
  | 'Match'
  | 'PartialMatch'
  | 'WeakMatch'
  | 'NoMatch';

export interface DigestSummary {
  id: string;
  digestDate: string;
  status: DigestStatus;
  itemCount: number;
  attemptCount: number;
  sentAt: string | null;
}

export interface JobCompensation {
  minAmount: number | null;
  maxAmount: number | null;
  currency: string;
  period: string;
  isEstimated: boolean;
  isKnown: boolean;
}

export interface JobRequirement {
  name: string;
  category: string;
  isRequired: boolean;
}

export interface DigestItemDetail {
  id: string;
  jobEvaluationId: string;
  jobOpportunityId: string;
  rank: number;
  jobTitle: string;
  employerName: string;
  score: number;
  recommendation: EvaluationRecommendation;
  jobUrl: string;
  location: string;
  modality: string | null;
  contractType: string | null;
  seniority: string | null;
  compensation: JobCompensation | null;
  description: string;
  publishedAt: string | null;
  sourceName: string | null;
  requirements: JobRequirement[];
}
