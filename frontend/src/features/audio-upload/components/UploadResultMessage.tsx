import type { AudioFileResponse } from '../../../api/types';
import { Alert } from '../../../shared/components/Alert';
import { formatBytes } from '../../../shared/format';

interface SuccessMessageProps {
  result: AudioFileResponse;
}

export function UploadSuccessMessage({ result }: SuccessMessageProps) {
  return (
    <Alert tone="success" title="Audio subido y procesado">
      <blockquote className="font-display border-done border-l-2 pl-4 text-lg leading-snug tracking-tight">
        {result.summary ?? 'Sin resumen disponible'}
      </blockquote>

      <dl className="mt-5 grid grid-cols-2 gap-x-6 gap-y-4 sm:grid-cols-4">
        <div>
          <dt className="eyebrow">Archivo</dt>
          <dd className="mt-1 truncate text-sm">{result.originalFileName}</dd>
        </div>
        <div>
          <dt className="eyebrow">Comprimido</dt>
          <dd className="numeric mt-1 text-sm">{formatBytes(result.sizeInBytes)}</dd>
        </div>
        <div>
          <dt className="eyebrow">Formato</dt>
          <dd className="numeric mt-1 text-sm">{result.contentType}</dd>
        </div>
        <div className="min-w-0">
          <dt className="eyebrow">Identificador</dt>
          <dd className="numeric mt-1 truncate text-xs">{result.id}</dd>
        </div>
      </dl>
    </Alert>
  );
}

interface ErrorMessageProps {
  message: string;
  details: string[];
}

export function UploadErrorMessage({ message, details }: ErrorMessageProps) {
  return (
    <Alert tone="error" title={message}>
      {details.length > 0 ? (
        <ul className="text-muted space-y-1">
          {details.map((detail) => (
            <li key={detail}>{detail}</li>
          ))}
        </ul>
      ) : null}
    </Alert>
  );
}
