import { CatalogClient, type FilterOptions, type VehiclePagedResult } from "./catalog-client";

const backend = process.env.BACKEND_URL ?? "http://localhost:5080";

async function fetchCatalog(path: string) {
  const response = await fetch(`${backend}/api/v1/public/${path}`, { next: { revalidate: 45 } });
  if (!response.ok) throw new Error(`Public catalog returned ${response.status}`);
  return response.json();
}

export default async function CatalogoPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const params = await searchParams;
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(params))
    if (typeof value === "string") query.set(key, value);

  let initialFilters: FilterOptions | null = null;
  let initialVehicles: VehiclePagedResult | null = null;
  let initialError: string | null = null;
  try {
    [initialFilters, initialVehicles] = await Promise.all([
      fetchCatalog("vehicles/filters"),
      fetchCatalog(`vehicles?${query.toString()}`),
    ]);
  } catch {
    initialError = "No fue posible cargar el catálogo. Intenta nuevamente.";
  }

  return (
    <CatalogClient
      initialFilters={initialFilters}
      initialVehicles={initialVehicles}
      initialQuery={query.toString()}
      initialError={initialError}
    />
  );
}
