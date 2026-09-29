import { Protected } from "../../components/protected";
export default function AdminLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <Protected allowedRoles={["Administrator", "Manager"]}>
      <div className="grid gap-6 md:grid-cols-[220px_1fr]">
        <aside className="rounded-lg bg-ink p-5 text-white">
          <strong>Administración</strong>
          <nav className="mt-5">
            <a href="/admin">Resumen</a>
          </nav>
        </aside>
        <section>{children}</section>
      </div>
    </Protected>
  );
}
