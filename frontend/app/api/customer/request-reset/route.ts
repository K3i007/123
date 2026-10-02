import { NextResponse } from "next/server";
import { rejectCrossSiteMutation } from "../../../../lib/request-security";

const backend = process.env.BACKEND_URL ?? "http://localhost:5080";

export async function POST(request: Request) {
  const rejected = rejectCrossSiteMutation(request);
  if (rejected) return rejected;
  const { email } = await request.json();
  const response = await fetch(`${backend}/api/v1/customers/password-reset`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email }),
    cache: "no-store"
  });
  return new NextResponse(null, { status: response.status });
}
