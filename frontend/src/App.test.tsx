import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import type { AudioFileResponse } from './api/types';
import App from './App';

const { refreshMock } = vi.hoisted(() => ({
  refreshMock: vi.fn(),
}));

vi.mock('./features/audio-library/hooks/useAudioFiles', () => ({
  useAudioFiles: () => ({
    state: { status: 'ready', items: [], total: 0 },
    refresh: refreshMock,
  }),
}));

vi.mock('./features/audio-library/components/AudioFileList', () => ({
  AudioFileList: () => <div>Biblioteca de audios</div>,
}));

vi.mock('./features/audio-upload/components/AudioUploadForm', () => ({
  AudioUploadForm: ({ onUploaded }: { onUploaded?: (result: AudioFileResponse) => void }) => (
    <button type="button" onClick={() => onUploaded?.({} as AudioFileResponse)}>
      Simular carga completada
    </button>
  ),
}));

describe('App', () => {
  it('actualiza la biblioteca cuando una nueva subida termina', async () => {
    refreshMock.mockReset();
    render(<App />);

    await userEvent.click(screen.getByRole('button', { name: /simular carga completada/i }));

    expect(refreshMock).toHaveBeenCalledOnce();
  });
});
