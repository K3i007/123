"use client";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "../../components/auth-provider";
import { request } from "../../lib/api";

type Profile = { email: string; name: string; phone: string | null; language: string; marketingConsent: boolean; emailVerified: boolean };
export default function ProfilePage() {
  const { ready, authenticated, accountType, logout } = useAuth();
  const router = useRouter(); const [profile, setProfile] = useState<Profile | null>(null); const [message, setMessage] = useState("");
  useEffect(() => {
    if (ready && (!authenticated || accountType !== "Customer")) { router.replace("/cuenta/login?returnTo=/perfil"); return; }
    if (authenticated && accountType === "Customer") void request<Profile>("/customers/profile").then(setProfile).catch(() => setMessage("No fue posible cargar el perfil."));
  }, [ready, authenticated, accountType, router]);
  if (!profile) return <section className="mx-auto max-w-xl py-16"><p>{message || "Cargando tu perfil..."}</p></section>;
  async function resend() { try { await request("/customers/verify-email/resend", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ email: profile?.email }) }); setMessage("Se ha reenviado el enlace a tu correo."); } catch { setMessage("No fue posible reenviar el enlace."); } } async function save(form: FormData) {
    try { await request("/customers/profile", { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ name: form.get("name"), phone: form.get("phone") || null, language: form.get("language"), marketingConsent: form.get("marketingConsent") === "on" }) }); setMessage("Perfil actualizado."); } catch { setMessage("No fue posible actualizar el perfil."); }
  }
  return <section className="mx-auto max-w-xl py-12"><h1 className="text-3xl font-bold text-slate-900">Mi perfil</h1><p className="mt-2 text-sm text-slate-600">{profile.email} · {profile.emailVerified ? "Correo verificado" : "Correo pendiente de verificación"}</p>{!profile.emailVerified && <button type="button" onClick={resend} className="mt-2 text-sm text-blue-600 hover:underline">Reenviar enlace de verificación</button>}<form action={save} className="mt-8 space-y-4"><label className="block text-sm font-medium">Nombre<input required name="name" defaultValue={profile.name} className="mt-1 w-full rounded border p-2" /></label><label className="block text-sm font-medium">Teléfono<input name="phone" defaultValue={profile.phone ?? ""} className="mt-1 w-full rounded border p-2" /></label><label className="block text-sm font-medium">Idioma<select name="language" defaultValue={profile.language} className="mt-1 w-full rounded border p-2"><option value="es-MX">Español</option><option value="en-US">English</option></select></label><label className="flex gap-2 text-sm"><input name="marketingConsent" type="checkbox" defaultChecked={profile.marketingConsent} /> Acepto recibir novedades</label>{message && <p role="status" className="text-sm text-emerald-700">{message}</p>}<button className="rounded bg-brand px-4 py-2 font-semibold text-white">Guardar cambios</button></form><button onClick={() => void logout().then(() => router.replace("/"))} className="mt-8 rounded border px-4 py-2 text-sm">Cerrar sesión</button></section>;
}
