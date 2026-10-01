import type { Metadata } from "next";
import Link from "next/link";
import { cache } from "react";
import { notFound } from "next/navigation";
import { VehicleActions } from "../../../components/vehicle-actions";
import { VehicleGallery } from "../../../components/vehicle-gallery";
import { VehicleCard, type PublicVehicleItem } from "../../../components/vehicle-card";
import {
  parsePublicCustomFields,
  safeJsonLd,
  sanitizeCatalogReturn,
  telephoneHref,
} from "../../../lib/vehicle-detail";

const backend = process.env.BACKEND_URL ?? "http://localhost:5080";
const siteUrl = process.env.NEXT_PUBLIC_SITE_URL ?? "http://localhost:3000";
interface VehicleDetail {
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
  branch: { name: string; address: string; phones: string[]; hours: Record<string, string> };
  equipment: string[];
  customFields: string;
  publishedOn: string | null;
  images: { url: string; alt: string; position: number }[];
}
const getVehicle = cache(async (id: string): Promise<VehicleDetail | null> => {
  try {
    const response = await fetch(`${backend}/api/v1/public/vehicles/${id}`, {
      next: { revalidate: 60 },
    });
    return response.ok ? ((await response.json()) as VehicleDetail) : null;
  } catch {
    return null;
  }
});
const getSimilar = cache(async (id: string): Promise<PublicVehicleItem[]> => {
  try {
    const response = await fetch(`${backend}/api/v1/public/vehicles/${id}/similar`, {
      next: { revalidate: 60 },
    });
    return response.ok ? ((await response.json()) as PublicVehicleItem[]) : [];
  } catch {
    return [];
  }
});
const titleFor = (vehicle: VehicleDetail) =>
  `${vehicle.make} ${vehicle.model} ${vehicle.variant ?? ""}`.trim();

export async function generateMetadata({
  params,
}: {
  params: Promise<{ id: string }>;
}): Promise<Metadata> {
  const vehicle = await getVehicle((await params).id);
  if (!vehicle) return { title: "Vehículo no disponible" };
  const title = titleFor(vehicle);
  const canonical = `${siteUrl}/vehiculos/${vehicle.id}`;
  return {
    title,
    description: `${title}, ${vehicle.year}. Disponible en ${vehicle.branch.name}.`,
    alternates: { canonical },
    openGraph: {
      title,
      description: `${title}, ${vehicle.year}`,
      url: canonical,
      images: [
        {
          url: new URL(vehicle.images[0]?.url ?? "/vehicle-placeholder.png", siteUrl).toString(),
          width: 1200,
          height: 630,
          alt: `Imagen de ${title}`,
        },
      ],
    },
  };
}

export default async function VehiculoDetailPage({
  params,
  searchParams,
}: {
  params: Promise<{ id: string }>;
  searchParams: Promise<{ from?: string }>;
}) {
  const { id } = await params;
  const vehicle = await getVehicle(id);
  if (!vehicle) notFound();
  const origin = sanitizeCatalogReturn((await searchParams).from);
  const title = titleFor(vehicle);
  const similar = await getSimilar(id);
  const price = new Intl.NumberFormat("es-MX", {
    style: "currency",
    currency: vehicle.currency,
    maximumFractionDigits: 0,
  }).format(vehicle.price);
  const customFields = Object.entries(parsePublicCustomFields(vehicle.customFields));
  const jsonLd = safeJsonLd({
    "@context": "https://schema.org",
    "@type": "Car",
    url: `${siteUrl}/vehiculos/${vehicle.id}`,
    name: title,
    brand: { "@type": "Brand", name: vehicle.make },
    model: vehicle.model,
    vehicleModelDate: vehicle.year,
    mileageFromOdometer: { "@type": "QuantitativeValue", value: vehicle.mileage, unitCode: "KMT" },
    itemCondition: vehicle.condition === "New" ? "https://schema.org/NewCondition" : "https://schema.org/UsedCondition",
    image: vehicle.images[0]?.url ? new URL(vehicle.images[0].url, siteUrl).toString() : undefined,
    offers: {
      "@type": "Offer",
      price: vehicle.price,
      priceCurrency: vehicle.currency,
      availability: "https://schema.org/InStock",
      url: `${siteUrl}/vehiculos/${vehicle.id}`,
      itemCondition: vehicle.condition === "New" ? "https://schema.org/NewCondition" : "https://schema.org/UsedCondition",
    },
  });
  return (
    <div className="mx-auto max-w-6xl space-y-8 py-6">
      <nav aria-label="Miga de pan" className="flex flex-wrap gap-2 text-sm text-slate-600">
        <Link href="/">Inicio</Link>
        <span>/</span>
        <Link href={origin}>Catálogo</Link>
        <span>/</span>
        <span>{vehicle.make}</span>
        <span>/</span>
        <span>{vehicle.model}</span>
      </nav>
      <div className="flex flex-wrap items-end justify-between gap-4 border-b border-slate-200 pb-5">
        <div>
          <p className="text-sm font-semibold text-brand">Disponible</p>
          <h1 className="text-3xl font-bold text-slate-900 sm:text-4xl">{title}</h1>
          <p className="mt-1 text-slate-600">
            {vehicle.year} · {new Intl.NumberFormat("es-MX").format(vehicle.mileage)} km ·{" "}
            {vehicle.branch.name}
          </p>
        </div>
        <p className="text-3xl font-bold text-slate-900">{price}</p>
      </div>
      <VehicleGallery title={title} images={vehicle.images} />
      <VehicleActions vehicleId={vehicle.id} />
      <div className="grid gap-8 lg:grid-cols-[1fr_320px]">
        <div className="space-y-8">
          <section>
            <h2 className="text-xl font-bold">Especificaciones</h2>
            <dl className="mt-3 grid grid-cols-2 gap-x-6 gap-y-4 border-y border-slate-200 py-5 text-sm sm:grid-cols-3">
              {[
                ["Transmisión", vehicle.transmission],
                ["Combustible", vehicle.fuel],
                ["Tracción", vehicle.drivetrain],
                ["Carrocería", vehicle.bodyStyle],
                ["Color", vehicle.color],
                ["Condición", vehicle.condition === "New" ? "Nuevo" : "Seminuevo"],
                ...customFields.map(([key, value]) => [key, String(value)]),
              ].map(([label, value]) => (
                <div key={label}>
                  <dt className="text-slate-600">{label}</dt>
                  <dd className="font-semibold text-slate-900">{value || "No especificado"}</dd>
                </div>
              ))}
            </dl>
          </section>
          <section>
            <h2 className="text-xl font-bold">Equipamiento</h2>
            <ul className="mt-3 grid gap-2 text-sm sm:grid-cols-2">
              {vehicle.equipment.length ? (
                vehicle.equipment.map((item) => (
                  <li key={item} className="border-b border-slate-200 py-2">
                    {item}
                  </li>
                ))
              ) : (
                <li className="text-slate-600">Equipamiento por confirmar.</li>
              )}
            </ul>
          </section>
          <section className="border-y border-slate-200 py-5">
            <h2 className="text-xl font-bold">Inspección y servicios</h2>
            <p className="mt-2 text-sm text-slate-600">Información disponible próximamente.</p>
          </section>
          {similar.length > 0 && (
            <section>
              <h2 className="text-xl font-bold">Vehículos similares</h2>
              <div className="mt-4 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
                {similar.map((item) => (
                  <VehicleCard key={item.id} vehicle={item} returnTo={origin} />
                ))}
              </div>
            </section>
          )}
        </div>
        <aside className="h-fit border border-slate-200 bg-white p-5 lg:sticky lg:top-20">
          <h2 className="text-lg font-bold">Sucursal {vehicle.branch.name}</h2>
          <p className="mt-2 text-sm text-slate-600">{vehicle.branch.address}</p>
          <div className="mt-4 space-y-2 text-sm">
            {vehicle.branch.phones.map((phone) => (
              <a className="block text-brand underline" key={phone} href={telephoneHref(phone)}>
                {phone}
              </a>
            ))}
          </div>
          <dl className="mt-4 space-y-1 text-sm text-slate-600">
            {Object.entries(vehicle.branch.hours).map(([day, hours]) => (
              <div key={day}>
                <dt className="inline font-medium">{day}: </dt>
                <dd className="inline">{hours}</dd>
              </div>
            ))}
          </dl>
          <Link
            className="mt-5 inline-block text-sm font-semibold text-brand underline"
            href="/contacto"
          >
            Ver contacto
          </Link>
        </aside>
      </div>
      <Link href={origin} className="inline-block text-sm font-semibold text-brand underline">
        Volver al catálogo
      </Link>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: jsonLd }} />
    </div>
  );
}
