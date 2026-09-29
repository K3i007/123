import "./globals.css";
import { AuthProvider } from "../components/auth-provider";
import { PublicFooter, PublicHeader } from "../components/chrome";

export const metadata = { title: "Concesionaria", description: "Vehículos seminuevos" };
export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="es">
      <body>
        <AuthProvider>
          <PublicHeader />
          <main className="mx-auto min-h-[70vh] max-w-7xl px-5 py-10">{children}</main>
          <PublicFooter />
        </AuthProvider>
      </body>
    </html>
  );
}
