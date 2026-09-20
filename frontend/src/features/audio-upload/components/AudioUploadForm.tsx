import { useState, type FormEvent } from 'react';
import type { AudioFileResponse } from '../../../api/types';
import { Alert } from '../../../shared/components/Alert';
import { useAudioUploadJob } from '../hooks/useAudioUploadJob';
import { validateAudioFile } from '../validation';
import { FileDropzone } from './FileDropzone';
import { UploadingIndicator } from './UploadingIndicator';
import { UploadErrorMessage, UploadSuccessMessage } from './UploadResultMessage';

interface AudioUploadFormProps {
  onUploaded?: (result: AudioFileResponse) => void;
}

export function AudioUploadForm({ onUploaded }: AudioUploadFormProps) {
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [validationMessage, setValidationMessage] = useState<string | null>(null);
  const [validatingFileName, setValidatingFileName] = useState<string | null>(null);
  const { state, upload, cancel, reset } = useAudioUploadJob({ onCompleted: onUploaded });

  const isUploading = state.status === 'uploading' || state.status === 'processing';
  const isValidating = validatingFileName !== null;
  const isBusy = isValidating || isUploading;

  async function handleFileSelected(file: File | null): Promise<boolean> {
    reset();
    setSelectedFile(null);
    setValidationMessage(null);

    if (!file) {
      return true;
    }

    setValidatingFileName(file.name);
    let validation;

    try {
      validation = await validateAudioFile(file);
    } catch {
      setValidationMessage('No se pudo leer el archivo para validarlo.');
      return false;
    } finally {
      setValidatingFileName(null);
    }

    if (!validation.valid) {
      setValidationMessage(validation.message);
      return false;
    }

    setSelectedFile(file);
    return true;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!selectedFile || isBusy) {
      return;
    }

    await upload(selectedFile);
  }

  return (
    <section id="procesar" className="scroll-mt-8">
      <form onSubmit={handleSubmit}>
        <FileDropzone
          selectedFile={selectedFile}
          disabled={isBusy}
          onFileSelected={handleFileSelected}
        />

        {isValidating ? (
          <p className="text-muted mt-3 text-sm" role="status">
            Validando {validatingFileName} en segundo plano...
          </p>
        ) : null}

        <button
          type="submit"
          disabled={!selectedFile || isBusy}
          className="bg-ink text-raised mt-5 w-full rounded-[--radius-control] px-5 py-3 text-sm font-semibold transition-opacity duration-200 hover:opacity-85 disabled:cursor-not-allowed disabled:opacity-30 sm:w-auto"
        >
          {isValidating ? 'Validando...' : isUploading ? 'Procesando...' : 'Subir audio'}
        </button>
      </form>

      <div className="mt-6 space-y-5">
        {validationMessage ? <Alert tone="error" title={validationMessage} /> : null}

        {state.status === 'uploading' ? (
          <UploadingIndicator fileName={state.fileName} onCancel={cancel} />
        ) : null}

        {state.status === 'processing' ? (
          <UploadingIndicator fileName={state.fileName} job={state.job} onCancel={cancel} />
        ) : null}

        {state.status === 'success' ? <UploadSuccessMessage result={state.result} /> : null}

        {state.status === 'error' ? (
          <UploadErrorMessage message={state.message} details={state.details} />
        ) : null}
      </div>
    </section>
  );
}
