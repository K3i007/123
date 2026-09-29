"use client";
import { createContext, useContext, useEffect, useMemo, useState } from "react";
import { setAccessToken } from "../lib/api";

type AuthContextValue = {
  ready: boolean;
  authenticated: boolean;
  roles: string[];
  login(email: string, password: string): Promise<void>;
  logout(): Promise<void>;
};
const AuthContext = createContext<AuthContextValue | undefined>(undefined);
export function AuthProvider({ children }: Readonly<{ children: React.ReactNode }>) {
  const [token, setToken] = useState<string | null>(null);
  const [ready, setReady] = useState(false);
  const roles = useMemo(() => (token ? readRoles(token) : []), [token]);
  function save(value: string | null) {
    setAccessToken(value);
    setToken(value);
  }
  useEffect(() => {
    fetch("/api/auth/refresh", { method: "POST" })
      .then(async (response) => {
        if (response.ok) save(((await response.json()) as { accessToken: string }).accessToken);
      })
      .finally(() => setReady(true));
  }, []);
  const value = useMemo<AuthContextValue>(
    () => ({
      ready,
      authenticated: token !== null,
      roles,
      async login(email, password) {
        const response = await fetch("/api/auth/login", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ email, password }),
        });
        if (!response.ok) throw new Error("Correo o contraseña inválidos.");
        save(((await response.json()) as { accessToken: string }).accessToken);
      },
      async logout() {
        await fetch("/api/auth/logout", { method: "POST" });
        save(null);
      },
    }),
    [ready, token, roles],
  );
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
export function useAuth() {
  const value = useContext(AuthContext);
  if (!value) throw new Error("AuthProvider no está disponible.");
  return value;
}
function readRoles(token: string): string[] {
  try {
    const payload = JSON.parse(
      atob(token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/")),
    ) as Record<string, unknown>;
    const role = payload["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"];
    return Array.isArray(role)
      ? role.filter((item): item is string => typeof item === "string")
      : typeof role === "string"
        ? [role]
        : [];
  } catch {
    return [];
  }
}
