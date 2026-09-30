import Link from "next/link";
import { VehicleCard, type PublicVehicleItem } from "../components/vehicle-card";

const backend = process.env.BACKEND_URL ?? "http://localhost:5080";

async function getFeaturedVehicles(): Promise<PublicVehicleItem[]> {
  try {
    const res = await fetch(`${backend}/api/v1/public/vehicles/featured`, {
      cache: "no-store",
    });
    if (!res.ok) return [];
    return (await res.json()) as PublicVehicleItem[];
  } catch {
    return [];
  }
}

async function getRecentVehicles(): Promise<PublicVehicleItem[]> {
  try {
    const res = await fetch(`${backend}/api/v1/public/vehicles/recent`, {
      cache: "no-store",
    });
    if (!res.ok) return [];
    return (await res.json()) as PublicVehicleItem[];
  } catch {
    return [];
  }
}

async function getBranches(): Promise<
  {
    id: string;
    name: string;
    address: string;
    phones: string;
    hours: string;
    managerName?: string;
  }[]
> {
  try {
    const res = await fetch(`${backend}/api/v1/public/branches`, {
      cache: "no-store",
    });
    if (!res.ok) return [];
    return await res.json();
  } catch {
    return [];
  }
}

export default async function HomePage() {
  const [featured, recent, branches] = await Promise.all([
    getFeaturedVehicles(),
    getRecentVehicles(),
    getBranches(),
  ]);

  return (
    <div className="space-y-16 py-4">
      {/* Hero Banner */}
      <section className="relative overflow-hidden rounded-2xl bg-gradient-to-r from-slate-900 via-brand-900 to-slate-900 px-6 py-16 text-white shadow-xl sm:px-12 sm:py-24">
        <div className="relative z-10 max-w-2xl">
          <span className="inline-flex items-center gap-1.5 rounded-full bg-brand/30 px-3 py-1 text-xs font-semibold tracking-wider text-brand-100 backdrop-blur-sm border border-brand/20">
            CONCESIONARIA DIGITAL CERTIFICADA
          </span>
          <h1 className="mt-4 text-3xl font-extrabold tracking-tight sm:text-5xl sm:leading-tight">
            Encuentra tu próximo auto con total garantía y seguridad.
          </h1>
          <p className="mt-4 text-base text-slate-300 sm:text-lg">
            Inventario inspeccionado punto a punto, precios transparentes de contado o financiado y
            entrega en sucursal con trazabilidad completa.
          </p>
          <div className="mt-8 flex flex-wrap gap-4">
            <Link
              href="/catalogo"
              className="rounded-xl bg-accent-600 px-6 py-3.5 text-base font-bold text-white shadow-md hover:bg-accent-700 focus:outline-none focus:ring-2 focus:ring-accent-600 focus:ring-offset-2 focus:ring-offset-slate-900 transition-all hover:scale-[1.02]"
            >
              Explorar catálogo completo
            </Link>
            <Link
              href="/contacto"
              className="rounded-xl border border-slate-700 bg-slate-800/80 px-6 py-3.5 text-base font-semibold text-slate-100 hover:bg-slate-700/80 focus:outline-none focus:ring-2 focus:ring-slate-500 transition-all"
            >
              Ver sucursales
            </Link>
          </div>
        </div>

        {/* Decorative Grid Pattern */}
        <div
          className="absolute inset-y-0 right-0 -z-0 w-1/2 opacity-10 pointer-events-none"
          style={{
            backgroundImage: "radial-gradient(circle at 2px 2px, white 1px, transparent 0)",
            backgroundSize: "24px 24px",
          }}
        />
      </section>

      {/* Value Propositions / Services */}
      <section className="grid grid-cols-1 gap-6 sm:grid-cols-3">
        <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
          <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-teal-50 text-brand">
            <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"
              />
            </svg>
          </div>
          <h3 className="mt-4 font-bold text-slate-900">Inspección Certificada</h3>
          <p className="mt-2 text-sm text-slate-600">
            Cada vehículo pasa por una revisión mecánica, estética y legal estricta antes de
            publicarse.
          </p>
        </div>

        <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
          <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-amber-50 text-accent-600">
            <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z"
              />
            </svg>
          </div>
          <h3 className="mt-4 font-bold text-slate-900">Precios Transparentes</h3>
          <p className="mt-2 text-sm text-slate-600">
            Sin cargos ocultos ni sorpresas. Cotizaciones claras y opciones de enganche flexibles.
          </p>
        </div>

        <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
          <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-sky-50 text-sky-600">
            <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z"
              />
            </svg>
          </div>
          <h3 className="mt-4 font-bold text-slate-900">Prueba de Manejo</h3>
          <p className="mt-2 text-sm text-slate-600">
            Agenda tu cita en la sucursal de tu elección y conduce tu próximo vehículo antes de
            decidir.
          </p>
        </div>
      </section>

      {/* Featured Vehicles */}
      <section className="space-y-6">
        <div className="flex items-end justify-between border-b border-slate-200 pb-4">
          <div>
            <span className="text-xs font-bold uppercase tracking-wider text-brand">
              SELECCIÓN PREMIUM
            </span>
            <h2 className="text-2xl font-black text-slate-900 sm:text-3xl">Vehículos Destacados</h2>
          </div>
          <Link
            href="/catalogo"
            className="text-sm font-semibold text-brand hover:text-brand-800 transition-colors focus:outline-none focus:underline"
          >
            Ver todos los destacados →
          </Link>
        </div>

        {featured.length > 0 ? (
          <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 catalog:grid-cols-3 wide:grid-cols-4">
            {featured.slice(0, 4).map((vehicle) => (
              <VehicleCard key={vehicle.id} vehicle={vehicle} />
            ))}
          </div>
        ) : (
          <p className="text-sm text-slate-500 py-6">
            No hay vehículos destacados disponibles en este momento.
          </p>
        )}
      </section>

      {/* Recent Vehicles */}
      <section className="space-y-6">
        <div className="flex items-end justify-between border-b border-slate-200 pb-4">
          <div>
            <span className="text-xs font-bold uppercase tracking-wider text-accent-600">
              RECIÉN INGRESADOS
            </span>
            <h2 className="text-2xl font-black text-slate-900 sm:text-3xl">
              Agregados Recientemente
            </h2>
          </div>
          <Link
            href="/catalogo?sort=recent"
            className="text-sm font-semibold text-brand hover:text-brand-800 transition-colors focus:outline-none focus:underline"
          >
            Ver novedades →
          </Link>
        </div>

        {recent.length > 0 ? (
          <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 catalog:grid-cols-3 wide:grid-cols-4">
            {recent.slice(0, 8).map((vehicle) => (
              <VehicleCard key={vehicle.id} vehicle={vehicle} />
            ))}
          </div>
        ) : (
          <p className="text-sm text-slate-500 py-6">No hay novedades registradas recientemente.</p>
        )}
      </section>

      {/* Extension Points (De tu interés / Cercanos a ti) */}
      <section className="grid grid-cols-1 gap-6 md:grid-cols-2">
        <div className="rounded-xl border border-dashed border-slate-300 bg-slate-100/60 p-6 text-center">
          <span className="inline-block rounded-full bg-slate-200 px-3 py-1 text-xs font-semibold text-slate-700">
            Punto de extensión (Fase 4)
          </span>
          <h3 className="mt-3 text-lg font-bold text-slate-800">Recomendados para ti</h3>
          <p className="mt-1 text-sm text-slate-600">
            Personalización según tu historial de búsquedas y autos favoritos disponible
            próximamente.
          </p>
        </div>

        <div className="rounded-xl border border-dashed border-slate-300 bg-slate-100/60 p-6 text-center">
          <span className="inline-block rounded-full bg-slate-200 px-3 py-1 text-xs font-semibold text-slate-700">
            Punto de extensión (Fase 10)
          </span>
          <h3 className="mt-3 text-lg font-bold text-slate-800">Autos cercanos a tu ubicación</h3>
          <p className="mt-1 text-sm text-slate-600">
            Búsqueda por geolocalización y radio de distancia disponible próximamente.
          </p>
        </div>
      </section>

      {/* Branches Section */}
      <section className="rounded-2xl border border-slate-200 bg-white p-8 shadow-sm">
        <div className="max-w-2xl">
          <span className="text-xs font-bold uppercase tracking-wider text-brand">
            RED DE ATENCIÓN
          </span>
          <h2 className="mt-1 text-2xl font-black text-slate-900 sm:text-3xl">
            Nuestras Sucursales
          </h2>
          <p className="mt-2 text-sm text-slate-600">
            Visítanos para conocer el inventario en persona, realizar inspecciones conjuntas y
            pruebas de manejo.
          </p>
        </div>

        <div className="mt-8 grid grid-cols-1 gap-6 md:grid-cols-3">
          {branches.map((b) => (
            <div key={b.id} className="rounded-xl border border-slate-100 bg-slate-50 p-5">
              <h3 className="font-bold text-slate-900 text-lg">Sucursal {b.name}</h3>
              <p className="mt-2 text-sm text-slate-600">{b.address}</p>
              {b.managerName && (
                <p className="mt-1 text-xs text-slate-500">Responsable: {b.managerName}</p>
              )}
              <div className="mt-4 pt-4 border-t border-slate-200/80 flex items-center justify-between text-xs">
                <span className="text-brand font-semibold">Atención presencial</span>
                <Link href="/contacto" className="text-slate-600 hover:text-slate-900 underline">
                  Detalles y horarios
                </Link>
              </div>
            </div>
          ))}
        </div>
      </section>
    </div>
  );
}
