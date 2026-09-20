import type {
  AudioProcessingJobSnapshot,
  AudioProcessingStepName,
  AudioProcessingStepSnapshot,
  AudioProcessingStepState,
} from '../../../api/types';

const STEP_LABELS: Record<AudioProcessingStepName, string> = {
  compression: 'Comprimiendo a AAC',
  transcription: 'Transcribiendo la voz',
  summarization: 'Generando el resumen',
  'noise-reduction': 'Reduciendo ruido con IA',
  storage: 'Subiendo al almacenamiento',
  database: 'Guardando la metadata',
};

const PARALLEL_STEPS: AudioProcessingStepName[] = [
  'compression',
  'transcription',
  'summarization',
  'noise-reduction',
];
const SEQUENTIAL_STEPS: AudioProcessingStepName[] = ['storage', 'database'];

const FILL_BY_STATE: Record<AudioProcessingStepState, string> = {
  Pending: 'w-0',
  Running: 'w-2/3 animate-pulse',
  Completed: 'w-full',
  Failed: 'w-full',
};

const COLOR_BY_STATE: Record<AudioProcessingStepState, string> = {
  Pending: 'bg-separator',
  Running: 'bg-running',
  Completed: 'bg-done',
  Failed: 'bg-failed',
};

const TEXT_BY_STATE: Record<AudioProcessingStepState, string> = {
  Pending: 'text-faint',
  Running: 'text-ink',
  Completed: 'text-muted',
  Failed: 'text-failed',
};

function StepStatus({ step }: { step: AudioProcessingStepSnapshot }) {
  if (step.durationMilliseconds !== null) {
    return <span className="numeric text-muted text-xs">{step.durationMilliseconds} ms</span>;
  }

  if (step.state === 'Running') {
    return <span className="text-running text-xs font-medium">en curso</span>;
  }

  if (step.state === 'Failed') {
    return <span className="text-failed text-xs font-medium">fallo</span>;
  }

  return <span className="text-faint text-xs">en espera</span>;
}

function Track({ step }: { step: AudioProcessingStepSnapshot }) {
  return (
    <div>
      <div className="flex items-baseline justify-between gap-4">
        <span className={`text-sm transition-colors duration-300 ${TEXT_BY_STATE[step.state]}`}>
          {STEP_LABELS[step.step]}
        </span>
        <StepStatus step={step} />
      </div>

      <div className="bg-sunken mt-2 h-1 overflow-hidden rounded-full">
        <div
          className={`h-full rounded-full transition-[width] duration-700 ease-out ${FILL_BY_STATE[step.state]} ${COLOR_BY_STATE[step.state]}`}
        />
      </div>
    </div>
  );
}

interface ProcessingStepperProps {
  job: AudioProcessingJobSnapshot;
}

export function ProcessingStepper({ job }: ProcessingStepperProps) {
  const byName = new Map(job.steps.map((step) => [step.step, step]));
  const pick = (names: AudioProcessingStepName[]) =>
    names
      .map((name) => byName.get(name))
      .filter((step): step is AudioProcessingStepSnapshot => step !== undefined);

  const parallel = pick(PARALLEL_STEPS);
  const sequential = pick(SEQUENTIAL_STEPS);

  return (
    <div data-testid="processing-stepper" className="mt-6">
      <p className="eyebrow">Procesamiento paralelo</p>

      <div className="mt-4 space-y-4">
        {parallel.map((step) => (
          <Track key={step.step} step={step} />
        ))}
      </div>

      <div className="my-5 flex items-center gap-3">
        <span aria-hidden="true" className="bg-separator h-px flex-1" />
        <span className="text-faint text-[0.6875rem] tracking-wide uppercase">
          al converger
        </span>
        <span aria-hidden="true" className="bg-separator h-px flex-1" />
      </div>

      <div className="space-y-4">
        {sequential.map((step) => (
          <Track key={step.step} step={step} />
        ))}
      </div>
    </div>
  );
}
