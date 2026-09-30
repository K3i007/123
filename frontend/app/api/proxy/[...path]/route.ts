import { NextResponse } from "next/server";

const backend = process.env.BACKEND_URL ?? "http://localhost:5080";

async function forward(request: Request, { params }: { params: Promise<{ path: string[] }> }) {
  const { path } = await params;
  const requestUrl = new URL(request.url);
  const targetUrl = `${backend}/api/v1/${path.join("/")}${requestUrl.search}`;

  const headers: Record<string, string> = {
    "Content-Type": request.headers.get("content-type") ?? "application/json",
  };
  const auth = request.headers.get("authorization");
  if (auth) headers["Authorization"] = auth;
  const ifMatch = request.headers.get("if-match");
  if (ifMatch) headers["If-Match"] = ifMatch;

  const response = await fetch(targetUrl, {
    method: request.method,
    headers,
    body: request.method === "GET" || request.method === "HEAD" ? undefined : await request.text(),
    cache: "no-store",
  });

  const responseHeaders = new Headers();
  responseHeaders.set("Content-Type", response.headers.get("Content-Type") ?? "application/json");
  const cacheControl = response.headers.get("Cache-Control");
  if (cacheControl) responseHeaders.set("Cache-Control", cacheControl);

  return new NextResponse(response.body, {
    status: response.status,
    headers: responseHeaders,
  });
}

export const GET = forward;
export const POST = forward;
export const PUT = forward;
export const DELETE = forward;
