"use client";
import { FormEvent, useState } from "react";
import { useAuth } from "../../components/auth-provider";
export default function LoginPage() {
  const { login } = useAuth();
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError(undefined);
    const form = new FormData(event.currentTarget);
    try {
      await login(String(form.get("email")), String(form.get("password")));
      window.location.assign("/admin");
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "No fue posible iniciar sesión.");
    } finally {
      setBusy(false);
    }
  }
  return (
    <section className="mx-auto max-w-md rounded-lg bg-white p-7 shadow-sm">
      <h1 className="text-2xl font-bold">Iniciar sesión</h1>
      <form className="mt-6 space-y-4" onSubmit={submit}>
        <label className="block">
          Correo
          <input required name="email" type="email" className="mt-1 w-full rounded border p-2" />
        </label>
        <label className="block">
          Contraseña
          <input
            required
            name="password"
            type="password"
            className="mt-1 w-full rounded border p-2"
          />
        </label>
        {error && (
          <p role="alert" className="text-red-700">
            {error}
          </p>
        )}
        <button
          disabled={busy}
          className="w-full rounded bg-brand px-4 py-2 font-semibold text-white disabled:opacity-60"
        >
          {busy ? "Ingresando..." : "Ingresar"}
        </button>
      </form>
    </section>
  );
}
