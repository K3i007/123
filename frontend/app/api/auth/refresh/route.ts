import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { rejectCrossSiteMutation, refreshCookieOptions as cookieOptions } from "../../../../lib/request-security";
const backend = process.env.BACKEND_URL ?? "http://localhost:5080";
export async function POST(request: Request) {
  const rejected = rejectCrossSiteMutation(request);
  if (rejected) return rejected;
  const store = await cookies();
  const refreshToken = store.get("refresh_token")?.value;
  if (!refreshToken) return NextResponse.json({ title: "Sesión no disponible." }, { status: 401 });
  const response = await fetch(`${backend}/api/v1/auth/refresh`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ refreshToken }),
    cache: "no-store",
  });
  if (!response.ok) {
    store.delete("refresh_token");
    return NextResponse.json({ title: "Sesión expirada." }, { status: 401 });
  }
  const data = (await response.json()) as {
    accessToken: string;
    refreshToken: string;
    accessTokenExpiresAt: string;
  };
  store.set("refresh_token", data.refreshToken, cookieOptions);
  return NextResponse.json({
    accessToken: data.accessToken,
    accessTokenExpiresAt: data.accessTokenExpiresAt,
  });
}
