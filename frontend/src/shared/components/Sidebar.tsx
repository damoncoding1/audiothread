interface SidebarProps {
  totalAudios: number | null;
}

export function Sidebar({ totalAudios }: SidebarProps) {
  return (
    <aside className="border-separator bg-raised flex shrink-0 flex-col border-b px-6 py-5 lg:h-dvh lg:w-64 lg:border-r lg:border-b-0 lg:py-8">
      <div className="flex items-center gap-3">
        <span
          aria-hidden="true"
          className="bg-ink text-raised grid size-8 place-items-center rounded-full text-sm font-bold"
        >
          A
        </span>
        <span className="font-display text-[0.9375rem] font-semibold tracking-tight">
          AudioThread
        </span>
      </div>

      <nav aria-label="Secciones" className="mt-8 hidden lg:block">
        <ul className="space-y-1">
          <li>
            <a
              href="#procesar"
              className="bg-sunken block rounded-[10px] px-3 py-2 text-sm font-medium"
            >
              Procesar audio
            </a>
          </li>
          <li>
            <a
              href="#biblioteca"
              className="text-muted hover:bg-sunken hover:text-ink block rounded-[10px] px-3 py-2 text-sm font-medium transition-colors"
            >
              Biblioteca
            </a>
          </li>
        </ul>
      </nav>

      <div className="mt-auto hidden lg:block">
        <p className="eyebrow">En la biblioteca</p>
        <p className="numeric mt-2 text-3xl font-medium tracking-tight">
          {totalAudios ?? '—'}
        </p>
        <p className="text-muted mt-1 text-xs">
          {totalAudios === 1 ? 'audio procesado' : 'audios procesados'}
        </p>
      </div>
    </aside>
  );
}
