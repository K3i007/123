import { NextResponse } from "next/server";
const backend = process.env.BACKEND_URL ?? "http://localhost:5080";
async function forward(request: Request, { params }: { params: Promise<{ path: string[] }> }) {
  const { path } = await params;
  const response = await fetch(`${backend}/api/v1/${path.join("/")}`, {
    method: request.method,
    headers: {
      Authorization: request.headers.get("authorization") ?? "",
      "Content-Type": request.headers.get("content-type") ?? "application/json",
      "If-Match": request.headers.get("if-match") ?? "",
    },
    body: request.method === "GET" ? undefined : await request.text(),
    cache: "no-store",
  });
  return new NextResponse(response.body, {
    status: response.status,
    headers: { "Content-Type": response.headers.get("Content-Type") ?? "application/json" },
  });
}
export const GET = forward;
export const POST = forward;
export const PUT = forward;
export const DELETE = forward;
