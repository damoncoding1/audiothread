import type { AudioProcessingJobSnapshot } from '../../../api/types';
import { Spinner } from '../../../shared/components/Spinner';
import { ProcessingStepper } from './ProcessingStepper';

interface UploadingIndicatorProps {
  fileName: string;
  job?: AudioProcessingJobSnapshot;
  onCancel?: () => void;
}

export function UploadingIndicator({ fileName, job, onCancel }: UploadingIndicatorProps) {
  const isQueued = job === undefined || job.status === 'Queued';

  return (
    <div
      data-testid="uploading-indicator"
      className="bg-raised shadow-panel rounded-[--radius-panel] p-6"
    >
      <div className="flex items-start gap-4">
        <Spinner label="Procesando audio" className="text-running mt-1 size-4" />

        <div className="min-w-0 flex-1">
          <p className="font-display text-[0.9375rem] leading-snug font-semibold tracking-tight">
            {isQueued ? 'Subiendo el archivo' : 'Procesando el audio en el servidor'}
          </p>
          <p className="text-muted mt-0.5 truncate text-sm">{fileName}</p>
        </div>

        {onCancel ? (
          <button
            type="button"
            onClick={onCancel}
            className="text-muted hover:text-ink shrink-0 text-xs font-medium transition-colors"
          >
            Cancelar
          </button>
        ) : null}
      </div>

      {job ? <ProcessingStepper job={job} /> : null}
    </div>
  );
}
