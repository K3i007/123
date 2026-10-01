"use client";
import { FormEvent, useState } from "react";
import Link from "next/link";

export default function RegistrationPage() {
  const [message, setMessage] = useState(""); const [error, setError] = useState(""); const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setError(""); setMessage("");
    const form = new FormData(event.currentTarget);
    const response = await fetch("/api/customer/register", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ email: form.get("email"), password: form.get("password"), name: form.get("name"), phone: form.get("phone") || null, privacyPolicyVersion: "2026-09" }) });
    const body = await response.json().catch(() => ({})); setBusy(false);
    if (!response.ok) { setError(body.detail ?? "No fue posible crear la cuenta."); return; }
    setMessage("Si el correo puede registrarse, recibirás instrucciones para verificarlo.");
  }
  return <section className="mx-auto max-w-md py-12"><h1 className="text-3xl font-bold text-slate-900">Crea tu cuenta</h1><form onSubmit={submit} className="mt-6 space-y-4"><label className="block text-sm font-medium">Nombre<input required name="name" className="mt-1 w-full rounded border p-2" /></label><label className="block text-sm font-medium">Correo<input required type="email" name="email" className="mt-1 w-full rounded border p-2" /></label><label className="block text-sm font-medium">Teléfono opcional<input name="phone" type="tel" className="mt-1 w-full rounded border p-2" /></label><label className="block text-sm font-medium">Contraseña<input required minLength={12} name="password" type="password" className="mt-1 w-full rounded border p-2" /></label>{error && <p role="alert" className="text-sm text-red-700">{error}</p>}{message && <p role="status" className="text-sm text-emerald-700">{message}</p>}<button disabled={busy} className="w-full rounded bg-brand px-4 py-2 font-semibold text-white disabled:opacity-60">{busy ? "Creando..." : "Crear cuenta"}</button></form><p className="mt-5 text-sm"><Link href="/cuenta/login" className="font-semibold text-brand underline">Ya tengo una cuenta</Link></p></section>;
}
