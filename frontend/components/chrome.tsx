"use client";
import Link from "next/link";
import { useAuth } from "./auth-provider";
export function PublicHeader() {
  const { authenticated, logout } = useAuth();
  return (
    <header className="border-b bg-white">
      <div className="mx-auto flex max-w-7xl items-center justify-between px-5 py-4">
        <Link className="font-bold text-ink" href="/">
          CONCESIONARIA
        </Link>
        <nav className="flex items-center gap-4 text-sm">
          <Link href="/">Inicio</Link>
          {authenticated ? (
            <button className="text-brand" onClick={() => void logout()}>
              Salir
            </button>
          ) : (
            <Link className="text-brand" href="/login">
              Ingresar
            </Link>
          )}
        </nav>
      </div>
    </header>
  );
}
export function PublicFooter() {
  return (
    <footer className="border-t bg-white">
      <div className="mx-auto max-w-7xl px-5 py-7 text-sm text-slate-600">
        Concesionaria · Todos los derechos reservados
      </div>
    </footer>
  );
}
