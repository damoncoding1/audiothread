export const ALLOWED_EXTENSIONS = ['.mp3', '.wav'] as const;
export const MAX_FILE_SIZE_IN_BYTES = 50 * 1024 * 1024;
export const AUDIO_HEADER_BYTES = 64 * 1024;

export const ACCEPT_ATTRIBUTE = '.mp3,.wav,audio/mpeg,audio/wav,audio/x-wav';

export type FileValidationResult = { valid: true } | { valid: false; message: string };

export interface AudioFileValidationInput {
  fileName: string;
  fileSize: number;
  contentType: string;
  header: ArrayBuffer;
}

export interface AudioFileValidationWorkerRequest extends AudioFileValidationInput {
  requestId: string;
}

export interface AudioFileValidationWorkerResponse {
  requestId: string;
  result: FileValidationResult;
  durationMilliseconds: number;
}

export type AudioValidationMode = 'sequential' | 'worker';

export interface AudioValidationMeasurement {
  mode: AudioValidationMode;
  result: FileValidationResult;
  totalDurationMilliseconds: number;
  validationDurationMilliseconds: number;
  validationContext: 'main-thread' | 'worker';
}

function getExtension(fileName: string): (typeof ALLOWED_EXTENSIONS)[number] | null {
  const lowerCaseName = fileName.toLowerCase();

  return ALLOWED_EXTENSIONS.find((extension) => lowerCaseName.endsWith(extension)) ?? null;
}

function isWav(bytes: Uint8Array): boolean {
  return (
    bytes.length >= 12 &&
    bytes[0] === 0x52 &&
    bytes[1] === 0x49 &&
    bytes[2] === 0x46 &&
    bytes[3] === 0x46 &&
    bytes[8] === 0x57 &&
    bytes[9] === 0x41 &&
    bytes[10] === 0x56 &&
    bytes[11] === 0x45
  );
}

function isMp3(bytes: Uint8Array): boolean {
  const hasId3Header =
    bytes.length >= 3 && bytes[0] === 0x49 && bytes[1] === 0x44 && bytes[2] === 0x33;

  if (hasId3Header) {
    return true;
  }

  if (bytes.length < 3) {
    return false;
  }

  const hasFrameSync = bytes[0] === 0xff && (bytes[1] & 0xe0) === 0xe0;
  const hasValidVersion = (bytes[1] & 0x18) !== 0x08;
  const hasValidLayer = (bytes[1] & 0x06) !== 0;
  const bitrateIndex = (bytes[2] & 0xf0) >> 4;
  const sampleRateIndex = (bytes[2] & 0x0c) >> 2;

  return (
    hasFrameSync &&
    hasValidVersion &&
    hasValidLayer &&
    bitrateIndex !== 0 &&
    bitrateIndex !== 0x0f &&
    sampleRateIndex !== 0x03
  );
}

export function validateAudioFileData(input: AudioFileValidationInput): FileValidationResult {
  const extension = getExtension(input.fileName);

  if (!extension) {
    return {
      valid: false,
      message: `Solo se permiten archivos ${ALLOWED_EXTENSIONS.join(' y ')}.`,
    };
  }

  if (input.fileSize === 0) {
    return { valid: false, message: 'El archivo esta vacio.' };
  }

  if (input.fileSize > MAX_FILE_SIZE_IN_BYTES) {
    return { valid: false, message: 'El archivo supera el maximo de 50 MB.' };
  }

  const allowedContentTypes =
    extension === '.mp3' ? ['audio/mpeg', 'audio/mp3'] : ['audio/wav', 'audio/wave', 'audio/x-wav'];

  if (input.contentType && !allowedContentTypes.includes(input.contentType.toLowerCase())) {
    return {
      valid: false,
      message: `El tipo declarado del archivo no corresponde a ${extension}.`,
    };
  }

  const header = new Uint8Array(input.header);
  const hasExpectedSignature = extension === '.wav' ? isWav(header) : isMp3(header);

  if (!hasExpectedSignature) {
    return {
      valid: false,
      message: `El contenido del archivo no corresponde a un audio ${extension.toUpperCase()}.`,
    };
  }

  return { valid: true };
}

export async function measureAudioFileValidation(
  file: File,
  mode: AudioValidationMode,
): Promise<AudioValidationMeasurement> {
  const startedAt = performance.now();
  const header = await file.slice(0, AUDIO_HEADER_BYTES).arrayBuffer();

  if (mode === 'sequential') {
    const validationStartedAt = performance.now();
    const result = validateAudioFileData({
      fileName: file.name,
      fileSize: file.size,
      contentType: file.type,
      header,
    });

    return {
      mode,
      result,
      totalDurationMilliseconds: performance.now() - startedAt,
      validationDurationMilliseconds: performance.now() - validationStartedAt,
      validationContext: 'main-thread',
    };
  }

  const requestId = crypto.randomUUID();
  const worker = new Worker(new URL('./audioValidation.worker.ts', import.meta.url), {
    type: 'module',
  });

  return new Promise<AudioValidationMeasurement>((resolve) => {
    const finish = (result: FileValidationResult, durationMilliseconds: number) => {
      worker.terminate();
      resolve({
        mode,
        result,
        totalDurationMilliseconds: performance.now() - startedAt,
        validationDurationMilliseconds: durationMilliseconds,
        validationContext: 'worker',
      });
    };

    worker.onmessage = (event: MessageEvent<AudioFileValidationWorkerResponse>) => {
      if (event.data.requestId === requestId) {
        finish(event.data.result, event.data.durationMilliseconds);
      }
    };

    worker.onerror = () => {
      finish({
        valid: false,
        message: 'No se pudo validar el archivo en segundo plano.',
      }, 0);
    };

    const request: AudioFileValidationWorkerRequest = {
      requestId,
      fileName: file.name,
      fileSize: file.size,
      contentType: file.type,
      header,
    };

    worker.postMessage(request, [header]);
  });
}

export async function validateAudioFile(file: File): Promise<FileValidationResult> {
  const mode = env.audioValidationMode;
  const measureName = `audio-validation-${mode}`;
  const measurementId = crypto.randomUUID();
  const startMark = `${measureName}-${measurementId}-start`;
  const endMark = `${measureName}-${measurementId}-end`;
  const supportsUserTiming =
    typeof performance.mark === 'function' && typeof performance.measure === 'function';

  if (supportsUserTiming) {
    performance.mark(startMark);
  }

  try {
    const measurement = await measureAudioFileValidation(file, mode);
    return measurement.result;
  } finally {
    if (supportsUserTiming) {
      performance.mark(endMark);
      performance.measure(measureName, startMark, endMark);
      performance.clearMarks(startMark);
      performance.clearMarks(endMark);
    }
  }
}
import { env } from '../../shared/config/env';
