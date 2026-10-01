import { NextResponse } from "next/server";
import { rejectCrossSiteMutation } from "../../../../lib/request-security";
const backend = process.env.BACKEND_URL ?? "http://localhost:5080";
export async function POST(request: Request) {
  const rejected = rejectCrossSiteMutation(request);
  if (rejected) return rejected;
  const response = await fetch(`${backend}/api/v1/customers/register`, { method: "POST", headers: { "Content-Type": "application/json" }, body: await request.text(), cache: "no-store" });
  return new NextResponse(await response.text(), { status: response.status, headers: { "Content-Type": response.headers.get("content-type") ?? "application/json" } });
}
