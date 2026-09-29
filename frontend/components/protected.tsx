"use client";
import { useRouter } from "next/navigation";
import { useEffect } from "react";
import { useAuth } from "./auth-provider";
export function Protected({
  children,
  allowedRoles,
}: Readonly<{ children: React.ReactNode; allowedRoles: string[] }>) {
  const { ready, authenticated, roles } = useAuth();
  const router = useRouter();
  const authorized = allowedRoles.some((role) => roles.includes(role));
  useEffect(() => {
    if (ready && (!authenticated || !authorized)) router.replace("/login");
  }, [ready, authenticated, authorized, router]);
  if (!ready) return <p>Verificando acceso...</p>;
  return authenticated && authorized ? <>{children}</> : null;
}
