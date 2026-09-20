import { useCallback, useEffect, useRef, useState } from 'react';
import { listAudioFiles } from '../../../api/audioFilesClient';
import type { AudioFileListItem } from '../../../api/types';

export type AudioFilesState =
  | { status: 'loading' }
  | { status: 'ready'; items: AudioFileListItem[]; total: number; skip: number; take: number }
  | { status: 'error'; message: string };

export const PAGE_SIZE = 6;

function isAbortError(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError';
}

export function useAudioFiles() {
  const [state, setState] = useState<AudioFilesState>({ status: 'loading' });
  const [skip, setSkip] = useState(0);
  const abortControllerRef = useRef<AbortController | null>(null);

  const load = useCallback(async (nextSkip: number) => {
    abortControllerRef.current?.abort();

    const controller = new AbortController();
    abortControllerRef.current = controller;
    setState({ status: 'loading' });

    try {
      const response = await listAudioFiles({
        skip: nextSkip,
        take: PAGE_SIZE,
        signal: controller.signal,
      });

      setState({
        status: 'ready',
        items: response.items,
        total: response.total,
        skip: response.skip,
        take: response.take,
      });
    } catch (error) {
      if (isAbortError(error)) {
        return;
      }

      setState({
        status: 'error',
        message: error instanceof Error ? error.message : 'No se pudo cargar la lista de audios.',
      });
    }
  }, []);

  useEffect(() => {
    void load(skip);

    return () => abortControllerRef.current?.abort();
  }, [load, skip]);

  const refresh = useCallback(() => {
    setSkip(0);
    void load(0);
  }, [load]);

  const goToPage = useCallback((nextSkip: number) => {
    setSkip(Math.max(0, nextSkip));
  }, []);

  return { state, refresh, goToPage };
}
