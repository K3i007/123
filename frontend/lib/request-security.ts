import { NextResponse } from "next/server";

export function rejectCrossSiteMutation(request: Request): NextResponse | null {
  const origin = request.headers.get("origin");
  const site = request.headers.get("sec-fetch-site");
  const expected = process.env.NEXT_PUBLIC_SITE_URL ?? "http://localhost:3000";
  if ((origin && origin !== expected) || (site && site !== "same-origin" && site !== "same-site")) {
    return NextResponse.json({ title: "Solicitud no permitida." }, { status: 403 });
  }
  return null;
}

export const refreshCookieOptions = {
  httpOnly: true,
  secure: process.env.AUTH_COOKIE_SECURE === "true" || process.env.NODE_ENV === "production",
  sameSite: "strict" as const,
  path: "/",
  maxAge: 60 * 60 * 24 * 14,
};
