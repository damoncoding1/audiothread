/// <reference lib="webworker" />

import {
  validateAudioFileData,
  type AudioFileValidationWorkerRequest,
  type AudioFileValidationWorkerResponse,
} from './validation';

self.onmessage = (event: MessageEvent<AudioFileValidationWorkerRequest>) => {
  const startedAt = performance.now();
  const response: AudioFileValidationWorkerResponse = {
    requestId: event.data.requestId,
    result: validateAudioFileData(event.data),
    durationMilliseconds: performance.now() - startedAt,
  };

  self.postMessage(response);
};

export {};
