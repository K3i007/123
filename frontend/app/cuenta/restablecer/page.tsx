"use client";
import { FormEvent, useState, useEffect } from "react";
import { useSearchParams, useRouter } from "next/navigation";
import Link from "next/link";

function PageContent() {
  const searchParams = useSearchParams();
  const router = useRouter();
  const [token, setToken] = useState<string>("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState(false);

  useEffect(() => {
    const t = searchParams.get("token");
    if (t) {
      setToken(t);
      window.history.replaceState(null, "", "/cuenta/restablecer");
    }
  }, [searchParams]);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError("");
    const form = new FormData(event.currentTarget);
    const password = String(form.get("password"));
    try {
      const response = await fetch("/api/customer/reset-password", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ token, password }),
        referrerPolicy: "no-referrer"
      });
      if (!response.ok) throw new Error("El enlace es inválido o la contraseña no cumple la política.");
      setSuccess(true);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Error desconocido");
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="mx-auto max-w-md py-12">
      <h1 className="text-3xl font-bold text-slate-900">Restablecer contraseña</h1>
      {!success ? (
        <form onSubmit={submit} className="mt-6 space-y-4">
          <input type="hidden" name="token" value={token} />
          <label className="block text-sm font-medium">
            Nueva contraseña (mínimo 12 caracteres)
            <input required name="password" type="password" minLength={12} className="mt-1 w-full rounded border p-2" />
          </label>
          {error && <p role="alert" className="text-sm text-red-700">{error}</p>}
          <button disabled={busy || !token} className="w-full rounded bg-brand px-4 py-2 font-semibold text-white disabled:opacity-60">
            {busy ? "Restableciendo..." : "Confirmar nueva contraseña"}
          </button>
        </form>
      ) : (
        <div className="mt-6">
          <p className="text-green-700 font-semibold">Tu contraseña ha sido restablecida.</p>
          <Link href="/cuenta/login" className="mt-4 inline-block font-semibold text-brand underline">Ir a iniciar sesión</Link>
        </div>
      )}
    </section>
  );
}


import { Suspense } from 'react';
export default function Page() { return <Suspense fallback={<div>Cargando...</div>}><PageContent /></Suspense>; }

