import { expect, test } from "@playwright/test";
import {
  parsePublicCustomFields,
  safeJsonLd,
  sanitizeCatalogReturn,
  telephoneHref,
} from "../lib/vehicle-detail";

test("the structured data escapes a malicious value", () => {
  const json = safeJsonLd({ name: "<script>alert(1)</script>" });
  expect(json).toContain("\\u003cscript>");
  expect(json).not.toContain("<script>");
});

test("the catalog return path cannot become an external navigation", () => {
  expect(sanitizeCatalogReturn("/catalogo?makeId=toyota")).toBe("/catalogo?makeId=toyota");
  expect(sanitizeCatalogReturn("/")).toBe("/");
  expect(sanitizeCatalogReturn("//evil.com")).toBe("/catalogo");
  expect(sanitizeCatalogReturn("javascript:alert(1)")).toBe("/catalogo");
  expect(telephoneHref("(55) 5550-0100")).toBe("tel:5555500100");
  expect(parsePublicCustomFields("not-json")).toEqual({});
});
