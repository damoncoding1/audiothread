import { env } from '../shared/config/env';
import { NetworkError, toApiError } from './apiError';
import type {
  AudioFileDetails,
  AudioFileListResponse,
  AudioFileResponse,
  AudioProcessingJobSnapshot,
} from './types';

const AUDIO_FILES_ENDPOINT = `${env.apiBaseUrl}/api/audio-files`;

interface RequestOptions {
  signal?: AbortSignal;
}

export interface ListAudioFilesOptions extends RequestOptions {
  skip?: number;
  take?: number;
}

async function send(request: () => Promise<Response>): Promise<Response> {
  let response: Response;

  try {
    response = await request();
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw error;
    }

    throw new NetworkError();
  }

  if (!response.ok) {
    throw await toApiError(response);
  }

  return response;
}

export async function uploadAudioFile(
  file: File,
  options: RequestOptions = {},
): Promise<AudioFileResponse> {
  const formData = new FormData();
  formData.append('file', file);

  const response = await send(() =>
    fetch(AUDIO_FILES_ENDPOINT, {
      method: 'POST',
      body: formData,
      signal: options.signal,
    }),
  );

  return (await response.json()) as AudioFileResponse;
}

export async function getAudioFileById(
  id: string,
  options: RequestOptions = {},
): Promise<AudioFileDetails> {
  const response = await send(() =>
    fetch(`${AUDIO_FILES_ENDPOINT}/${id}`, { signal: options.signal }),
  );

  return (await response.json()) as AudioFileDetails;
}

export async function createAudioProcessingJob(
  file: File,
  options: RequestOptions = {},
): Promise<AudioProcessingJobSnapshot> {
  const formData = new FormData();
  formData.append('file', file);

  const response = await send(() =>
    fetch(`${AUDIO_FILES_ENDPOINT}/jobs`, {
      method: 'POST',
      body: formData,
      signal: options.signal,
    }),
  );

  return (await response.json()) as AudioProcessingJobSnapshot;
}

export function buildJobEventsUrl(jobId: string): string {
  return `${AUDIO_FILES_ENDPOINT}/jobs/${jobId}/events`;
}

export async function listAudioFiles(
  options: ListAudioFilesOptions = {},
): Promise<AudioFileListResponse> {
  const { skip = 0, take = 20, signal } = options;
  const query = new URLSearchParams({ skip: String(skip), take: String(take) });

  const response = await send(() => fetch(`${AUDIO_FILES_ENDPOINT}?${query}`, { signal }));

  return (await response.json()) as AudioFileListResponse;
}
