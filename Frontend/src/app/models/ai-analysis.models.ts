import { Urgency } from './sla.models';

/** Ticket.AiStatus (Backend AiStatus). Skipped: no analysis was requested (the AI was unavailable at the opening). */
export type AiStatus = 'Pending' | 'Processing' | 'Completed' | 'Failed' | 'Skipped';

/** Backend AiValidationVerdict of a stored analysis (an Invalid response is never stored). */
export type AiVerdict = 'Valid' | 'LowConfidence' | 'UnknownCategory';

/** Backend AiAnalysisDisposition: applied to the ticket, or a recommendation only – and why. */
export type AiDisposition = 'Applied' | 'LowConfidence' | 'UnknownCategory' | 'ManuallyClassified' | 'AlreadyRouted';

export type AiConfidenceLevel = 'Low' | 'Medium' | 'High';

/** GET /api/tickets/{id}/ai-analysis – processing (Backend AiProcessingDto). */
export interface AiProcessing {
  status: AiStatus;
  errorCode: string | null;
  /** The error (or Skipped) in Hebrew, from the server. */
  errorMessage: string | null;
  requestedAt: string | null;
  nextAttemptAt: string | null;
}

/** GET /api/tickets/{id}/ai-analysis – the stored analysis (Backend AiAnalysisDetailsDto). */
export interface AiAnalysisDetails {
  categoryCode: string;
  categoryName: string | null;
  /** Only when the category is active – what "החל המלצה" sends. */
  categoryId: number | null;
  urgency: Urgency;
  /** 0..1 */
  confidence: number;
  confidenceLevel: AiConfidenceLevel;
  suggestedSubject: string;
  summary: string;
  missingInformation: string[];
  suggestedSolution: string;
  model: string;
  promptVersion: string;
  createdAt: string;
  verdict: AiVerdict;
  disposition: AiDisposition;
  applied: boolean;
  matchesCurrentClassification: boolean;
  /** Exactly what was sent to the AI, after PII redaction. */
  redactedInput: string;
}

/** GET /api/tickets/{id}/ai-analysis (Backend TicketAiAnalysisDto). Staff only. */
export interface TicketAiAnalysis {
  processing: AiProcessing;
  /** The latest stored analysis; a failed or running re-analysis keeps the previous one. */
  analysis: AiAnalysisDetails | null;
}

/** An analysis is waiting or running. */
export function isAnalysisInProgress(status: AiStatus): boolean {
  return status === 'Pending' || status === 'Processing';
}
