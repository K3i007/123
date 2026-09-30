import Link from "next/link";
import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Cómo comprar tu vehículo",
  description:
    "Proceso seguro, transparente y garantizado para adquirir tu próximo auto en Auto Premier.",
};

export default function ComprarPage() {
  const steps = [
    {
      num: "01",
      title: "Explora y filtra nuestro catálogo",
      desc: "Navega entre cientos de vehículos certificados con precios reales, kilometraje garantizado y filtros específicos por marca, modelo y presupuesto.",
    },
    {
      num: "02",
      title: "Agenda tu prueba de manejo",
      desc: "Selecciona el auto que te interese y agenda una cita en cualquiera de nuestras sucursales para conducirlo y revisarlo a detalle.",
    },
    {
      num: "03",
      title: "Cotización y financiamiento transparente",
      desc: "Elige entre pago de contado o planes de financiamiento con tasas preferenciales y enganches desde el 15%. Sin comisiones ocultas.",
    },
    {
      num: "04",
      title: "Inspección final y entrega inmediata",
      desc: "Revisamos contigo los puntos mecánicos y estéticos. Recibe tu factura, documentación en regla y garantía por escrito.",
    },
  ];

  return (
    <div className="mx-auto max-w-4xl py-8 space-y-12">
      <div className="text-center space-y-4">
        <span className="text-xs font-bold uppercase tracking-wider text-brand">
          GUÍA DE COMPRA
        </span>
        <h1 className="text-3xl font-black text-slate-900 sm:text-5xl">
          Comprar tu auto nunca fue tan fácil y seguro
        </h1>
        <p className="text-base text-slate-600 sm:text-lg max-w-2xl mx-auto">
          En Auto Premier todos los vehículos de nuestro inventario pertenecen a la concesionaria y
          han sido verificados legal y mecánicamente.
        </p>
      </div>

      <div className="grid grid-cols-1 gap-6 sm:grid-cols-2">
        {steps.map((s) => (
          <div key={s.num} className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
            <span className="text-2xl font-black text-brand">{s.num}</span>
            <h2 className="mt-2 text-xl font-bold text-slate-900">{s.title}</h2>
            <p className="mt-2 text-sm text-slate-600 leading-relaxed">{s.desc}</p>
          </div>
        ))}
      </div>

      <div className="rounded-2xl bg-slate-900 p-8 text-center text-white sm:p-12">
        <h2 className="text-2xl font-bold sm:text-3xl">¿Listo para encontrar tu nuevo auto?</h2>
        <p className="mt-2 text-sm text-slate-300 max-w-xl mx-auto">
          Ingresa a nuestro catálogo filtrable y encuentra las mejores ofertas con disponibilidad
          inmediata.
        </p>
        <div className="mt-6">
          <Link
            href="/catalogo"
            className="inline-block rounded-xl bg-accent-600 px-8 py-3.5 text-base font-bold text-white shadow-md hover:bg-accent-700 transition-all focus:outline-none focus:ring-2 focus:ring-accent-600 focus:ring-offset-2"
          >
            Ir al catálogo de vehículos
          </Link>
        </div>
      </div>
    </div>
  );
}
