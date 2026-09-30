import type { MetadataRoute } from "next";

// The sitemap is rendered at request time so `next build` never requires the API to be running.
export const dynamic = "force-dynamic";

const backend = process.env.BACKEND_URL ?? "http://localhost:5080";
const baseUrl = process.env.NEXT_PUBLIC_SITE_URL ?? "http://localhost:3000";

interface VehicleListItem {
  id: string;
  createdAt: string;
}

export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const staticRoutes: MetadataRoute.Sitemap = [
    {
      url: `${baseUrl}`,
      lastModified: new Date(),
      changeFrequency: "daily",
      priority: 1.0,
    },
    {
      url: `${baseUrl}/catalogo`,
      lastModified: new Date(),
      changeFrequency: "hourly",
      priority: 0.9,
    },
    {
      url: `${baseUrl}/comprar`,
      lastModified: new Date(),
      changeFrequency: "weekly",
      priority: 0.8,
    },
    {
      url: `${baseUrl}/contacto`,
      lastModified: new Date(),
      changeFrequency: "monthly",
      priority: 0.7,
    },
  ];

  // Metadata routes can be evaluated while compiling. Keep production builds
  // independent from a locally running API; runtime requests still paginate it.
  if (process.env.NEXT_PHASE === "phase-production-build") {
    return staticRoutes;
  }

  try {
    const vehicleRoutes: MetadataRoute.Sitemap = [];
    let page = 1;
    while (true) {
      const res = await fetch(`${backend}/api/v1/public/vehicles?page=${page}&pageSize=48`, {
        next: { revalidate: 300 },
      });
      if (!res.ok) break;
      const data = await res.json();
      vehicleRoutes.push(
        ...(data.items || []).map((v: VehicleListItem) => ({
          url: `${baseUrl}/vehiculos/${v.id}`,
          lastModified: new Date(v.createdAt),
          changeFrequency: "weekly" as const,
          priority: 0.8,
        })),
      );
      if (page * data.pageSize >= data.total) break;
      page += 1;
    }
    return [...staticRoutes, ...vehicleRoutes];
  } catch {
    return staticRoutes;
  }
}
