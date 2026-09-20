import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError, NetworkError } from './apiError';
import { uploadAudioFile } from './audioFilesClient';

function createAudioFile() {
  return new File(['contenido'], 'entrevista.wav', { type: 'audio/wav' });
}

describe('audioFilesClient', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('envia el archivo como multipart bajo la clave file', async () => {
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockResolvedValue(
      new Response(JSON.stringify({ id: 'abc' }), {
        status: 201,
        headers: { 'Content-Type': 'application/json' },
      }),
    );

    await uploadAudioFile(createAudioFile());

    const [, requestInit] = fetchMock.mock.calls[0];
    const body = requestInit?.body as FormData;

    expect(requestInit?.method).toBe('POST');
    expect(body).toBeInstanceOf(FormData);
    expect((body.get('file') as File).name).toBe('entrevista.wav');
  });

  it('convierte una respuesta de validacion en un ApiError con los detalles', async () => {
    vi.mocked(fetch).mockResolvedValue(
      new Response(
        JSON.stringify({
          title: 'El archivo enviado no es valido.',
          errors: { file: ['La extension no esta permitida.'] },
        }),
        { status: 400, headers: { 'Content-Type': 'application/json' } },
      ),
    );

    const error = await uploadAudioFile(createAudioFile()).catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(400);
    expect((error as ApiError).details).toEqual(['La extension no esta permitida.']);
  });

  it('convierte una falla de red en un NetworkError', async () => {
    vi.mocked(fetch).mockRejectedValue(new TypeError('Failed to fetch'));

    const error = await uploadAudioFile(createAudioFile()).catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(NetworkError);
  });
});
