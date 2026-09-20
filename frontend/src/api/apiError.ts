import type { ProblemDetails } from './types';

export class ApiError extends Error {
  readonly status: number;
  readonly details: string[];

  constructor(message: string, status: number, details: string[] = []) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.details = details;
  }
}

export class NetworkError extends Error {
  constructor(
    message = 'No se pudo conectar con el servidor. Verifica que la API este ejecutandose.',
  ) {
    super(message);
    this.name = 'NetworkError';
  }
}

const STATUS_MESSAGES: Record<number, string> = {
  400: 'El archivo enviado no es valido.',
  404: 'No se encontro el audio solicitado.',
  413: 'El archivo supera el tamano maximo permitido.',
  500: 'El servidor no pudo procesar el audio.',
};

function extractDetails(problem: ProblemDetails): string[] {
  if (!problem.errors) {
    return problem.detail ? [problem.detail] : [];
  }

  return Object.values(problem.errors).flat();
}

export async function toApiError(response: Response): Promise<ApiError> {
  const fallbackMessage =
    STATUS_MESSAGES[response.status] ?? `La API respondio con el estado ${response.status}.`;

  try {
    const problem = (await response.json()) as ProblemDetails;
    const details = extractDetails(problem);

    return new ApiError(problem.title ?? fallbackMessage, response.status, details);
  } catch {
    return new ApiError(fallbackMessage, response.status);
  }
}
