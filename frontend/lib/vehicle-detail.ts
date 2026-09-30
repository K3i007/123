export function sanitizeCatalogReturn(value: string | undefined): string {
  if (!value || value.startsWith("//") || value.includes(":") || !value.startsWith("/"))
    return "/catalogo";
  return value === "/" || value === "/catalogo" || value.startsWith("/catalogo?")
    ? value
    : "/catalogo";
}

export function safeJsonLd(value: unknown): string {
  return JSON.stringify(value).replace(/</g, "\\u003c");
}

export function telephoneHref(phone: string): string {
  return `tel:${phone.replace(/[^+\d]/g, "")}`;
}

export function parsePublicCustomFields(raw: string): Record<string, string | number | boolean> {
  try {
    const value: unknown = JSON.parse(raw);
    return value && typeof value === "object" && !Array.isArray(value)
      ? (value as Record<string, string | number | boolean>)
      : {};
  } catch {
    return {};
  }
}
