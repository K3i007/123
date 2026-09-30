"use client";

import { useState } from "react";

export function VehicleActions() {
  const [message, setMessage] = useState<string | null>(null);
  const action = (label: string) =>
    setMessage(
      `${label} estará disponible próximamente. Inicia sesión cuando esta función esté habilitada.`,
    );
  return (
    <section aria-label="Acciones del vehículo" className="border-y border-slate-200 py-4">
      <div className="flex flex-wrap gap-2">
        {[
          "Favorito",
          "Comparar",
          "Solicitar información",
          "Solicitar cotización",
          "Prueba de manejo",
        ].map((label) => (
          <button
            key={label}
            type="button"
            onClick={() => action(label)}
            className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm font-semibold hover:border-brand"
          >
            {label}
          </button>
        ))}
      </div>
      {message && (
        <p role="status" className="mt-3 text-sm text-slate-700">
          {message}
        </p>
      )}
    </section>
  );
}
