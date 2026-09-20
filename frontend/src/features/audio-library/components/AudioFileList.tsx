import { Alert } from '../../../shared/components/Alert';
import { AudioCardSkeleton } from '../../../shared/components/Skeleton';
import type { AudioFilesState } from '../hooks/useAudioFiles';
import { AudioFileCard } from './AudioFileCard';

interface AudioFileListProps {
  state: AudioFilesState;
  onRetry: () => void;
  onPageChange: (skip: number) => void;
}

function EmptyState() {
  return (
    <div className="border-separator rounded-[--radius-panel] border border-dashed px-6 py-16 text-center">
      <p className="font-display text-[0.9375rem] font-semibold tracking-tight">
        Todavia no hay audios procesados
      </p>
      <p className="text-muted mx-auto mt-2 max-w-sm text-sm">
        Subi una grabacion de voz y va a aparecer aca con su resumen, lista para escuchar.
      </p>
    </div>
  );
}

export function AudioFileList({ state, onRetry, onPageChange }: AudioFileListProps) {
  const isReady = state.status === 'ready';
  const currentPage = isReady ? Math.floor(state.skip / state.take) + 1 : 1;
  const totalPages = isReady ? Math.max(1, Math.ceil(state.total / state.take)) : 1;

  return (
    <section id="biblioteca" className="mt-16 scroll-mt-8">
      <div className="flex items-baseline justify-between gap-4">
        <h2 className="font-display text-title font-semibold">Biblioteca</h2>
        {isReady && state.total > 0 ? (
          <span className="numeric text-muted text-xs">
            {state.total} {state.total === 1 ? 'audio' : 'audios'}
          </span>
        ) : null}
      </div>

      <div className="mt-5">
        {state.status === 'loading' ? (
          <ul className="grid gap-4 sm:grid-cols-2">
            {Array.from({ length: 4 }, (_, index) => (
              <AudioCardSkeleton key={index} />
            ))}
          </ul>
        ) : null}

        {state.status === 'error' ? (
          <Alert tone="error" title={state.message}>
            <button
              type="button"
              onClick={onRetry}
              className="text-ink mt-1 text-sm font-medium underline underline-offset-4"
            >
              Reintentar
            </button>
          </Alert>
        ) : null}

        {isReady && state.items.length === 0 ? <EmptyState /> : null}

        {isReady && state.items.length > 0 ? (
          <>
            <ul className="grid gap-4 sm:grid-cols-2">
              {state.items.map((audioFile) => (
                <AudioFileCard key={audioFile.id} audioFile={audioFile} />
              ))}
            </ul>

            {totalPages > 1 ? (
              <nav
                aria-label="Paginacion de la biblioteca"
                className="mt-8 flex items-center justify-center gap-6"
              >
                <button
                  type="button"
                  onClick={() => onPageChange(state.skip - state.take)}
                  disabled={state.skip === 0}
                  className="text-muted hover:text-ink text-sm font-medium transition-colors disabled:opacity-30 disabled:hover:text-muted"
                >
                  Anterior
                </button>

                <span className="numeric text-faint text-xs">
                  {currentPage} de {totalPages}
                </span>

                <button
                  type="button"
                  onClick={() => onPageChange(state.skip + state.take)}
                  disabled={state.skip + state.take >= state.total}
                  className="text-muted hover:text-ink text-sm font-medium transition-colors disabled:opacity-30 disabled:hover:text-muted"
                >
                  Siguiente
                </button>
              </nav>
            ) : null}
          </>
        ) : null}
      </div>
    </section>
  );
}
