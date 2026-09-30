export function VehiclePlaceholder({ alt, className = "" }: { alt: string; className?: string }) {
  return (
    <div
      role="img"
      aria-label={alt}
      className={`relative flex aspect-video w-full items-center justify-center overflow-hidden bg-gradient-to-br from-slate-100 via-slate-200 to-slate-300 ${className}`}
    >
      <svg
        className="h-20 w-32 text-slate-400/80 transition-transform duration-300 group-hover:scale-105"
        fill="currentColor"
        viewBox="0 0 24 24"
        aria-hidden="true"
      >
        <path d="M18.92 6.01C18.72 5.42 18.16 5 17.5 5h-11c-.66 0-1.21.42-1.42 1.01L3 12v8c0 .55.45 1 1 1h1c.55 0 1-.45 1-1v-1h12v1c0 .55.45 1 1 1h1c.55 0 1-.45 1-1v-8l-2.08-5.99zM6.85 7h10.29l1.04 3H5.81l1.04-3zM19 17H5v-4.66l.12-.34h13.77l.11.34V17z" />
        <circle cx="7.5" cy="14.5" r="1.5" />
        <circle cx="16.5" cy="14.5" r="1.5" />
      </svg>
      <span className="absolute bottom-2 right-2 rounded bg-slate-900/60 px-2 py-0.5 text-[10px] font-medium tracking-wider text-slate-100 backdrop-blur-sm">
        FOTO PRÓXIMAMENTE
      </span>
    </div>
  );
}
