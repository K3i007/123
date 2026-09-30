import Link from "next/link";
import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Centro de Notificaciones",
  description: "Alertas de búsqueda y avisos de interés.",
};

export default function NotificacionesPage() {
  return (
    <div className="mx-auto max-w-xl py-16 text-center space-y-6">
      <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-full bg-amber-50 text-accent-600">
        <svg className="h-8 w-8" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            strokeWidth={2}
            d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9"
          />
        </svg>
      </div>

      <span className="inline-block rounded-full bg-slate-200 px-3 py-1 text-xs font-semibold text-slate-700">
        Próximamente en Fases 4 y 8
      </span>

      <h1 className="text-3xl font-black text-slate-900">Centro de Notificaciones</h1>

      <p className="text-sm text-slate-600 leading-relaxed">
        Aquí recibirás avisos inmediatos sobre bajas de precio en vehículos guardados, alertas de
        búsqueda cuando ingrese un auto de tu interés y confirmaciones de citas.
      </p>

      <div className="pt-4">
        <Link
          href="/catalogo"
          className="inline-block rounded-xl bg-accent-600 px-6 py-2.5 text-sm font-bold text-white shadow-sm hover:bg-accent-700 transition-colors"
        >
          Volver al catálogo
        </Link>
      </div>
    </div>
  );
}
