const DEFAULT_API_BASE_URL = 'http://localhost:5270';
const DEFAULT_AUDIO_VALIDATION_MODE = 'worker';

function readApiBaseUrl(): string {
  const configured = import.meta.env.VITE_API_BASE_URL?.trim();
  const baseUrl = configured && configured.length > 0 ? configured : DEFAULT_API_BASE_URL;

  return baseUrl.endsWith('/') ? baseUrl.slice(0, -1) : baseUrl;
}

function readAudioValidationMode(): 'sequential' | 'worker' {
  return import.meta.env.VITE_AUDIO_VALIDATION_MODE === 'sequential'
    ? 'sequential'
    : DEFAULT_AUDIO_VALIDATION_MODE;
}

export const env = {
  apiBaseUrl: readApiBaseUrl(),
  audioValidationMode: readAudioValidationMode(),
} as const;
