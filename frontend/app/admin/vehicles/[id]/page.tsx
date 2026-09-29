"use client";
import { FormEvent, useEffect, useState } from "react";
import { ApiError, api, request } from "../../../../lib/api";
type Vehicle = {
  id: string;
  makeId: string;
  modelId: string;
  variantId?: string;
  branchId: string;
  make: string;
  model: string;
  year: number;
  mileage: number;
  price: number;
  currency: string;
  status: string;
  condition: string;
  version: number;
  vin?: string;
  plate?: string;
  color?: string;
  transmission?: string;
  fuel?: string;
  drivetrain?: string;
  bodyStyle?: string;
  customFields: string;
};
type History = {
  prices: { previousPrice: number; newPrice: number; currency: string; reason: string }[];
};
export default function VehicleDetail({ params }: { params: Promise<{ id: string }> }) {
  const [vehicle, setVehicle] = useState<Vehicle>();
  const [history, setHistory] = useState<History>();
  const [message, setMessage] = useState<string>();
  useEffect(() => {
    void params.then(async ({ id }) => {
      setVehicle(await api<Vehicle>(`/inventory/vehicles/${id}`));
      setHistory(await api<History>(`/inventory/vehicles/${id}/history`));
    });
  }, [params]);
  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!vehicle) return;
    const form = new FormData(event.currentTarget);
    try {
      const updated = await request<Vehicle>(`/inventory/vehicles/${vehicle.id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json", "If-Match": `\"${vehicle.version}\"` },
        body: JSON.stringify({
          makeId: vehicle.makeId,
          modelId: vehicle.modelId,
          variantId: vehicle.variantId,
          branchId: vehicle.branchId,
          year: Number(form.get("year")),
          mileage: Number(form.get("mileage")),
          price: Number(form.get("price")),
          currency: vehicle.currency,
          condition: vehicle.condition,
          vin: vehicle.vin,
          plate: vehicle.plate,
          color: vehicle.color,
          transmission: vehicle.transmission,
          fuel: vehicle.fuel,
          drivetrain: vehicle.drivetrain,
          bodyStyle: vehicle.bodyStyle,
          customFields: vehicle.customFields,
          priceReason: "Actualización desde panel",
        }),
      });
      setVehicle(updated);
      setMessage("Cambios guardados.");
    } catch (error) {
      setMessage(
        error instanceof ApiError && error.status === 409
          ? "Otro usuario actualizó este vehículo. Recarga la página antes de guardar."
          : error instanceof Error
            ? error.message
            : "No fue posible guardar.",
      );
    }
  }
  if (!vehicle) return <p>Cargando vehículo...</p>;
  return (
    <section className="max-w-3xl">
      <h1 className="text-3xl font-bold">
        {vehicle.make} {vehicle.model}
      </h1>
      <p className="mt-2 text-slate-600">Estado: {vehicle.status}</p>
      <form
        className="mt-6 grid gap-4 rounded-lg bg-white p-6 shadow-sm sm:grid-cols-2"
        onSubmit={save}
      >
        <label>
          Año
          <input
            name="year"
            defaultValue={vehicle.year}
            className="mt-1 w-full rounded border p-2"
          />
        </label>
        <label>
          Kilometraje
          <input
            name="mileage"
            defaultValue={vehicle.mileage}
            className="mt-1 w-full rounded border p-2"
          />
        </label>
        <label>
          Precio
          <input
            name="price"
            defaultValue={vehicle.price}
            className="mt-1 w-full rounded border p-2"
          />
        </label>
        <button className="self-end rounded bg-brand px-4 py-2 text-white">Guardar cambios</button>
      </form>
      {message && (
        <p role="status" className="mt-4">
          {message}
        </p>
      )}
      <h2 className="mt-8 text-xl font-bold">Historial de precios</h2>
      <ul className="mt-3 space-y-2">
        {history?.prices.map((item, index) => (
          <li className="rounded bg-white p-3" key={index}>
            {item.currency} {item.previousPrice} → {item.newPrice} · {item.reason}
          </li>
        ))}
      </ul>
    </section>
  );
}
