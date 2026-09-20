import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/apiError';
import type {
  AudioFileResponse,
  AudioProcessingJobSnapshot,
  AudioProcessingJobStatus,
  AudioProcessingStepSnapshot,
} from '../../../api/types';
import { AudioUploadForm } from './AudioUploadForm';

vi.mock('../../../api/audioFilesClient', () => ({
  createAudioProcessingJob: vi.fn(),
  buildJobEventsUrl: (jobId: string) =>
    `http://localhost:5270/api/audio-files/jobs/${jobId}/events`,
}));

vi.mock('../validation', async (importOriginal) => {
  const original = await importOriginal<typeof import('../validation')>();

  return {
    ...original,
    validateAudioFile: vi.fn(),
  };
});

const { createAudioProcessingJob } = await import('../../../api/audioFilesClient');
const { validateAudioFile } = await import('../validation');
const createJobMock = vi.mocked(createAudioProcessingJob);
const validateAudioFileMock = vi.mocked(validateAudioFile);

class FakeEventSource {
  static instances: FakeEventSource[] = [];
  static readonly CLOSED = 2;

  onmessage: ((event: MessageEvent) => void) | null = null;
  onerror: ((event: Event) => void) | null = null;
  readyState = 0;
  url: string;

  constructor(url: string) {
    this.url = url;
    FakeEventSource.instances.push(this);
  }

  close() {
    this.readyState = FakeEventSource.CLOSED;
  }

  emit(snapshot: AudioProcessingJobSnapshot) {
    this.onmessage?.({ data: JSON.stringify(snapshot) } as MessageEvent);
  }

  static latest() {
    return FakeEventSource.instances.at(-1);
  }
}

function createDeferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason: unknown) => void;

  const promise = new Promise<T>((resolvePromise, rejectPromise) => {
    resolve = resolvePromise;
    reject = rejectPromise;
  });

  return { promise, resolve, reject };
}

const JOB_ID = '4f1c2b3a-0d5e-4a6b-8c7d-9e0f1a2b3c4d';

const successResult: AudioFileResponse = {
  id: '8b1f0f1a-1c2d-4e3f-9a8b-7c6d5e4f3a2b',
  originalFileName: 'entrevista.wav',
  contentType: 'audio/aac',
  sizeInBytes: 1729851,
  storageBucket: 'audio-files',
  storagePath: 'uploads/8b1f/entrevista.aac',
  url: null,
  originalUrl: null,
  filteredUrl: null,
  summary: 'Lee consents to Confederate surrender.',
  createdAtUtc: '2026-08-02T18:00:00Z',
};

function createJob(
  status: AudioProcessingJobStatus,
  steps: Partial<Record<string, AudioProcessingStepSnapshot['state']>> = {},
  overrides: Partial<AudioProcessingJobSnapshot> = {},
): AudioProcessingJobSnapshot {
  const names = [
    'compression',
    'transcription',
    'summarization',
    'noise-reduction',
    'storage',
    'database',
  ] as const;

  return {
    id: JOB_ID,
    originalFileName: 'entrevista.wav',
    status,
    steps: names.map((step) => ({
      step,
      state: steps[step] ?? 'Pending',
      durationMilliseconds: null,
    })),
    result: null,
    errorMessage: null,
    createdAtUtc: '2026-08-02T18:00:00Z',
    completedAtUtc: null,
    ...overrides,
  };
}

function emitJob(snapshot: AudioProcessingJobSnapshot) {
  act(() => {
    FakeEventSource.latest()!.emit(snapshot);
  });
}

function createAudioFile(name = 'entrevista.wav') {
  return new File(['contenido-de-audio'], name, { type: 'audio/wav' });
}

async function selectFile(file: File, { applyAccept = true } = {}) {
  const user = userEvent.setup({ applyAccept });
  await user.upload(screen.getByLabelText(/archivo de audio/i), file);

  return user;
}

describe('AudioUploadForm', () => {
  beforeEach(() => {
    createJobMock.mockReset();
    validateAudioFileMock.mockReset();
    validateAudioFileMock.mockResolvedValue({ valid: true });
    FakeEventSource.instances = [];
    vi.stubGlobal('EventSource', FakeEventSource);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.clearAllMocks();
  });

  it('muestra el componente de carga mientras el archivo se esta subiendo', async () => {
    const deferred = createDeferred<AudioProcessingJobSnapshot>();
    createJobMock.mockReturnValue(deferred.promise);

    render(<AudioUploadForm />);
    const user = await selectFile(createAudioFile());

    expect(screen.queryByTestId('uploading-indicator')).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /subir audio/i }));

    const indicator = await screen.findByTestId('uploading-indicator');
    expect(indicator).toBeInTheDocument();
    expect(within(indicator).getByRole('status')).toBeInTheDocument();
    expect(within(indicator).getByText('entrevista.wav')).toBeInTheDocument();

    await act(async () => {
      deferred.resolve(createJob('Queued'));
    });

    expect(screen.getByTestId('uploading-indicator')).toBeInTheDocument();
  });

  it('muestra las etapas reales que reporta el servidor', async () => {
    createJobMock.mockResolvedValue(createJob('Queued'));

    render(<AudioUploadForm />);
    const user = await selectFile(createAudioFile());
    await user.click(screen.getByRole('button', { name: /subir audio/i }));

    await waitFor(() => expect(FakeEventSource.latest()).toBeDefined());

    emitJob(createJob('Processing', { compression: 'Completed', transcription: 'Running' }));

    const stepper = await screen.findByTestId('processing-stepper');
    expect(within(stepper).getByText(/comprimiendo a aac/i)).toBeInTheDocument();
    expect(within(stepper).getByText(/transcribiendo la voz/i)).toBeInTheDocument();
  });

  it('oculta el componente de carga y muestra el resumen cuando el trabajo termina', async () => {
    createJobMock.mockResolvedValue(createJob('Queued'));

    render(<AudioUploadForm />);
    const user = await selectFile(createAudioFile());
    await user.click(screen.getByRole('button', { name: /subir audio/i }));

    await waitFor(() => expect(FakeEventSource.latest()).toBeDefined());

    emitJob(createJob('Completed', {}, { result: successResult }));

    expect(await screen.findByText(/audio subido y procesado/i)).toBeInTheDocument();
    expect(screen.getByText(successResult.summary as string)).toBeInTheDocument();
    expect(screen.queryByTestId('uploading-indicator')).not.toBeInTheDocument();
  });

  it('muestra el mensaje de error cuando el trabajo falla en el servidor', async () => {
    createJobMock.mockResolvedValue(createJob('Queued'));

    render(<AudioUploadForm />);
    const user = await selectFile(createAudioFile());
    await user.click(screen.getByRole('button', { name: /subir audio/i }));

    await waitFor(() => expect(FakeEventSource.latest()).toBeDefined());

    emitJob(createJob('Failed', {}, { errorMessage: 'El modelo no esta disponible.' }));

    expect(await screen.findByText(/el modelo no esta disponible/i)).toBeInTheDocument();
    expect(screen.queryByTestId('uploading-indicator')).not.toBeInTheDocument();
  });

  it('muestra el mensaje de error cuando la API rechaza el archivo', async () => {
    createJobMock.mockRejectedValue(
      new ApiError('El archivo enviado no es valido.', 400, ['La extension no esta permitida.']),
    );

    render(<AudioUploadForm />);
    const user = await selectFile(createAudioFile());
    await user.click(screen.getByRole('button', { name: /subir audio/i }));

    expect(await screen.findByText(/el archivo enviado no es valido/i)).toBeInTheDocument();
    expect(screen.getByText(/la extension no esta permitida/i)).toBeInTheDocument();
  });

  it('deshabilita el boton mientras el trabajo esta en curso', async () => {
    const deferred = createDeferred<AudioProcessingJobSnapshot>();
    createJobMock.mockReturnValue(deferred.promise);

    render(<AudioUploadForm />);
    const user = await selectFile(createAudioFile());
    const submitButton = screen.getByRole('button', { name: /subir audio/i });

    expect(submitButton).toBeEnabled();

    await user.click(submitButton);

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /procesando/i })).toBeDisabled();
    });

    await act(async () => {
      deferred.resolve(createJob('Queued'));
    });
  });

  it('rechaza archivos con extension no permitida sin llamar a la API', async () => {
    validateAudioFileMock.mockResolvedValue({
      valid: false,
      message: 'Solo se permiten archivos .mp3 y .wav.',
    });

    render(<AudioUploadForm />);
    await selectFile(new File(['x'], 'documento.pdf', { type: 'application/pdf' }), {
      applyAccept: false,
    });

    expect(await screen.findByText(/solo se permiten archivos/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /subir audio/i })).toBeDisabled();
    expect(createJobMock).not.toHaveBeenCalled();
  });

  it('mantiene bloqueada la subida mientras el Web Worker valida el archivo', async () => {
    const deferred = createDeferred<Awaited<ReturnType<typeof validateAudioFile>>>();
    validateAudioFileMock.mockReturnValue(deferred.promise);

    render(<AudioUploadForm />);
    void selectFile(createAudioFile());

    expect(
      await screen.findByText(/validando entrevista.wav en segundo plano/i),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /validando/i })).toBeDisabled();

    await act(async () => deferred.resolve({ valid: true }));

    expect(await screen.findByRole('button', { name: /subir audio/i })).toBeEnabled();
  });

  it('bloquea un archivo cuyo contenido no coincide con su extension', async () => {
    validateAudioFileMock.mockResolvedValue({
      valid: false,
      message: 'El contenido del archivo no corresponde a un audio .WAV.',
    });

    render(<AudioUploadForm />);
    await selectFile(createAudioFile());

    expect(await screen.findByText(/contenido.*no corresponde/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/archivo de audio/i)).toHaveValue('');
    expect(screen.getByRole('button', { name: /subir audio/i })).toBeDisabled();
    expect(createJobMock).not.toHaveBeenCalled();
  });
});
