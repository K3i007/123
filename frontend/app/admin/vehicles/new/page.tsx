"use client";
import { useState } from "react";
export default function NewVehiclePage() {
  const [section, setSection] = useState(1);
  const sections = [
    "Datos principales",
    "Características",
    "Precio y equipamiento",
    "Ubicación",
    "Campos adicionales",
    "Resumen",
  ];
  return (
    <section className="max-w-3xl">
      <h1 className="text-3xl font-bold">Alta de vehículo</h1>
      <nav className="mt-6 flex flex-wrap gap-2" aria-label="Secciones de alta">
        {sections.map((label, index) => (
          <button
            className={`rounded border px-3 py-2 ${section === index + 1 ? "bg-ink text-white" : "bg-white"}`}
            onClick={() => setSection(index + 1)}
            type="button"
            key={label}
          >
            {index + 1}. {label}
          </button>
        ))}
      </nav>
      <div className="mt-7 space-y-5 rounded-lg bg-white p-6 shadow-sm">
        {section === 1 && (
          <>
            <label className="block">
              Marca
              <input className="mt-1 w-full rounded border p-2" />
            </label>
            <label className="block">
              Modelo
              <input className="mt-1 w-full rounded border p-2" />
            </label>
            <label className="block">
              Año
              <input className="mt-1 w-full rounded border p-2" type="number" />
            </label>
          </>
        )}
        {section === 2 && <p>Transmisión, combustible, tracción y carrocería.</p>}
        {section === 3 && (
          <label className="block">
            Precio
            <input className="mt-1 w-full rounded border p-2" type="number" />
          </label>
        )}
        {section === 4 && (
          <label className="block">
            Sucursal
            <select className="mt-1 w-full rounded border p-2">
              <option>Selecciona una sucursal</option>
            </select>
          </label>
        )}
        {section === 5 && <p>Los campos configurables aparecerán según el tipo de vehículo.</p>}
        {section === 6 && (
          <>
            <p>Fotografías e inspección estarán disponibles próximamente.</p>
            <p>La publicación mostrará requisitos pendientes.</p>
          </>
        )}
        <div className="flex gap-3">
          <button className="rounded border px-4 py-2" type="button">
            Guardar borrador
          </button>
          <button className="rounded bg-brand px-4 py-2 text-white" type="button">
            Continuar
          </button>
        </div>
      </div>
    </section>
  );
}
