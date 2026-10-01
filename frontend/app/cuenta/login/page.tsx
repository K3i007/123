"use client";
import { FormEvent, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "../../../components/auth-provider";

export default function CustomerLoginPage() {
  const { loginCustomer } = useAuth();
  const router = useRouter();
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setError("");
    const form = new FormData(event.currentTarget);
    try { await loginCustomer(String(form.get("email")), String(form.get("password"))); router.replace("/perfil"); }
    catch (cause) { setError(cause instanceof Error ? cause.message : "No fue posible iniciar sesión."); }
    finally { setBusy(false); }
  }
  return <section className="mx-auto max-w-md py-12"><h1 className="text-3xl font-bold text-slate-900">Accede a tu cuenta</h1><form onSubmit={submit} className="mt-6 space-y-4"><label className="block text-sm font-medium">Correo<input required name="email" type="email" className="mt-1 w-full rounded border p-2" /></label><label className="block text-sm font-medium">Contraseña<input required name="password" type="password" className="mt-1 w-full rounded border p-2" /></label>{error && <p role="alert" className="text-sm text-red-700">{error}</p>}<button disabled={busy} className="w-full rounded bg-brand px-4 py-2 font-semibold text-white disabled:opacity-60">{busy ? "Ingresando..." : "Ingresar"}</button></form><p className="mt-5 text-sm">¿Aún no tienes cuenta? <Link className="font-semibold text-brand underline" href="/registro">Regístrate</Link></p></section>;
}
