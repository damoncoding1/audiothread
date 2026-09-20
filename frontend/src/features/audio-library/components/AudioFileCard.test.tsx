import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { AudioFileListItem } from '../../../api/types';
import { AudioFileCard } from './AudioFileCard';

const audioFile: AudioFileListItem = {
  id: '24da6ca7-1ca0-4b44-a8dd-cff81da4206b',
  originalFileName: 'entrevista.wav',
  contentType: 'audio/aac',
  sizeInBytes: 2048,
  url: 'https://example.test/compressed.aac',
  originalUrl: 'https://example.test/original.wav',
  filteredUrl: 'https://example.test/filtered.wav',
  summary: 'Resumen de la entrevista',
  status: 'Summarized',
  createdAtUtc: '2026-09-06T00:00:00Z',
};

describe('AudioFileCard', () => {
  it('shows independent players for the original and noise-reduced audio', () => {
    render(<AudioFileCard audioFile={audioFile} />);

    expect(screen.getByText('Audio original')).toBeInTheDocument();
    expect(screen.getByText('Audio con ruido reducido')).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: /reproducir entrevista\.wav, original/i }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: /reproducir entrevista\.wav, con ruido reducido/i }),
    ).toBeInTheDocument();
  });

  it('keeps old records usable when the filtered version does not exist', () => {
    render(<AudioFileCard audioFile={{ ...audioFile, originalUrl: null, filteredUrl: null }} />);

    expect(
      screen.getByText('La version filtrada no esta disponible para este audio.'),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: /reproducir entrevista\.wav, original/i }),
    ).toBeInTheDocument();
  });
});
