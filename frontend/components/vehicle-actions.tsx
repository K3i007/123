"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useAuth } from "./auth-provider";
import { request } from "../lib/api";

const favoritesKey = "guest_favorite_vehicle_ids";
const comparisonKey = "guest_comparison_vehicle_ids";

function readIds(key: string) {
  try {
    const value: unknown = JSON.parse(localStorage.getItem(key) ?? "[]");
    return Array.isArray(value) ? value.filter((id): id is string => typeof id === "string") : [];
  } catch { return []; }
}

export function VehicleActions({ vehicleId }: { vehicleId: string }) {
  const { authenticated, accountType } = useAuth();
  const [favorite, setFavorite] = useState(false);
  const [comparison, setComparison] = useState<string[]>([]);
  const [message, setMessage] = useState("");

  useEffect(() => {
    if (authenticated && accountType === "Customer") {
      void request<{ vehicleIds: string[] }>("/customers/favorites").then((data) => setFavorite(data.vehicleIds.includes(vehicleId))).catch(() => undefined);
      void request<{ vehicleIds: string[] }>("/customers/comparison").then((data) => setComparison(data.vehicleIds)).catch(() => undefined);
    } else {
      setFavorite(readIds(favoritesKey).includes(vehicleId));
      setComparison(readIds(comparisonKey));
    }
  }, [authenticated, accountType, vehicleId]);

  async function toggleFavorite() {
    const next = !favorite;
    if (authenticated && accountType === "Customer") {
      try { await request(`/customers/favorites/${vehicleId}`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ favorite: next }) }); }
      catch { setMessage("No fue posible actualizar tus favoritos."); return; }
    } else {
      const ids = readIds(favoritesKey);
      localStorage.setItem(favoritesKey, JSON.stringify(next ? [...new Set([...ids, vehicleId])].slice(0, 100) : ids.filter((id) => id !== vehicleId)));
    }
    setFavorite(next); setMessage(next ? "Vehículo guardado en favoritos." : "Vehículo eliminado de favoritos.");
    window.dispatchEvent(new Event("concesionaria_counts_changed"));
  }

  async function toggleComparison() {
    const next = comparison.includes(vehicleId) ? comparison.filter((id) => id !== vehicleId) : [...comparison, vehicleId];
    if (next.length > 4) { setMessage("Puedes comparar hasta cuatro vehículos."); return; }
    if (authenticated && accountType === "Customer") {
      try { const result = await request<{ vehicleIds: string[] }>("/customers/comparison", { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ vehicleIds: next }) }); setComparison(result.vehicleIds); }
      catch { setMessage("No fue posible actualizar la comparación."); return; }
    } else { localStorage.setItem(comparisonKey, JSON.stringify(next)); setComparison(next); }
    setMessage(next.includes(vehicleId) ? "Vehículo agregado a la comparación." : "Vehículo eliminado de la comparación.");
    window.dispatchEvent(new Event("concesionaria_counts_changed"));
  }

  return <section aria-label="Acciones del vehículo" className="border-y border-slate-200 py-4"><div className="flex flex-wrap gap-2"><button type="button" onClick={() => void toggleFavorite()} aria-pressed={favorite} className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm font-semibold hover:border-brand">{favorite ? "Quitar favorito" : "Guardar favorito"}</button><button type="button" onClick={() => void toggleComparison()} aria-pressed={comparison.includes(vehicleId)} className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm font-semibold hover:border-brand">{comparison.includes(vehicleId) ? "Quitar de comparar" : "Comparar"}</button><Link href={comparison.length >= 2 ? `/comparar?ids=${comparison.join(",")}` : "/comparar"} className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm font-semibold hover:border-brand">Ver comparación ({comparison.length})</Link></div>{message && <p role="status" className="mt-3 text-sm text-slate-700">{message}</p>}</section>;
}
