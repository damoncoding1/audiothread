import { useId, useRef, useState, type ChangeEvent, type DragEvent } from 'react';
import { formatBytes } from '../../../shared/format';
import { ACCEPT_ATTRIBUTE, ALLOWED_EXTENSIONS, MAX_FILE_SIZE_IN_BYTES } from '../validation';

interface FileDropzoneProps {
  selectedFile: File | null;
  disabled: boolean;
  onFileSelected: (file: File | null) => boolean | Promise<boolean>;
}

export function FileDropzone({ selectedFile, disabled, onFileSelected }: FileDropzoneProps) {
  const inputId = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  const [isDraggingOver, setIsDraggingOver] = useState(false);

  async function handleChange(event: ChangeEvent<HTMLInputElement>) {
    const input = event.currentTarget;
    const accepted = await onFileSelected(input.files?.[0] ?? null);

    if (!accepted) {
      input.value = '';
    }
  }

  function handleDragOver(event: DragEvent<HTMLDivElement>) {
    event.preventDefault();

    if (!disabled) {
      setIsDraggingOver(true);
    }
  }

  function handleDragLeave(event: DragEvent<HTMLDivElement>) {
    event.preventDefault();
    setIsDraggingOver(false);
  }

  async function handleDrop(event: DragEvent<HTMLDivElement>) {
    event.preventDefault();
    setIsDraggingOver(false);

    if (disabled) {
      return;
    }

    const file = event.dataTransfer.files?.[0] ?? null;

    if (file && inputRef.current) {
      inputRef.current.files = event.dataTransfer.files;
    }

    const accepted = await onFileSelected(file);

    if (!accepted && inputRef.current) {
      inputRef.current.value = '';
    }
  }

  return (
    <div
      onDragOver={handleDragOver}
      onDragLeave={handleDragLeave}
      onDrop={handleDrop}
      data-dragging={isDraggingOver || undefined}
      className="border-separator data-[dragging]:border-running data-[dragging]:bg-sunken rounded-[--radius-panel] border border-dashed p-8 text-center transition-colors duration-200"
    >
      <label htmlFor={inputId} className="block cursor-pointer">
        <span className="sr-only">Archivo de audio</span>

        {selectedFile ? (
          <span className="block">
            <span className="font-display block text-[0.9375rem] font-semibold tracking-tight">
              {selectedFile.name}
            </span>
            <span className="numeric text-muted mt-1 block text-xs">
              {formatBytes(selectedFile.size)}
            </span>
            <span className="text-running mt-4 inline-block text-xs font-medium">
              Elegir otro archivo
            </span>
          </span>
        ) : (
          <span className="block">
            <span className="font-display block text-[0.9375rem] font-semibold tracking-tight">
              {isDraggingOver ? 'Solta el archivo' : 'Arrastra un audio o hace clic para elegirlo'}
            </span>
            <span className="numeric text-faint mt-2 block text-xs tracking-wide uppercase">
              {ALLOWED_EXTENSIONS.map((extension) => extension.slice(1)).join(' · ')} ·{' '}
              {formatBytes(MAX_FILE_SIZE_IN_BYTES)}
            </span>
          </span>
        )}
      </label>

      <input
        ref={inputRef}
        id={inputId}
        type="file"
        accept={ACCEPT_ATTRIBUTE}
        disabled={disabled}
        onChange={handleChange}
        className="sr-only"
      />
    </div>
  );
}
