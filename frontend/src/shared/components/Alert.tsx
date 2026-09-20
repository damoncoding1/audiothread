import type { ReactNode } from 'react';

export type AlertTone = 'success' | 'error' | 'info';

interface AlertProps {
  tone: AlertTone;
  title: string;
  children?: ReactNode;
}

const TONE_COLOR: Record<AlertTone, string> = {
  success: 'text-done',
  error: 'text-failed',
  info: 'text-muted',
};

const TONE_MARK: Record<AlertTone, string> = {
  success: 'M2 6.2 4.6 8.8 10 3.4',
  error: 'M3 3l6 6M9 3l-6 6',
  info: 'M6 5.2v4M6 2.8v.6',
};

export function Alert({ tone, title, children }: AlertProps) {
  return (
    <div
      role="alert"
      className="bg-raised shadow-panel rounded-[--radius-panel] p-5 transition-all duration-300"
    >
      <div className="flex items-start gap-3">
        <svg
          aria-hidden="true"
          viewBox="0 0 12 12"
          className={`mt-0.5 size-4 shrink-0 ${TONE_COLOR[tone]}`}
          fill="none"
          stroke="currentColor"
          strokeWidth="1.75"
          strokeLinecap="round"
          strokeLinejoin="round"
        >
          <path d={TONE_MARK[tone]} />
        </svg>

        <div className="min-w-0 flex-1">
          <p className="font-display text-[0.9375rem] leading-snug font-semibold tracking-tight">
            {title}
          </p>
          {children ? <div className="mt-3 text-sm">{children}</div> : null}
        </div>
      </div>
    </div>
  );
}
