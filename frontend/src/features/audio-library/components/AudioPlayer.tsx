import { useRef, useState } from 'react';

interface AudioPlayerProps {
  url: string | null;
  fileName: string;
  unavailableMessage?: string;
}

function formatTime(seconds: number): string {
  if (!Number.isFinite(seconds)) {
    return '0:00';
  }

  const minutes = Math.floor(seconds / 60);
  const remaining = Math.floor(seconds % 60);

  return `${minutes}:${remaining.toString().padStart(2, '0')}`;
}

export function AudioPlayer({ url, fileName, unavailableMessage }: AudioPlayerProps) {
  const audioRef = useRef<HTMLAudioElement>(null);
  const [isPlaying, setIsPlaying] = useState(false);
  const [progress, setProgress] = useState(0);
  const [duration, setDuration] = useState(0);

  if (!url) {
    return (
      <p className="text-faint text-xs">
        {unavailableMessage ?? 'Sin URL publica. Configura el bucket como publico para reproducirlo.'}
      </p>
    );
  }

  function toggle() {
    const audio = audioRef.current;

    if (!audio) {
      return;
    }

    if (audio.paused) {
      void audio.play();
      setIsPlaying(true);
      return;
    }

    audio.pause();
    setIsPlaying(false);
  }

  return (
    <div className="bg-sunken flex items-center gap-3 rounded-[--radius-control] px-3 py-2">
      <button
        type="button"
        onClick={toggle}
        aria-label={isPlaying ? `Pausar ${fileName}` : `Reproducir ${fileName}`}
        className="bg-ink text-raised grid size-8 shrink-0 place-items-center rounded-full transition-opacity hover:opacity-85"
      >
        <svg aria-hidden="true" viewBox="0 0 12 12" className="size-3" fill="currentColor">
          {isPlaying ? (
            <path d="M3 2h2v8H3zM7 2h2v8H7z" />
          ) : (
            <path d="M3.5 2.2 9.5 6l-6 3.8z" />
          )}
        </svg>
      </button>

      <div className="bg-separator h-1 flex-1 overflow-hidden rounded-full">
        <div
          className="bg-ink h-full rounded-full transition-[width] duration-200"
          style={{ width: duration > 0 ? `${(progress / duration) * 100}%` : '0%' }}
        />
      </div>

      <span className="numeric text-muted shrink-0 text-xs">
        {formatTime(duration - progress)}
      </span>

      <audio
        ref={audioRef}
        src={url}
        preload="metadata"
        onLoadedMetadata={(event) => setDuration(event.currentTarget.duration)}
        onTimeUpdate={(event) => setProgress(event.currentTarget.currentTime)}
        onEnded={() => {
          setIsPlaying(false);
          setProgress(0);
        }}
        className="hidden"
      />
    </div>
  );
}
