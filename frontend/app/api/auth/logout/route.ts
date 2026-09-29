import { cookies } from "next/headers";
import { NextResponse } from "next/server";
const backend = process.env.BACKEND_URL ?? "http://localhost:5080";
export async function POST() {
  const store = await cookies();
  const refreshToken = store.get("refresh_token")?.value;
  if (refreshToken)
    await fetch(`${backend}/api/v1/auth/logout`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ refreshToken }),
    });
  store.delete("refresh_token");
  return new NextResponse(null, { status: 204 });
}
