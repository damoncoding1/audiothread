import type { AudioFileListItem } from '../../../api/types';
import { formatBytes, formatDateTime } from '../../../shared/format';
import { AudioPlayer } from './AudioPlayer';

interface AudioFileCardProps {
  audioFile: AudioFileListItem;
}

export function AudioFileCard({ audioFile }: AudioFileCardProps) {
  return (
    <li className="bg-raised shadow-panel hover:shadow-lifted rounded-[--radius-panel] p-5 transition-shadow duration-300">
      <div className="flex items-baseline justify-between gap-3">
        <p className="text-muted truncate text-xs">{audioFile.originalFileName}</p>
        <span className="numeric text-faint shrink-0 text-[0.6875rem]">
          {formatDateTime(audioFile.createdAtUtc)}
        </span>
      </div>

      <p className="font-display mt-3 text-lg leading-snug tracking-tight">
        {audioFile.summary ?? 'Sin resumen disponible'}
      </p>

      <div className="numeric text-faint mt-4 flex flex-wrap items-center gap-x-3 gap-y-1 text-[0.6875rem] tracking-wide uppercase">
        <span>{formatBytes(audioFile.sizeInBytes)}</span>
        <span aria-hidden="true">·</span>
        <span>{audioFile.contentType.replace('audio/', '')}</span>
      </div>

      <div className="mt-4 grid gap-3">
        <div>
          <p className="text-muted mb-1.5 text-xs font-medium">Audio original</p>
          <AudioPlayer
            url={audioFile.originalUrl ?? audioFile.url}
            fileName={`${audioFile.originalFileName}, original`}
          />
        </div>

        <div>
          <p className="text-muted mb-1.5 text-xs font-medium">Audio con ruido reducido</p>
          <AudioPlayer
            url={audioFile.filteredUrl}
            fileName={`${audioFile.originalFileName}, con ruido reducido`}
            unavailableMessage="La version filtrada no esta disponible para este audio."
          />
        </div>
      </div>
    </li>
  );
}
