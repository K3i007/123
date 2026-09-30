import Link from "next/link";
import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Página no encontrada (404)",
  description: "El recurso o vehículo solicitado no está disponible.",
};

export default function NotFound() {
  return (
    <section className="mx-auto max-w-xl py-20 text-center space-y-6">
      <span className="inline-block text-6xl font-black text-brand tracking-tight">404</span>
      <h1 className="text-3xl font-black text-slate-900 sm:text-4xl">
        Página o vehículo no encontrado
      </h1>
      <p className="text-base text-slate-600 max-w-md mx-auto leading-relaxed">
        El enlace que intentaste abrir no existe, cambió de dirección o el vehículo ya no se
        encuentra en el inventario publicado.
      </p>

      <div className="flex flex-wrap items-center justify-center gap-4 pt-4">
        <Link
          href="/catalogo"
          className="rounded-xl bg-accent-600 px-6 py-3 text-sm font-bold text-white shadow-sm hover:bg-accent-700 transition-colors focus:outline-none focus:ring-2 focus:ring-accent-600"
        >
          Explorar catálogo de autos
        </Link>
        <Link
          href="/"
          className="rounded-xl border border-slate-300 bg-white px-6 py-3 text-sm font-semibold text-slate-700 shadow-sm hover:bg-slate-50 transition-colors focus:outline-none focus:ring-2 focus:ring-brand"
        >
          Ir a la página principal
        </Link>
      </div>
    </section>
  );
}
