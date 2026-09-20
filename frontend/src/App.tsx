import { useCallback } from 'react';
import { AudioFileList } from './features/audio-library/components/AudioFileList';
import { useAudioFiles } from './features/audio-library/hooks/useAudioFiles';
import { AudioUploadForm } from './features/audio-upload/components/AudioUploadForm';
import { Footer } from './shared/components/Footer';
import { Sidebar } from './shared/components/Sidebar';

function App() {
  const { state, refresh, goToPage } = useAudioFiles();

  const handleUploaded = useCallback(() => {
    refresh();
  }, [refresh]);

  return (
    <div className="lg:flex">
      <Sidebar totalAudios={state.status === 'ready' ? state.total : null} />

      <div className="min-w-0 flex-1 lg:h-dvh lg:overflow-y-auto">
        <main className="mx-auto max-w-5xl px-6 py-10 lg:px-12 lg:py-16">
          <header>
            <p className="eyebrow">Procesamiento local</p>
            <h1 className="font-display text-display mt-3 font-semibold text-balance">
              De grabacion a resumen, sin salir de tu servidor.
            </h1>
            <p className="text-muted mt-4 max-w-xl text-base leading-relaxed">
              Subi una grabacion de voz. El servidor la comprime y la resume en dos hilos que
              trabajan al mismo tiempo, y aca vas viendo cada etapa mientras ocurre.
            </p>
          </header>

          <div className="mt-10">
            <AudioUploadForm onUploaded={handleUploaded} />
          </div>

          <AudioFileList state={state} onRetry={refresh} onPageChange={goToPage} />

          <Footer />
        </main>
      </div>
    </div>
  );
}

export default App;
