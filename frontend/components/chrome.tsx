"use client";

import Link from "next/link";
import { useState } from "react";
import { useAuth } from "./auth-provider";

export function PublicHeader() {
  const { authenticated, roles, logout } = useAuth();
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  return (
    <header className="sticky top-0 z-40 border-b border-slate-200/80 bg-white/95 backdrop-blur-sm">
      <div className="mx-auto flex max-w-[1750px] items-center justify-between px-4 py-3.5 sm:px-6 lg:px-8">
        {/* Brand Logo */}
        <div className="flex items-center gap-6">
          <Link
            href="/"
            className="flex items-center gap-2 font-black text-xl tracking-tight text-slate-900 focus:outline-none focus:ring-2 focus:ring-brand rounded-md"
            aria-label="Concesionaria Inicio"
          >
            <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-brand text-white font-black text-lg shadow-sm">
              C
            </span>
            <span className="flex flex-col text-left leading-none">
              <span className="font-extrabold text-base tracking-wide text-slate-900">AUTO</span>
              <span className="text-[10px] font-bold tracking-widest text-brand">PREMIER</span>
            </span>
          </Link>

          {/* Desktop Primary Nav */}
          <nav
            className="hidden md:flex items-center gap-1 text-sm font-semibold text-slate-700"
            aria-label="Navegación principal"
          >
            <Link
              href="/catalogo"
              className="rounded-md px-3 py-2 transition-colors hover:bg-slate-100 hover:text-slate-900 focus:outline-none focus:ring-2 focus:ring-brand"
            >
              Catálogo
            </Link>
            <Link
              href="/comprar"
              className="rounded-md px-3 py-2 transition-colors hover:bg-slate-100 hover:text-slate-900 focus:outline-none focus:ring-2 focus:ring-brand"
            >
              Comprar
            </Link>
            <Link
              href="/catalogo"
              className="rounded-md px-3 py-2 transition-colors hover:bg-slate-100 hover:text-slate-900 focus:outline-none focus:ring-2 focus:ring-brand"
            >
              Buscar coche
            </Link>
            <Link
              href="/contacto"
              className="rounded-md px-3 py-2 transition-colors hover:bg-slate-100 hover:text-slate-900 focus:outline-none focus:ring-2 focus:ring-brand"
            >
              Contacto
            </Link>
          </nav>
        </div>

        {/* Desktop Secondary Nav / User / Notifications */}
        <div className="hidden md:flex items-center gap-3">
          <Link
            href="/comparar"
            className="rounded-full p-2 text-slate-600 transition-colors hover:bg-slate-100 hover:text-slate-900 focus:outline-none focus:ring-2 focus:ring-brand"
            aria-label="Comparar vehículos"
            title="Comparar vehículos"
          >
            <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 6h13M8 12h13M8 18h13M3 6h.01M3 12h.01M3 18h.01" /></svg>
          </Link>
          <Link
            href="/perfil"
            className="rounded-full p-2 text-slate-600 transition-colors hover:bg-slate-100 hover:text-slate-900 focus:outline-none focus:ring-2 focus:ring-brand"
            aria-label="Favoritos"
            title="Favoritos"
          >
            <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 21s-7-4.35-7-10a4 4 0 017-2.65A4 4 0 0119 11c0 5.65-7 10-7 10z" /></svg>
          </Link>
          <Link
            href="/notificaciones"
            className="relative rounded-full p-2 text-slate-600 transition-colors hover:bg-slate-100 hover:text-slate-900 focus:outline-none focus:ring-2 focus:ring-brand"
            aria-label="Notificaciones"
            title="Notificaciones"
          >
            <svg
              className="h-5 w-5"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
              aria-hidden="true"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9"
              />
            </svg>
            <span className="absolute top-1.5 right-1.5 flex h-2 w-2">
              <span className="h-full w-full rounded-full bg-accent-600"></span>
            </span>
          </Link>

          <Link
            href="/perfil"
            className="flex items-center gap-1.5 rounded-md px-3 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-100 hover:text-slate-900 transition-colors focus:outline-none focus:ring-2 focus:ring-brand"
            aria-label="Perfil de usuario"
          >
            <svg
              className="h-5 w-5 text-slate-500"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
              aria-hidden="true"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z"
              />
            </svg>
            <span>Perfil</span>
          </Link>

          {authenticated ? (
            <div className="flex items-center gap-2 border-l border-slate-200 pl-3">
              {roles.some((r) => ["Administrator", "Manager", "InventoryManager"].includes(r)) && (
                <Link
                  href="/admin"
                  className="rounded-md bg-slate-900 px-3 py-1.5 text-xs font-semibold text-white hover:bg-slate-800 transition-colors focus:outline-none focus:ring-2 focus:ring-slate-900"
                >
                  Panel interno
                </Link>
              )}
              <button
                type="button"
                onClick={() => void logout()}
                className="rounded-md border border-slate-200 px-3 py-1.5 text-xs font-semibold text-slate-700 hover:bg-slate-100 transition-colors focus:outline-none focus:ring-2 focus:ring-brand"
              >
                Cerrar sesión
              </button>
            </div>
          ) : (
            <Link
              href="/login"
              className="rounded-lg bg-brand px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-brand-800 transition-colors focus:outline-none focus:ring-2 focus:ring-brand focus:ring-offset-2"
            >
              Ingresar
            </Link>
          )}
        </div>

        {/* Mobile Menu Button */}
        <div className="flex md:hidden items-center gap-2">
          <button
            type="button"
            onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
            className="rounded-md p-2 text-slate-700 hover:bg-slate-100 hover:text-slate-900 focus:outline-none focus:ring-2 focus:ring-brand"
            aria-expanded={mobileMenuOpen}
            aria-label={mobileMenuOpen ? "Cerrar menú principal" : "Abrir menú principal"}
          >
            {mobileMenuOpen ? (
              <svg
                className="h-6 w-6"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
                aria-hidden="true"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M6 18L18 6M6 6l12 12"
                />
              </svg>
            ) : (
              <svg
                className="h-6 w-6"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
                aria-hidden="true"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M4 6h16M4 12h16M4 18h16"
                />
              </svg>
            )}
          </button>
        </div>
      </div>

      {/* Mobile Drawer Navigation */}
      {mobileMenuOpen && (
        <nav
          className="border-b border-slate-200 bg-white px-4 py-4 md:hidden shadow-lg"
          aria-label="Navegación móvil"
        >
          <div className="flex flex-col gap-2 font-medium text-slate-800">
            <Link
              href="/catalogo"
              onClick={() => setMobileMenuOpen(false)}
              className="rounded-md px-3 py-2.5 hover:bg-slate-100"
            >
              Catálogo de vehículos
            </Link>
            <Link
              href="/comprar"
              onClick={() => setMobileMenuOpen(false)}
              className="rounded-md px-3 py-2.5 hover:bg-slate-100"
            >
              Comprar
            </Link>
            <Link
              href="/catalogo"
              onClick={() => setMobileMenuOpen(false)}
              className="rounded-md px-3 py-2.5 hover:bg-slate-100"
            >
              Buscar coche
            </Link>
            <Link
              href="/contacto"
              onClick={() => setMobileMenuOpen(false)}
              className="rounded-md px-3 py-2.5 hover:bg-slate-100"
            >
              Contacto y sucursales
            </Link>
            <div className="my-2 border-t border-slate-100" />
            <Link
              href="/perfil"
              onClick={() => setMobileMenuOpen(false)}
              className="flex items-center gap-2 rounded-md px-3 py-2.5 hover:bg-slate-100"
            >
              <svg
                className="h-5 w-5 text-slate-500"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z"
                />
              </svg>
              Mi Perfil
            </Link>
            <Link
              href="/notificaciones"
              onClick={() => setMobileMenuOpen(false)}
              className="flex items-center gap-2 rounded-md px-3 py-2.5 hover:bg-slate-100"
            >
              <svg
                className="h-5 w-5 text-slate-500"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9"
                />
              </svg>
              Notificaciones
            </Link>

            <div className="mt-3 pt-3 border-t border-slate-100 flex flex-col gap-2">
              {authenticated ? (
                <>
                  {roles.some((r) =>
                    ["Administrator", "Manager", "InventoryManager"].includes(r),
                  ) && (
                    <Link
                      href="/admin"
                      onClick={() => setMobileMenuOpen(false)}
                      className="rounded-md bg-slate-900 px-4 py-2.5 text-center text-sm font-semibold text-white"
                    >
                      Panel de Administración
                    </Link>
                  )}
                  <button
                    type="button"
                    onClick={() => {
                      setMobileMenuOpen(false);
                      void logout();
                    }}
                    className="rounded-md border border-slate-300 px-4 py-2 text-center text-sm font-semibold text-slate-700"
                  >
                    Cerrar sesión
                  </button>
                </>
              ) : (
                <Link
                  href="/login"
                  onClick={() => setMobileMenuOpen(false)}
                  className="rounded-md bg-brand px-4 py-2.5 text-center text-sm font-semibold text-white"
                >
                  Ingresar a mi cuenta
                </Link>
              )}
            </div>
          </div>
        </nav>
      )}
    </header>
  );
}

export function PublicFooter() {
  return (
    <footer className="border-t border-slate-200 bg-slate-900 text-slate-300">
      <div className="mx-auto max-w-[1750px] px-4 py-12 sm:px-6 lg:px-8">
        <div className="grid grid-cols-1 gap-8 sm:grid-cols-2 lg:grid-cols-4">
          <div>
            <div className="flex items-center gap-2">
              <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-brand text-white font-black text-base">
                C
              </span>
              <span className="font-extrabold text-base tracking-wide text-white">
                AUTO PREMIER
              </span>
            </div>
            <p className="mt-3 text-sm text-slate-400 leading-relaxed">
              Plataforma profesional para la compraventa de vehículos certificados, seminuevos y
              garantizados en México.
            </p>
            <div className="mt-4 flex items-center gap-3 text-slate-400">
              <a
                href="#facebook"
                className="hover:text-white transition-colors"
                aria-label="Facebook"
              >
                <svg className="h-5 w-5" fill="currentColor" viewBox="0 0 24 24">
                  <path d="M22 12c0-5.523-4.477-10-10-10S2 6.477 2 12c0 4.991 3.657 9.128 8.438 9.878v-6.987h-2.54V12h2.54V9.797c0-2.506 1.492-3.89 3.777-3.89 1.094 0 2.238.195 2.238.195v2.46h-1.26c-1.243 0-1.63.771-1.63 1.562V12h2.773l-.443 2.89h-2.33v6.988C18.343 21.128 22 16.991 22 12z" />
                </svg>
              </a>
              <a
                href="#whatsapp"
                className="hover:text-white transition-colors"
                aria-label="WhatsApp"
              >
                <svg className="h-5 w-5" fill="currentColor" viewBox="0 0 24 24">
                  <path d="M12.031 6.172c-3.181 0-5.767 2.586-5.768 5.766-.001 1.298.38 2.27 1.019 3.287l-.711 2.598 2.664-.698c.97.545 1.77.828 2.802.828 3.18 0 5.767-2.587 5.768-5.766 0-3.18-2.587-5.815-5.774-5.815zm3.385 8.243c-.144.405-.837.774-1.17.824-.312.045-.693.075-1.954-.447-1.612-.667-2.651-2.316-2.733-2.424-.079-.109-.648-.863-.648-1.646 0-.782.41-1.168.556-1.328.146-.16.32-.2.427-.2.106 0 .213.002.306.006.1.005.233-.038.365.278.136.326.464 1.134.505 1.218.041.083.069.18.014.288-.056.108-.083.176-.166.273-.083.098-.175.218-.25.293-.083.084-.17.175-.073.342.097.166.432.714.928 1.155.638.568 1.176.744 1.343.827.166.083.264.069.362-.042.097-.111.417-.485.528-.652.111-.166.222-.138.375-.083.153.055.972.458 1.139.541.167.083.278.125.319.194.042.07.042.403-.102.808z" />
                </svg>
              </a>
            </div>
          </div>

          <div>
            <h2 className="text-sm font-semibold uppercase tracking-wider text-white">
              Navegación
            </h2>
            <ul className="mt-3 space-y-2 text-sm text-slate-400">
              <li>
                <Link href="/catalogo" className="hover:text-white transition-colors">
                  Catálogo de autos
                </Link>
              </li>
              <li>
                <Link href="/comprar" className="hover:text-white transition-colors">
                  Cómo comprar
                </Link>
              </li>
              <li>
                <Link href="/contacto" className="hover:text-white transition-colors">
                  Sucursales y horarios
                </Link>
              </li>
              <li>
                <Link href="/perfil" className="hover:text-white transition-colors">
                  Área de clientes
                </Link>
              </li>
            </ul>
          </div>

          <div>
            <h2 className="text-sm font-semibold uppercase tracking-wider text-white">
              Sucursales
            </h2>
            <ul className="mt-3 space-y-2 text-sm text-slate-400">
              <li>
                <span className="text-white font-medium">Centro:</span> Av. Principal 100
              </li>
              <li>
                <span className="text-white font-medium">Norte:</span> Blvd. Norte 250
              </li>
              <li>
                <span className="text-white font-medium">Aeropuerto:</span> Carretera Aeropuerto 55
              </li>
              <li>
                <span className="text-slate-400 text-xs">Lunes a Domingo · Atención integral</span>
              </li>
            </ul>
          </div>

          <div>
            <h2 className="text-sm font-semibold uppercase tracking-wider text-white">
              Contacto y Soporte
            </h2>
            <ul className="mt-3 space-y-2 text-sm text-slate-400">
              <li className="flex items-center gap-2">
                <span>Teléfono: (55) 5550-0100</span>
              </li>
              <li>Correo: contacto@concesionaria.local</li>
              <li className="pt-2 text-xs text-slate-400">
                Garantía mecánica, inspección certificada y financiamiento pre-aprobado.
              </li>
            </ul>
          </div>
        </div>

        <div className="mt-12 border-t border-slate-800 pt-6 text-center text-xs text-slate-400">
          <p>
            © {new Date().getFullYear()} Concesionaria Auto Premier. Todos los derechos reservados.
          </p>
        </div>
      </div>
    </footer>
  );
}
