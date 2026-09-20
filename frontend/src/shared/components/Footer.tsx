export function Footer() {
  return (
    <footer className="border-separator mt-16 border-t pt-6 pb-10">
      <div className="text-muted flex flex-wrap items-center justify-between gap-3 text-xs">
        <p>
          Compresion con FFmpeg, transcripcion con Whisper y resumen con Qwen, todo ejecutado
          localmente en el servidor.
        </p>
        <p className="numeric text-faint">Programacion 7 · Jala University</p>
      </div>
    </footer>
  );
}
