import { cookies } from "next/headers";
import { NextResponse } from "next/server";
const backend = process.env.BACKEND_URL ?? "http://localhost:5080";
const cookieOptions = {
  httpOnly: true,
  secure: process.env.AUTH_COOKIE_SECURE === "true" || process.env.NODE_ENV === "production",
  sameSite: "strict" as const,
  path: "/",
  maxAge: 60 * 60 * 24 * 14,
};
export async function POST(request: Request) {
  const response = await fetch(`${backend}/api/v1/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: await request.text(),
    cache: "no-store",
  });
  if (!response.ok)
    return NextResponse.json({ title: "Credenciales inválidas." }, { status: response.status });
  const data = (await response.json()) as {
    accessToken: string;
    refreshToken: string;
    accessTokenExpiresAt: string;
  };
  (await cookies()).set("refresh_token", data.refreshToken, cookieOptions);
  return NextResponse.json({
    accessToken: data.accessToken,
    accessTokenExpiresAt: data.accessTokenExpiresAt,
  });
}
