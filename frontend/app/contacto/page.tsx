import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Contacto y Sucursales",
  description:
    "Conoce nuestras ubicaciones, horarios de atención y números de contacto en Auto Premier.",
};

const backend = process.env.BACKEND_URL ?? "http://localhost:5080";

async function getBranches(): Promise<
  {
    id: string;
    name: string;
    address: string;
    phones: string;
    hours: string;
    managerName?: string;
  }[]
> {
  try {
    const res = await fetch(`${backend}/api/v1/public/branches`, {
      cache: "no-store",
    });
    if (!res.ok) return [];
    return await res.json();
  } catch {
    return [];
  }
}

export default async function ContactoPage() {
  const branches = await getBranches();

  return (
    <div className="mx-auto max-w-5xl py-8 space-y-12">
      <div className="text-center space-y-3">
        <span className="text-xs font-bold uppercase tracking-wider text-brand">
          ATENCIÓN AL CLIENTE
        </span>
        <h1 className="text-3xl font-black text-slate-900 sm:text-5xl">Sucursales y Contacto</h1>
        <p className="text-base text-slate-600 max-w-xl mx-auto">
          Estamos a tu disposición en nuestras tres sucursales para asesorarte en la elección y
          financiamiento de tu vehículo.
        </p>
      </div>

      <div className="grid grid-cols-1 gap-6 md:grid-cols-3">
        {branches.map((b) => {
          let parsedPhones: string[] = [];
          try {
            parsedPhones = JSON.parse(b.phones);
          } catch {
            parsedPhones = [b.phones];
          }

          let parsedHours = "";
          try {
            const h = JSON.parse(b.hours);
            parsedHours = Object.entries(h)
              .map(([k, v]) => `${k.toUpperCase()}: ${v}`)
              .join(" | ");
          } catch {
            parsedHours = b.hours;
          }

          return (
            <div
              key={b.id}
              className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm flex flex-col justify-between"
            >
              <div>
                <span className="inline-block rounded-full bg-teal-50 px-3 py-1 text-xs font-bold text-brand">
                  Sucursal Oficial
                </span>
                <h2 className="mt-3 text-xl font-bold text-slate-900">Sucursal {b.name}</h2>
                <div className="mt-4 space-y-2 text-sm text-slate-600">
                  <p className="flex items-start gap-2">
                    <svg
                      className="h-5 w-5 text-slate-400 shrink-0 mt-0.5"
                      fill="none"
                      viewBox="0 0 24 24"
                      stroke="currentColor"
                    >
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth={2}
                        d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z"
                      />
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth={2}
                        d="M15 11a3 3 0 11-6 0 3 3 0 016 0z"
                      />
                    </svg>
                    <span>{b.address}</span>
                  </p>

                  <p className="flex items-center gap-2">
                    <svg
                      className="h-5 w-5 text-slate-400 shrink-0"
                      fill="none"
                      viewBox="0 0 24 24"
                      stroke="currentColor"
                    >
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth={2}
                        d="M3 5a2 2 0 012-2h3.28a1 1 0 01.948.684l1.498 4.493a1 1 0 01-.502 1.21l-2.257 1.13a11.042 11.042 0 005.516 5.516l1.13-2.257a1 1 0 011.21-.502l4.493 1.498a1 1 0 01.684.949V19a2 2 0 01-2 2h-1C9.716 21 3 14.284 3 6V5z"
                      />
                    </svg>
                    <span>{parsedPhones.join(", ") || "(55) 5550-0100"}</span>
                  </p>

                  <p className="flex items-center gap-2">
                    <svg
                      className="h-5 w-5 text-slate-400 shrink-0"
                      fill="none"
                      viewBox="0 0 24 24"
                      stroke="currentColor"
                    >
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth={2}
                        d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z"
                      />
                    </svg>
                    <span className="text-xs text-slate-500">
                      {parsedHours || "Lunes a Domingo · 09:00 - 18:00"}
                    </span>
                  </p>
                </div>
              </div>

              {b.managerName && (
                <div className="mt-6 border-t border-slate-100 pt-3 text-xs text-slate-500">
                  Gerente de sucursal: <strong className="text-slate-700">{b.managerName}</strong>
                </div>
              )}
            </div>
          );
        })}
      </div>

      <div className="rounded-2xl border border-slate-200 bg-slate-50 p-8 text-center space-y-2">
        <h3 className="font-bold text-slate-900 text-lg">Central de Ventas y Atención Digital</h3>
        <p className="text-sm text-slate-600 max-w-lg mx-auto">
          ¿Tienes dudas sobre un vehículo en particular? Puedes llamarnos al (55) 5550-0100 o
          escribirnos a contacto@concesionaria.local.
        </p>
      </div>
    </div>
  );
}
