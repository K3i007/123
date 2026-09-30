import Link from "next/link";
import { notFound } from "next/navigation";
import { VehiclePlaceholder } from "../../../components/vehicle-placeholder";

const backend = process.env.BACKEND_URL ?? "http://localhost:5080";

interface PublicVehicleDetail {
  id: string;
  make: string;
  model: string;
  variant: string | null;
  year: number;
  mileage: number;
  price: number;
  currency: string;
  condition: "New" | "Used";
  color: string | null;
  transmission: string | null;
  fuel: string | null;
  drivetrain: string | null;
  bodyStyle: string | null;
  branch: string;
  branchId: string;
  customFields: string;
  imageUrl: string | null;
  createdAt: string;
}

async function getVehicle(id: string): Promise<PublicVehicleDetail | null> {
  try {
    const res = await fetch(`${backend}/api/v1/public/vehicles/${id}`, {
      cache: "no-store",
    });
    if (!res.ok) return null;
    return await res.json();
  } catch {
    return null;
  }
}

export default async function VehiculoDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const vehicle = await getVehicle(id);

  if (!vehicle) {
    notFound();
  }

  const title = `${vehicle.make} ${vehicle.model} ${vehicle.variant || ""}`.trim();
  const formattedPrice = new Intl.NumberFormat("es-MX", {
    style: "currency",
    currency: vehicle.currency || "MXN",
    maximumFractionDigits: 0,
  }).format(vehicle.price);

  const formattedMileage = new Intl.NumberFormat("es-MX").format(vehicle.mileage);

  return (
    <div className="mx-auto max-w-4xl py-6 space-y-8">
      {/* Breadcrumb / Return */}
      <nav aria-label="Miga de pan" className="text-xs text-slate-500 flex items-center gap-2">
        <Link href="/" className="hover:underline">
          Inicio
        </Link>
        <span>/</span>
        <Link href="/catalogo" className="hover:underline">
          Catálogo
        </Link>
        <span>/</span>
        <span className="text-slate-900 font-medium truncate">{title}</span>
      </nav>

      {/* Vehicle Header */}
      <div className="flex flex-col sm:flex-row sm:items-end justify-between gap-4 border-b border-slate-200 pb-4">
        <div>
          <span className="inline-block rounded-full bg-slate-900 px-3 py-0.5 text-xs font-semibold text-white">
            {vehicle.condition === "New" ? "Vehículo Nuevo" : "Seminuevo Certificado"}
          </span>
          <h1 className="mt-2 text-2xl font-black text-slate-900 sm:text-4xl">{title}</h1>
          <p className="mt-1 text-sm text-slate-500">
            Año {vehicle.year} • {vehicle.bodyStyle || "Sedán"} • Sucursal {vehicle.branch}
          </p>
        </div>
        <div>
          <span className="text-xs uppercase font-semibold text-slate-400 block">
            Precio al contado
          </span>
          <span className="text-3xl font-black text-slate-900 tracking-tight">
            {formattedPrice}
          </span>
        </div>
      </div>

      {/* Media Placeholder */}
      <div className="overflow-hidden rounded-2xl border border-slate-200 shadow-sm">
        <VehiclePlaceholder alt={`Fotografía provisional de ${title}`} className="max-h-[420px]" />
      </div>

      {/* Specifications Grid */}
      <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <h2 className="text-lg font-bold text-slate-900 border-b border-slate-100 pb-3">
          Ficha Técnica Básica
        </h2>
        <dl className="mt-4 grid grid-cols-2 gap-4 sm:grid-cols-3 text-sm">
          <div>
            <dt className="text-xs text-slate-500 font-semibold uppercase">Kilometraje</dt>
            <dd className="mt-1 font-bold text-slate-900">{formattedMileage} km</dd>
          </div>
          <div>
            <dt className="text-xs text-slate-500 font-semibold uppercase">Transmisión</dt>
            <dd className="mt-1 font-bold text-slate-900">
              {vehicle.transmission || "No especificada"}
            </dd>
          </div>
          <div>
            <dt className="text-xs text-slate-500 font-semibold uppercase">Combustible</dt>
            <dd className="mt-1 font-bold text-slate-900">{vehicle.fuel || "Gasolina"}</dd>
          </div>
          <div>
            <dt className="text-xs text-slate-500 font-semibold uppercase">Tracción</dt>
            <dd className="mt-1 font-bold text-slate-900">{vehicle.drivetrain || "Delantera"}</dd>
          </div>
          <div>
            <dt className="text-xs text-slate-500 font-semibold uppercase">Color exterior</dt>
            <dd className="mt-1 font-bold text-slate-900">{vehicle.color || "No especificado"}</dd>
          </div>
          <div>
            <dt className="text-xs text-slate-500 font-semibold uppercase">Sucursal</dt>
            <dd className="mt-1 font-bold text-slate-900">{vehicle.branch}</dd>
          </div>
        </dl>
      </section>

      {/* Phase 3 Extension Notice */}
      <div className="rounded-2xl border border-dashed border-teal-300 bg-teal-50/50 p-6 text-center space-y-2">
        <span className="inline-block rounded-full bg-brand/20 px-3 py-1 text-xs font-bold text-brand">
          Punto de extensión (Fase 3: Ficha Detallada)
        </span>
        <h3 className="font-bold text-slate-900 text-base">
          Galería completa e inspección técnica
        </h3>
        <p className="text-sm text-slate-600 max-w-lg mx-auto">
          En la Fase 3 estará disponible la galería fotográfica HD de 360°, el reporte de inspección
          punto a punto y las acciones de cotización directa y apartado.
        </p>
        <div className="pt-3">
          <Link
            href="/catalogo"
            className="inline-block rounded-xl bg-accent-600 px-6 py-2.5 text-sm font-bold text-white shadow-sm hover:bg-accent-700 transition-colors"
          >
            ← Volver al catálogo de vehículos
          </Link>
        </div>
      </div>
    </div>
  );
}
