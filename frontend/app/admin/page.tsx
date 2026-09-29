"use client";
import { FormEvent, useEffect, useState } from "react";
import { api } from "../../lib/api";
import Link from "next/link";
type Vehicle = {
  id: string;
  make: string;
  model: string;
  branch: string;
  year: number;
  price: number;
  currency: string;
  status: string;
};
type Page = { items: Vehicle[]; total: number };
export default function AdminPage() {
  const [data, setData] = useState<Page>();
  const [search, setSearch] = useState("");
  useEffect(() => {
    api<Page>("/inventory/vehicles?page=1&pageSize=20").then(setData);
  }, []);
  function submit(event: FormEvent) {
    event.preventDefault();
    api<Page>(`/inventory/vehicles?page=1&pageSize=20&search=${encodeURIComponent(search)}`).then(
      setData,
    );
  }
  return (
    <section>
      <div className="flex items-end justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold">Inventario</h1>
          <p className="mt-2 text-slate-600">Vehículos, estados y preparación para publicación.</p>
        </div>
        <Link
          className="rounded bg-brand px-4 py-2 font-semibold text-white"
          href="/admin/vehicles/new"
        >
          Agregar vehículo
        </Link>
      </div>
      <form className="mt-7 flex max-w-xl gap-2" onSubmit={submit}>
        <label className="sr-only" htmlFor="search">
          Buscar inventario
        </label>
        <input
          id="search"
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          className="min-w-0 flex-1 rounded border p-2"
          placeholder="Marca, modelo o versión"
        />
        <button className="rounded border px-4 py-2">Buscar</button>
      </form>
      <div className="mt-6 overflow-x-auto rounded-lg bg-white shadow-sm">
        <table className="w-full text-left text-sm">
          <thead className="border-b bg-slate-50">
            <tr>
              <th className="p-3">Vehículo</th>
              <th className="p-3">Año</th>
              <th className="p-3">Precio</th>
              <th className="p-3">Sucursal</th>
              <th className="p-3">Estado</th>
            </tr>
          </thead>
          <tbody>
            {data?.items.map((vehicle) => (
              <tr className="border-b" key={vehicle.id}>
                <td className="p-3 font-medium">
                  <Link className="text-brand underline" href={`/admin/vehicles/${vehicle.id}`}>
                    {vehicle.make} {vehicle.model}
                  </Link>
                </td>
                <td className="p-3">{vehicle.year}</td>
                <td className="p-3">
                  {vehicle.currency} {vehicle.price.toLocaleString("es-MX")}
                </td>
                <td className="p-3">{vehicle.branch}</td>
                <td className="p-3">
                  <span className="rounded bg-slate-100 px-2 py-1">{vehicle.status}</span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p className="mt-3 text-sm text-slate-600">
        {data ? `${data.total} vehículos encontrados` : "Cargando inventario..."}
      </p>
    </section>
  );
}
