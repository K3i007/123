import Link from "next/link";

export default function NotFound() {
  return (
    <section className="py-16 text-center">
      <h1 className="text-4xl font-bold">Página no encontrada</h1>
      <Link className="mt-6 inline-block text-brand underline" href="/">
        Volver al inicio
      </Link>
    </section>
  );
}
