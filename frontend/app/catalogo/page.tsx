import { CatalogClient, type FilterOptions, type VehiclePagedResult } from "./catalog-client";

const backend = process.env.BACKEND_URL ?? "http://localhost:5080";

async function fetchCatalog(path: string) {
  try {
    const response = await fetch(`${backend}/api/v1/public/${path}`, { next: { revalidate: 45 } });
    const contentType = response.headers.get("content-type") ?? "";
    if (!response.ok) throw new Error(response.status === 429 ? `El catálogo está temporalmente limitado.${response.headers.get("retry-after") ? ` Reintenta en ${response.headers.get("retry-after")} segundos.` : ""}` : response.status >= 500 ? "El catálogo no está disponible temporalmente." : `El catálogo respondió ${response.status}.`);
    if (!contentType.includes("application/json")) throw new Error("El catálogo devolvió una respuesta inválida.");
    return response.json();
  } catch (error) {
    if (error instanceof Error && error.message !== "fetch failed") throw error;
    throw new Error("No se pudo conectar con el catálogo.");
  }
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
