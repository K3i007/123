import Link from "next/link";
import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Área de Clientes",
  description: "Perfil de cliente, favoritos y cotizaciones guardadas.",
};

export default function PerfilPage() {
  return (
    <div className="mx-auto max-w-xl py-16 text-center space-y-6">
      <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-full bg-teal-50 text-brand">
        <svg className="h-8 w-8" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            strokeWidth={2}
            d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z"
          />
        </svg>
      </div>

      <span className="inline-block rounded-full bg-slate-200 px-3 py-1 text-xs font-semibold text-slate-700">
        Próximamente en Fase 4
      </span>

      <h1 className="text-3xl font-black text-slate-900">Área de Clientes y Perfil</h1>

      <p className="text-sm text-slate-600 leading-relaxed">
        En la Fase 4 podrás gestionar tu cuenta, consultar tus autos favoritos, comparar vehículos
        lado a lado y revisar el estado de tus cotizaciones y citas de manejo.
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
