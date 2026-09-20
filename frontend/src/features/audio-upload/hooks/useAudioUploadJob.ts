import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiError, NetworkError } from '../../../api/apiError';
import { buildJobEventsUrl, createAudioProcessingJob } from '../../../api/audioFilesClient';
import type { AudioFileResponse, AudioProcessingJobSnapshot } from '../../../api/types';

export type UploadJobState =
  | { status: 'idle' }
  | { status: 'uploading'; fileName: string }
  | { status: 'processing'; fileName: string; job: AudioProcessingJobSnapshot }
  | { status: 'success'; job: AudioProcessingJobSnapshot; result: AudioFileResponse }
  | { status: 'error'; message: string; details: string[]; job?: AudioProcessingJobSnapshot };

interface UseAudioUploadJobOptions {
  onCompleted?: (result: AudioFileResponse) => void;
}

function toErrorState(error: unknown): UploadJobState {
  if (error instanceof ApiError) {
    return { status: 'error', message: error.message, details: error.details };
  }

  if (error instanceof NetworkError) {
    return { status: 'error', message: error.message, details: [] };
  }

  return {
    status: 'error',
    message: 'Ocurrio un error inesperado al procesar el audio.',
    details: [],
  };
}

export function useAudioUploadJob({ onCompleted }: UseAudioUploadJobOptions = {}) {
  const [state, setState] = useState<UploadJobState>({ status: 'idle' });
  const abortControllerRef = useRef<AbortController | null>(null);
  const eventSourceRef = useRef<EventSource | null>(null);

  const closeEventSource = useCallback(() => {
    eventSourceRef.current?.close();
    eventSourceRef.current = null;
  }, []);

  useEffect(() => {
    return () => {
      abortControllerRef.current?.abort();
      eventSourceRef.current?.close();
    };
  }, []);

  const watchJob = useCallback(
    (jobId: string, fileName: string) => {
      closeEventSource();

      const eventSource = new EventSource(buildJobEventsUrl(jobId));
      eventSourceRef.current = eventSource;

      eventSource.onmessage = (event) => {
        const job = JSON.parse(event.data) as AudioProcessingJobSnapshot;

        if (job.status === 'Completed' && job.result) {
          closeEventSource();
          setState({ status: 'success', job, result: job.result });
          onCompleted?.(job.result);
          return;
        }

        if (job.status === 'Failed') {
          closeEventSource();
          setState({
            status: 'error',
            message: job.errorMessage ?? 'El servidor no pudo procesar el audio.',
            details: [],
            job,
          });
          return;
        }

        setState({ status: 'processing', fileName, job });
      };

      eventSource.onerror = () => {
        if (eventSource.readyState === EventSource.CLOSED) {
          setState({
            status: 'error',
            message: 'Se perdio la conexion con el servidor mientras se procesaba el audio.',
            details: [],
          });
        }
      };
    },
    [closeEventSource, onCompleted],
  );

  const upload = useCallback(
    async (file: File) => {
      abortControllerRef.current?.abort();
      closeEventSource();

      const controller = new AbortController();
      abortControllerRef.current = controller;
      setState({ status: 'uploading', fileName: file.name });

      try {
        const job = await createAudioProcessingJob(file, { signal: controller.signal });

        setState({ status: 'processing', fileName: file.name, job });
        watchJob(job.id, file.name);
      } catch (error) {
        if (error instanceof DOMException && error.name === 'AbortError') {
          setState({ status: 'idle' });
          return;
        }

        setState(toErrorState(error));
      } finally {
        if (abortControllerRef.current === controller) {
          abortControllerRef.current = null;
        }
      }
    },
    [closeEventSource, watchJob],
  );

  const cancel = useCallback(() => {
    abortControllerRef.current?.abort();
    closeEventSource();
    setState({ status: 'idle' });
  }, [closeEventSource]);

  const reset = useCallback(() => {
    abortControllerRef.current?.abort();
    closeEventSource();
    setState({ status: 'idle' });
  }, [closeEventSource]);

  return { state, upload, cancel, reset };
}
