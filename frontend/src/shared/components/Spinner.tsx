interface SpinnerProps {
  label?: string;
  className?: string;
}

export function Spinner({ label, className = 'size-4' }: SpinnerProps) {
  return (
    <span
      role="status"
      aria-live="polite"
      aria-label={label ?? 'Cargando'}
      className={`inline-block ${className} animate-spin rounded-full border-2 border-current border-t-transparent opacity-60`}
    />
  );
}
