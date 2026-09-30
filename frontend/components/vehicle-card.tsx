import Link from "next/link";
import { VehiclePlaceholder } from "./vehicle-placeholder";

export interface PublicVehicleItem {
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
  imageUrl: string | null;
  createdAt: string;
}

export function VehicleCard({ vehicle }: { vehicle: PublicVehicleItem }) {
  const title = `${vehicle.make} ${vehicle.model} ${vehicle.variant ? vehicle.variant : ""}`.trim();
  const formattedPrice = new Intl.NumberFormat("es-MX", {
    style: "currency",
    currency: vehicle.currency || "MXN",
    maximumFractionDigits: 0,
  }).format(vehicle.price);

  const formattedMileage = new Intl.NumberFormat("es-MX").format(vehicle.mileage);

  return (
    <article
      data-testid="vehicle-card"
      className="group flex flex-col overflow-hidden rounded-xl border border-slate-200/80 bg-white shadow-sm transition-all duration-200 hover:-translate-y-0.5 hover:border-slate-300 hover:shadow-md"
    >
      <Link
        href={`/vehiculos/${vehicle.id}`}
        className="relative block overflow-hidden focus:outline-none focus:ring-2 focus:ring-brand focus:ring-offset-2"
        aria-label={`Ver detalle de ${title}, año ${vehicle.year}, precio ${formattedPrice}`}
      >
        <VehiclePlaceholder alt={`Fotografía de ${title}`} />
        <span
          className={`absolute left-3 top-3 rounded-full px-2.5 py-0.5 text-xs font-semibold tracking-wide shadow-sm ${
            vehicle.condition === "New"
              ? "bg-brand text-white"
              : "bg-slate-900/80 text-white backdrop-blur-sm"
          }`}
        >
          {vehicle.condition === "New" ? "Nuevo" : "Seminuevo"}
        </span>
      </Link>

      <div className="flex flex-1 flex-col p-4">
        <div className="flex items-start justify-between gap-2">
          <div>
            <span className="text-xs font-medium text-slate-500 uppercase tracking-wider">
              {vehicle.year} • {vehicle.bodyStyle || "Vehículo"}
            </span>
            <h3 className="mt-0.5 line-clamp-1 text-lg font-bold text-slate-900">
              <Link
                href={`/vehiculos/${vehicle.id}`}
                className="hover:text-brand transition-colors focus:outline-none focus:underline"
              >
                {title}
              </Link>
            </h3>
          </div>
        </div>

        <div className="mt-3 flex flex-wrap gap-x-3 gap-y-1 text-xs text-slate-600">
          <span className="flex items-center gap-1">
            <svg
              className="h-3.5 w-3.5 text-slate-400"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z"
              />
            </svg>
            {formattedMileage} km
          </span>
          {vehicle.transmission && (
            <span className="flex items-center gap-1">
              <span className="inline-block h-1.5 w-1.5 rounded-full bg-slate-300" />
              {vehicle.transmission}
            </span>
          )}
          {vehicle.fuel && (
            <span className="flex items-center gap-1">
              <span className="inline-block h-1.5 w-1.5 rounded-full bg-slate-300" />
              {vehicle.fuel}
            </span>
          )}
        </div>

        <div className="mt-1 text-xs text-slate-500 flex items-center gap-1">
          <svg
            className="h-3.5 w-3.5 text-slate-400"
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2}
              d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z"
            />
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2}
              d="M15 11a3 3 0 11-6 0 3 3 0 016 0z"
            />
          </svg>
          {vehicle.branch}
        </div>

        <div className="mt-4 flex items-center justify-between border-t border-slate-100 pt-3">
          <div>
            <span className="text-[10px] uppercase font-semibold text-slate-400 block">
              Precio al contado
            </span>
            <span className="text-xl font-extrabold text-slate-900 tracking-tight">
              {formattedPrice}
            </span>
          </div>
          <Link
            href={`/vehiculos/${vehicle.id}`}
            className="rounded-lg bg-accent-600 px-3.5 py-2 text-sm font-semibold text-white shadow-sm hover:bg-accent-700 focus:outline-none focus:ring-2 focus:ring-accent-600 focus:ring-offset-2 transition-colors"
          >
            Ver auto
          </Link>
        </div>
      </div>
    </article>
  );
}
