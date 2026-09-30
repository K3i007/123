"use client";

import { useCallback, useEffect, useMemo, useRef, useState, useTransition } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { VehicleCard, type PublicVehicleItem } from "../../components/vehicle-card";

export interface FilterOptions {
  makes: {
    id: string;
    name: string;
    models: { id: string; name: string; variants: { id: string; name: string }[] }[];
  }[];
  branches: { id: string; name: string }[];
  transmissions: string[];
  fuels: string[];
  bodyStyles: string[];
  drivetrains: string[];
  minPrice: number;
  maxPrice: number;
  minYear: number;
  maxYear: number;
  minMileage: number;
  maxMileage: number;
  conditions: ("New" | "Used")[];
  customFields: { key: string; label: string; type: string; values: string[] }[];
}
export interface VehiclePagedResult {
  items: PublicVehicleItem[];
  total: number;
  page: number;
  pageSize: number;
}

type Params = Record<string, string | null>;

export function CatalogClient({
  initialFilters,
  initialVehicles,
  initialQuery,
  initialError,
}: {
  initialFilters: FilterOptions | null;
  initialVehicles: VehiclePagedResult | null;
  initialQuery: string;
  initialError: string | null;
}) {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const [, startTransition] = useTransition();
  const [filters, setFilters] = useState(initialFilters);
  const [data, setData] = useState(initialVehicles);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(initialError);
  const [retry, setRetry] = useState(0);
  const [mobileOpen, setMobileOpen] = useState(false);
  const initialUsed = useRef(false);
  const currentQuery = searchParams.toString();
  const q = searchParams.get("q") ?? "";
  const [search, setSearch] = useState(q);
  const searchTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    setSearch(q);
  }, [q]);
  useEffect(() => {
    if (
      !initialUsed.current &&
      currentQuery === initialQuery &&
      initialVehicles &&
      initialFilters
    ) {
      initialUsed.current = true;
      return;
    }
    let cancelled = false;
    async function load() {
      setLoading(true);
      setError(null);
      try {
        const response = await fetch(`/api/proxy/public/vehicles?${currentQuery}`);
        if (!response.ok) throw new Error("catalog response failed");
        const next = (await response.json()) as VehiclePagedResult;
        if (!cancelled) setData(next);
      } catch {
        if (!cancelled) setError("No fue posible cargar los vehículos. Intenta nuevamente.");
      } finally {
        if (!cancelled) setLoading(false);
      }
    }
    load();
    return () => {
      cancelled = true;
    };
  }, [currentQuery, initialFilters, initialQuery, initialVehicles, retry]);

  useEffect(() => {
    if (filters) return;
    let cancelled = false;
    fetch("/api/proxy/public/vehicles/filters")
      .then((response) =>
        response.ok
          ? (response.json() as Promise<FilterOptions>)
          : Promise.reject(new Error("filters response failed")),
      )
      .then((next) => {
        if (!cancelled) setFilters(next);
      })
      .catch(() => {
        if (!cancelled) setError("No fue posible cargar los filtros del catálogo.");
      });
    return () => {
      cancelled = true;
    };
  }, [filters]);

  const update = useCallback(
    (next: Params, resetPage = true) => {
      const params = new URLSearchParams(searchParams.toString());
      Object.entries(next).forEach(([key, value]) =>
        value ? params.set(key, value) : params.delete(key),
      );
      if (resetPage) params.delete("page");
      startTransition(() =>
        router.replace(params.size ? `${pathname}?${params}` : pathname, { scroll: false }),
      );
    },
    [pathname, router, searchParams, startTransition],
  );

  const clear = () => {
    setSearch("");
    router.replace(pathname, { scroll: false });
  };
  const changeSearch = (value: string) => {
    setSearch(value);
    if (searchTimer.current) clearTimeout(searchTimer.current);
    searchTimer.current = setTimeout(() => update({ q: value.trim() || null }), 300);
  };
  const makeId = searchParams.get("makeId") ?? "";
  const modelId = searchParams.get("modelId") ?? "";
  const selectedMake = filters?.makes.find((make) => make.id === makeId);
  const models = selectedMake?.models ?? [];
  const variants = models.find((model) => model.id === modelId)?.variants ?? [];
  const priceMin = Math.max(
    filters?.minPrice ?? 0,
    Number(searchParams.get("minPrice") ?? filters?.minPrice ?? 0),
  );
  const priceMax = Math.min(
    filters?.maxPrice ?? 0,
    Number(searchParams.get("maxPrice") ?? filters?.maxPrice ?? 0),
  );
  const active = useMemo(
    () => Array.from(searchParams.keys()).filter((key) => key !== "page" && key !== "sort").length,
    [searchParams],
  );
  const pages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;
  const page = Number(searchParams.get("page") ?? 1);

  const controls = (
    <FilterControls
      filters={filters}
      values={searchParams}
      makeId={makeId}
      modelId={modelId}
      models={models}
      variants={variants}
      priceMin={priceMin}
      priceMax={priceMax}
      update={update}
    />
  );

  return (
    <div className="space-y-6">
      <section className="mx-auto max-w-2xl" aria-label="Búsqueda de vehículos">
        <label htmlFor="catalog-search" className="sr-only">
          Buscar vehículos por marca, modelo o versión
        </label>
        <input
          id="catalog-search"
          type="search"
          value={search}
          onChange={(event) => changeSearch(event.target.value)}
          placeholder="Buscar por marca, modelo o versión"
          className="w-full rounded-lg border border-slate-300 bg-white px-4 py-3 text-sm shadow-sm focus:border-brand focus:outline-none focus:ring-2 focus:ring-brand/20"
        />
      </section>
      <div className="flex items-center justify-between gap-3 border-b border-slate-200 pb-4">
        <button
          type="button"
          onClick={() => setMobileOpen(true)}
          className="rounded-lg border border-slate-300 px-3 py-2 text-sm font-semibold lg:hidden"
        >
          Filtros{active ? ` (${active})` : ""}
        </button>
        <p aria-live="polite" className="text-sm text-slate-600">
          {loading ? "Buscando vehículos..." : `${data?.total ?? 0} vehículos`}
        </p>
        <select
          aria-label="Ordenar resultados"
          value={searchParams.get("sort") ?? "recent"}
          onChange={(event) => update({ sort: event.target.value })}
          className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm"
        >
          <option value="recent">Más recientes</option>
          <option value="price_asc">Precio: menor a mayor</option>
          <option value="price_desc">Precio: mayor a menor</option>
          <option value="mileage_asc">Menor kilometraje</option>
          <option value="year_desc">Año más nuevo</option>
        </select>
      </div>
      <div className="flex items-start gap-6">
        <aside
          data-testid="filter-panel"
          className="sticky top-20 hidden w-[288px] min-w-[288px] space-y-4 rounded-lg border border-slate-200 bg-white p-5 shadow-sm lg:block"
          aria-label="Panel de filtros"
        >
          <div className="flex justify-between">
            <h2 className="font-bold">Filtros</h2>
            {active > 0 && (
              <button type="button" onClick={clear} className="text-sm text-accent-700 underline">
                Limpiar filtros
              </button>
            )}
          </div>
          {controls}
        </aside>
        <section className="min-w-0 flex-1" aria-label="Resultados del catálogo">
          {error ? (
            <ErrorState onRetry={() => setRetry((value) => value + 1)} />
          ) : loading && !data ? (
            <Skeleton />
          ) : data?.items.length ? (
            <>
              <div
                data-testid="vehicles-grid"
                className="grid grid-cols-1 gap-6 lg:grid-cols-2 catalog:grid-cols-3 wide:grid-cols-4"
              >
                {data.items.map((vehicle) => (
                  <VehicleCard
                    key={vehicle.id}
                    vehicle={vehicle}
                    returnTo={`/catalogo${currentQuery ? `?${currentQuery}` : ""}`}
                  />
                ))}
              </div>
              {pages > 1 && (
                <nav
                  className="mt-8 flex items-center justify-center gap-3"
                  aria-label="Paginación"
                >
                  <button
                    type="button"
                    disabled={page <= 1}
                    onClick={() => update({ page: String(page - 1) }, false)}
                    className="rounded-lg border px-3 py-2 disabled:opacity-40"
                  >
                    Anterior
                  </button>
                  <span className="text-sm">
                    Página {page} de {pages}
                  </span>
                  <button
                    type="button"
                    disabled={page >= pages}
                    onClick={() => update({ page: String(page + 1) }, false)}
                    className="rounded-lg border px-3 py-2 disabled:opacity-40"
                  >
                    Siguiente
                  </button>
                </nav>
              )}
            </>
          ) : (
            <EmptyState clear={clear} />
          )}
        </section>
      </div>
      {mobileOpen && (
        <div className="fixed inset-0 z-50 bg-slate-900/40 lg:hidden">
          <aside className="ml-auto h-full w-full max-w-sm overflow-y-auto bg-white p-5">
            <div className="mb-4 flex justify-between">
              <h2 className="font-bold">Filtros</h2>
              <button
                type="button"
                onClick={() => setMobileOpen(false)}
                aria-label="Cerrar filtros"
              >
                Cerrar
              </button>
            </div>
            {controls}
            <button
              type="button"
              onClick={() => setMobileOpen(false)}
              className="mt-6 w-full rounded-lg bg-brand px-4 py-3 font-semibold text-white"
            >
              Ver resultados
            </button>
          </aside>
        </div>
      )}
    </div>
  );
}

function FilterControls({
  filters,
  values,
  makeId,
  modelId,
  models,
  variants,
  priceMin,
  priceMax,
  update,
}: {
  filters: FilterOptions | null;
  values: URLSearchParams;
  makeId: string;
  modelId: string;
  models: FilterOptions["makes"][number]["models"];
  variants: { id: string; name: string }[];
  priceMin: number;
  priceMax: number;
  update: (params: Params, resetPage?: boolean) => void;
}) {
  if (!filters) return <p className="text-sm text-slate-600">Los filtros no están disponibles.</p>;
  const select = (
    label: string,
    key: string,
    options: readonly { id?: string; name?: string }[] | readonly string[],
  ) => (
    <label className="block text-sm font-medium text-slate-700">
      {label}
      <select
        id={key === "makeId" ? "filter-make" : `filter-${key}`}
        value={values.get(key) ?? ""}
        onChange={(event) => update({ [key]: event.target.value || null })}
        className="mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm"
      >
        <option value="">Todos</option>
        {options.map((option) => {
          const value = typeof option === "string" ? option : option.id!;
          const text = typeof option === "string" ? option : option.name!;
          return (
            <option key={value} value={value}>
              {text}
            </option>
          );
        })}
      </select>
    </label>
  );
  const setPrice = (key: "minPrice" | "maxPrice", number: number) =>
    update({ [key]: String(number) });
  return (
    <div className="space-y-4">
      {select("Marca", "makeId", filters.makes)}
      <label className="block text-sm font-medium text-slate-700">
        Modelo
        <select
          id="filter-model"
          disabled={!makeId}
          value={modelId}
          onChange={(event) => update({ modelId: event.target.value || null, variantId: null })}
          className="mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm disabled:bg-slate-100"
        >
          <option value="">Todos</option>
          {models.map((model) => (
            <option key={model.id} value={model.id}>
              {model.name}
            </option>
          ))}
        </select>
      </label>
      {variants.length > 0 && select("Versión", "variantId", variants)}
      <fieldset>
        <legend className="text-sm font-medium text-slate-700">Rango de precio (MXN)</legend>
        <div className="mt-2 flex gap-2 text-xs">
          <output aria-label="Precio mínimo">${priceMin.toLocaleString("es-MX")}</output>
          <span>a</span>
          <output aria-label="Precio máximo">${priceMax.toLocaleString("es-MX")}</output>
        </div>
        <div className="relative mt-2 h-7">
          <input
            type="range"
            min={filters.minPrice}
            max={priceMax}
            step={10000}
            value={priceMin}
            onChange={(event) =>
              setPrice("minPrice", Math.min(Number(event.target.value), priceMax))
            }
            aria-label="Precio mínimo slider"
            className="absolute inset-x-0 w-full accent-brand"
          />
          <input
            type="range"
            min={priceMin}
            max={filters.maxPrice}
            step={10000}
            value={priceMax}
            onChange={(event) =>
              setPrice("maxPrice", Math.max(Number(event.target.value), priceMin))
            }
            aria-label="Precio máximo slider"
            className="absolute inset-x-0 w-full accent-brand"
          />
        </div>
      </fieldset>
      <div className="grid grid-cols-2 gap-2">
        <label className="text-sm font-medium">
          Año desde
          <select
            value={values.get("minYear") ?? ""}
            onChange={(event) => update({ minYear: event.target.value || null })}
            className="mt-1 w-full rounded-lg border p-2 text-sm"
          >
            <option value="">Todos</option>
            {Array.from(
              { length: filters.maxYear - filters.minYear + 1 },
              (_, i) => filters.minYear + i,
            ).map((year) => (
              <option key={year}>{year}</option>
            ))}
          </select>
        </label>
        <label className="text-sm font-medium">
          Año hasta
          <select
            value={values.get("maxYear") ?? ""}
            onChange={(event) => update({ maxYear: event.target.value || null })}
            className="mt-1 w-full rounded-lg border p-2 text-sm"
          >
            <option value="">Todos</option>
            {Array.from(
              { length: filters.maxYear - filters.minYear + 1 },
              (_, i) => filters.maxYear - i,
            ).map((year) => (
              <option key={year}>{year}</option>
            ))}
          </select>
        </label>
      </div>
      <label className="block text-sm font-medium">
        Kilometraje máximo
        <input
          id="filter-mileage"
          type="number"
          min="0"
          max={filters.maxMileage}
          value={values.get("maxMileage") ?? ""}
          onChange={(event) => update({ maxMileage: event.target.value || null })}
          className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
        />
      </label>
      {select("Transmisión", "transmission", filters.transmissions)}
      {select("Combustible", "fuel", filters.fuels)}
      {select("Tracción", "drivetrain", filters.drivetrains)}
      {select("Carrocería", "bodyStyle", filters.bodyStyles)}
      {select("Sucursal", "branchId", filters.branches)}
      <label className="block text-sm font-medium">
        Condición
        <select
          value={values.get("condition") ?? ""}
          onChange={(event) => update({ condition: event.target.value || null })}
          className="mt-1 w-full rounded-lg border border-slate-300 p-2 text-sm"
        >
          <option value="">Todas</option>
          <option value="New">Nuevo</option>
          <option value="Used">Seminuevo</option>
        </select>
      </label>
      {(filters.customFields ?? []).map((field) => (
        <label key={field.key} className="block text-sm font-medium">
          {field.label}
          <select
            value={values.get(`cf.${field.key}`) ?? ""}
            onChange={(event) => update({ [`cf.${field.key}`]: event.target.value || null })}
            className="mt-1 w-full rounded-lg border border-slate-300 p-2 text-sm"
          >
            <option value="">Todos</option>
            {field.values.map((value) => (
              <option key={value} value={value}>
                {value}
              </option>
            ))}
          </select>
        </label>
      ))}
    </div>
  );
}

function ErrorState({ onRetry }: { onRetry: () => void }) {
  return (
    <div role="alert" className="rounded-lg border border-red-200 bg-red-50 p-8 text-center">
      <p className="font-semibold text-red-800">
        No fue posible cargar los vehículos. Intenta nuevamente.
      </p>
      <button
        type="button"
        onClick={onRetry}
        className="mt-4 rounded-lg bg-red-700 px-4 py-2 text-sm font-semibold text-white"
      >
        Reintentar
      </button>
    </div>
  );
}
function EmptyState({ clear }: { clear: () => void }) {
  return (
    <div className="rounded-lg border border-dashed border-slate-300 p-12 text-center">
      <h2 className="font-bold">No se encontraron vehículos con las condiciones seleccionadas</h2>
      <button
        type="button"
        data-testid="empty-clear-btn"
        onClick={clear}
        className="mt-4 rounded-lg bg-accent-600 px-4 py-2 text-sm font-semibold text-white"
      >
        Limpiar filtros
      </button>
    </div>
  );
}
function Skeleton() {
  return (
    <div className="grid grid-cols-1 gap-6 lg:grid-cols-2 catalog:grid-cols-3 wide:grid-cols-4">
      {Array.from({ length: 8 }, (_, index) => (
        <div key={index} className="h-80 animate-pulse rounded-lg bg-slate-200" />
      ))}
    </div>
  );
}
