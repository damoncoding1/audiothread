import { describe, expect, it } from 'vitest';
import { MAX_FILE_SIZE_IN_BYTES, validateAudioFileData } from './validation';

function wavHeader(): ArrayBuffer {
  const bytes = new Uint8Array(12);
  bytes.set([0x52, 0x49, 0x46, 0x46], 0);
  bytes.set([0x57, 0x41, 0x56, 0x45], 8);
  return bytes.buffer;
}

function mp3Header(): ArrayBuffer {
  return new Uint8Array([0x49, 0x44, 0x33, 0x04]).buffer;
}

describe('validateAudioFileData', () => {
  it('acepta un WAV cuya extension, MIME y firma binaria coinciden', () => {
    expect(
      validateAudioFileData({
        fileName: 'entrevista.wav',
        fileSize: 12,
        contentType: 'audio/wav',
        header: wavHeader(),
      }),
    ).toEqual({ valid: true });
  });

  it('acepta un MP3 con encabezado ID3', () => {
    expect(
      validateAudioFileData({
        fileName: 'entrevista.mp3',
        fileSize: 4,
        contentType: 'audio/mpeg',
        header: mp3Header(),
      }),
    ).toEqual({ valid: true });
  });

  it('rechaza un archivo renombrado como WAV sin firma RIFF/WAVE', () => {
    const result = validateAudioFileData({
      fileName: 'documento.wav',
      fileSize: 20,
      contentType: 'audio/wav',
      header: new Uint8Array([1, 2, 3, 4]).buffer,
    });

    expect(result.valid).toBe(false);
    expect(result.valid ? '' : result.message).toMatch(/contenido.*no corresponde/i);
  });

  it('rechaza archivos que superan 50 MB', () => {
    const result = validateAudioFileData({
      fileName: 'grande.mp3',
      fileSize: MAX_FILE_SIZE_IN_BYTES + 1,
      contentType: 'audio/mpeg',
      header: mp3Header(),
    });

    expect(result.valid).toBe(false);
    expect(result.valid ? '' : result.message).toMatch(/supera el maximo/i);
  });
});
