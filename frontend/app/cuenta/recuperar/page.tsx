"use client";
import { FormEvent, useState } from "react";
import Link from "next/link";

function PageContent() {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError("");
    const form = new FormData(event.currentTarget);
    const email = String(form.get("email"));
    try {
      const response = await fetch("/api/customer/request-reset", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email })
      });
      if (!response.ok) throw new Error("Ocurrió un error.");
      setSuccess(true);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Error desconocido");
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="mx-auto max-w-md py-12">
      <h1 className="text-3xl font-bold text-slate-900">Recuperar cuenta</h1>
      {!success ? (
        <form onSubmit={submit} className="mt-6 space-y-4">
          <label className="block text-sm font-medium">
            Correo electrónico
            <input required name="email" type="email" className="mt-1 w-full rounded border p-2" />
          </label>
          {error && <p role="alert" className="text-sm text-red-700">{error}</p>}
          <button disabled={busy} className="w-full rounded bg-brand px-4 py-2 font-semibold text-white disabled:opacity-60">
            {busy ? "Enviando..." : "Enviar enlace de recuperación"}
          </button>
        </form>
      ) : (
        <div className="mt-6">
          <p className="text-green-700 font-semibold">Si existe una cuenta con ese correo, recibirás un enlace de recuperación pronto.</p>
          <Link href="/cuenta/login" className="mt-4 inline-block font-semibold text-brand underline">Volver al inicio de sesión</Link>
        </div>
      )}
    </section>
  );
}


import { Suspense } from 'react';
export default function Page() { return <Suspense fallback={<div>Cargando...</div>}><PageContent /></Suspense>; }

