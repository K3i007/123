import "./globals.css";
import type { Metadata } from "next";
import { AuthProvider } from "../components/auth-provider";
import { PublicFooter, PublicHeader } from "../components/chrome";

export const metadata: Metadata = {
  title: {
    default: "Auto Premier | Concesionaria de Autos Seminuevos y Certificados",
    template: "%s | Auto Premier",
  },
  description:
    "Catálogo exclusivo de vehículos certificados, seminuevos y garantizados. Cotiza, agenda tu prueba de manejo y compra con confianza.",
  openGraph: {
    title: "Auto Premier | Concesionaria de Autos",
    description: "Catálogo exclusivo de autos seminuevos y garantizados.",
    type: "website",
    locale: "es_MX",
  },
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="es" className="h-full bg-slate-50 text-slate-900 antialiased">
      <body className="flex min-h-full flex-col font-sans">
        <AuthProvider>
          <PublicHeader />
          <main className="mx-auto w-full max-w-[1750px] flex-1 px-4 sm:px-6 lg:px-8 py-6">
            {children}
          </main>
          <PublicFooter />
        </AuthProvider>
      </body>
    </html>
  );
}
