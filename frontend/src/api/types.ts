export interface AudioFileResponse {
  id: string;
  originalFileName: string;
  contentType: string;
  sizeInBytes: number;
  storageBucket: string;
  storagePath: string;
  url: string | null;
  originalUrl: string | null;
  filteredUrl: string | null;
  summary: string | null;
  createdAtUtc: string;
}

export interface AudioFileDetails extends AudioFileResponse {
  storedFileName: string;
  status: string;
  summarizedAtUtc: string | null;
}

export interface AudioFileListItem {
  id: string;
  originalFileName: string;
  contentType: string;
  sizeInBytes: number;
  url: string | null;
  originalUrl: string | null;
  filteredUrl: string | null;
  summary: string | null;
  status: string;
  createdAtUtc: string;
}

export interface AudioFileListResponse {
  items: AudioFileListItem[];
  total: number;
  skip: number;
  take: number;
}

export type AudioProcessingJobStatus = 'Queued' | 'Processing' | 'Completed' | 'Failed';

export type AudioProcessingStepState = 'Pending' | 'Running' | 'Completed' | 'Failed';

export type AudioProcessingStepName =
  | 'compression'
  | 'transcription'
  | 'summarization'
  | 'noise-reduction'
  | 'storage'
  | 'database';

export interface AudioProcessingStepSnapshot {
  step: AudioProcessingStepName;
  state: AudioProcessingStepState;
  durationMilliseconds: number | null;
}

export interface AudioProcessingJobSnapshot {
  id: string;
  originalFileName: string;
  status: AudioProcessingJobStatus;
  steps: AudioProcessingStepSnapshot[];
  result: AudioFileResponse | null;
  errorMessage: string | null;
  createdAtUtc: string;
  completedAtUtc: string | null;
}

export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}
