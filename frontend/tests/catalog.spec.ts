import { test, expect } from "@playwright/test";
import type { components } from "../app/api-types";

const mockFilters: components["schemas"]["PublicFilterOptionsDto"] = {
  makes: [
    {
      id: "make-bmw-1",
      name: "BMW",
      models: [
        {
          id: "model-serie3-1",
          name: "Serie 3",
          variants: [{ id: "var-330i-1", name: "330i M Sport" }],
        },
      ],
    },
    {
      id: "make-toyota-2",
      name: "Toyota",
      models: [
        {
          id: "model-corolla-2",
          name: "Corolla",
          variants: [{ id: "var-hybrid-2", name: "SE Hybrid" }],
        },
      ],
    },
  ],
  transmissions: ["Automática", "Manual", "CVT"],
  // These names match the camel-cased PublicFilterOptionsDto response.
  fuels: ["Gasolina", "Híbrido", "Eléctrico", "Diésel"],
  drivetrains: ["Delantera", "Trasera", "AWD", "4x4"],
  bodyStyles: ["Sedán", "SUV", "Hatchback", "Pickup", "Coupé"],
  branches: [
    { id: "branch-1", name: "Sucursal Central" },
    { id: "branch-2", name: "Sucursal Norte" },
  ],
  minPrice: 150000,
  maxPrice: 1500000,
  minYear: 2016,
  maxYear: 2025,
  minMileage: 0,
  maxMileage: 120000,
  conditions: ["New", "Used"],
};

const mockVehicles: components["schemas"]["PublicVehicleListItemDto"][] = Array.from({
  length: 12,
}).map((_, i) => ({
  id: `10000000-0000-0000-0000-0000000000${(i + 1).toString().padStart(2, "0")}`,
  make: i % 2 === 0 ? "BMW" : "Toyota",
  model: i % 2 === 0 ? "Serie 3" : "Corolla",
  variant: i % 2 === 0 ? "330i M Sport" : "SE Hybrid",
  year: 2021 + (i % 4),
  price: 350000 + i * 50000,
  currency: "MXN",
  mileage: 15000 + i * 8000,
  transmission: "Automática",
  fuel: i % 2 === 0 ? "Gasolina" : "Híbrido",
  drivetrain: i % 2 === 0 ? "Trasera" : "Delantera",
  bodyStyle: "Sedán",
  branch: "Sucursal Central",
  branchId: "branch-1",
  imageUrl: null,
  condition: i === 0 ? "New" : "Used",
  createdAt: "2026-09-20T10:00:00Z",
}));

async function setupMocks(page: any) {
  await page.route("**/api/proxy/**", async (route: any) => {
    const url = new URL(route.request().url());

    if (url.pathname.includes("/vehicles/filters")) {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(mockFilters),
      });
    }

    if (url.pathname.includes("/vehicles")) {
      const q = url.searchParams.get("q")?.toLowerCase();
      const makeId = url.searchParams.get("makeId");
      const minPrice = Number(url.searchParams.get("minPrice") || "0");
      const maxPrice = Number(url.searchParams.get("maxPrice") || "99999999");

      let filtered = [...mockVehicles];
      if (q) {
        filtered = filtered.filter(
          (v) =>
            v.make.toLowerCase().includes(q) ||
            v.model.toLowerCase().includes(q) ||
            (v.variant && v.variant.toLowerCase().includes(q)),
        );
      }
      if (makeId === "make-bmw-1") {
        filtered = filtered.filter((v) => v.make === "BMW");
      } else if (makeId === "make-toyota-2") {
        filtered = filtered.filter((v) => v.make === "Toyota");
      }
      if (minPrice > 0) {
        filtered = filtered.filter((v) => v.price >= minPrice);
      }
      if (maxPrice < 99999999) {
        filtered = filtered.filter((v) => v.price <= maxPrice);
      }

      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          items: filtered,
          page: 1,
          pageSize: 12,
          total: filtered.length,
          totalPages: Math.ceil(filtered.length / 12) || 1,
        }),
      });
    }

    return route.continue();
  });
}

test.describe("Public Catalog E2E & Responsiveness", () => {
  test.beforeEach(async ({ page }) => {
    await setupMocks(page);
  });

  test("Flow: search -> filter -> clear filters", async ({ page }) => {
    await page.goto("/catalogo");

    // Check search input
    const searchInput = page.locator("#catalog-search");
    await expect(searchInput).toBeVisible();

    // Type "bmw" into search
    await searchInput.fill("bmw");
    // Wait for debounce
    await page.waitForTimeout(400);

    // Verify URL updated with q=bmw
    await expect(page).toHaveURL(/q=bmw/);

    // Verify cards contain BMW
    await expect(page.locator('[data-testid="vehicle-card"]').first()).toContainText("BMW");

    // The first render is SSR against the actual API, so use the typed option it returned.
    const makeSelect = page.locator("#filter-make");
    const toyotaOption = makeSelect.locator("option", { hasText: "Toyota" });
    const toyotaId = await toyotaOption.getAttribute("value");
    expect(toyotaId).toBeTruthy();
    await makeSelect.selectOption(toyotaId!);
    await page.waitForTimeout(300);

    // Verify URL reflects makeId
    await expect(page).toHaveURL(new RegExp(`makeId=${toyotaId}`));

    // Click "Limpiar filtros" in the sidebar
    const clearBtn = page.getByRole("button", { name: "Limpiar filtros" }).first();
    await clearBtn.click();

    // Verify URL reset to /catalogo and search cleared
    await expect(page).toHaveURL("/catalogo");
    await expect(searchInput).toHaveValue("");
  });

  test("Empty state displayed on impossible query and recovers with clear button", async ({
    page,
  }) => {
    await page.goto("/catalogo");

    const searchInput = page.locator("#catalog-search");
    await searchInput.fill("VehiculoInexistente999");
    await page.waitForTimeout(400);

    // Verify empty state message
    await expect(
      page.getByText("No se encontraron vehículos con las condiciones seleccionadas"),
    ).toBeVisible();

    // Click clear button in empty state
    const clearBtn = page.getByTestId("empty-clear-btn");
    await expect(clearBtn).toBeVisible();
    await clearBtn.click();

    // Verify reset
    await expect(page).toHaveURL("/catalogo");
    await expect(
      page.getByText("No se encontraron vehículos con las condiciones seleccionadas"),
    ).not.toBeVisible();
  });

  test("URL persistence: query params restored on reload", async ({ page }) => {
    await page.goto("/catalogo?q=toyota");

    const searchInput = page.locator("#catalog-search");
    await expect(searchInput).toHaveValue("toyota");

    const makeSelect = page.locator("#filter-make");
    const toyotaOption = makeSelect.locator("option", { hasText: "Toyota" });
    const toyotaId = await toyotaOption.getAttribute("value");
    expect(toyotaId).toBeTruthy();
    await makeSelect.selectOption(toyotaId!);
    await expect(makeSelect).toHaveValue(toyotaId!);

    // Reload page
    await page.reload();
    await expect(searchInput).toHaveValue("toyota");
    await expect(makeSelect).toHaveValue(toyotaId!);
  });

  test("Responsive grid columns and sticky filter panel at 1024px, 1300px, and 1600px", async ({
    page,
  }) => {
    // 1024px viewport
    await page.setViewportSize({ width: 1024, height: 800 });
    await page.goto("/catalogo");

    // Check sticky desktop sidebar
    const filterPanel = page.locator('[data-testid="filter-panel"]');
    await expect(filterPanel).toBeVisible();

    const panelBox = await filterPanel.boundingBox();
    expect(panelBox?.width).toBeCloseTo(288, 1);

    const isSticky = await filterPanel.evaluate((el) => {
      return window.getComputedStyle(el).position === "sticky";
    });
    expect(isSticky).toBe(true);

    // Verify 2 columns at 1024px (base desktop)
    const grid = page.locator('[data-testid="vehicles-grid"]');
    await expect(grid).toBeVisible();
    const cols1024 = await grid.evaluate((el) => {
      return window.getComputedStyle(el).gridTemplateColumns.split(" ").length;
    });
    expect(cols1024).toBe(2);

    // 1300px viewport -> 3 columns
    await page.setViewportSize({ width: 1300, height: 900 });
    await page.waitForTimeout(200);
    const cols1300 = await grid.evaluate((el) => {
      return window.getComputedStyle(el).gridTemplateColumns.split(" ").length;
    });
    expect(cols1300).toBe(3);

    // 1600px viewport -> 4 columns
    await page.setViewportSize({ width: 1600, height: 1000 });
    await page.waitForTimeout(200);
    const cols1600 = await grid.evaluate((el) => {
      return window.getComputedStyle(el).gridTemplateColumns.split(" ").length;
    });
    expect(cols1600).toBe(4);
  });

  test("Mobile view at 768px: compact drawer and single column", async ({ page }) => {
    await page.setViewportSize({ width: 768, height: 800 });
    await page.goto("/catalogo");

    // Desktop filter panel hidden
    const desktopPanel = page.locator('[data-testid="filter-panel"]');
    await expect(desktopPanel).toBeHidden();

    // Mobile filter button visible
    const mobileBtn = page.getByRole("button", { name: "Filtros", exact: true });
    await expect(mobileBtn).toBeVisible();

    // 1 column on mobile
    const grid = page.locator('[data-testid="vehicles-grid"]');
    await expect(grid).toBeVisible();
    const colsMobile = await grid.evaluate((el) => {
      return window.getComputedStyle(el).gridTemplateColumns.split(" ").length;
    });
    expect(colsMobile).toBe(1);
  });

  test("Price range exposes two keyboard-operable sliders", async ({ page }) => {
    await page.goto("/catalogo");
    const minimum = page.getByRole("slider", { name: "Precio mínimo slider" });
    const maximum = page.getByRole("slider", { name: "Precio máximo slider" });
    await expect(minimum).toBeVisible();
    await expect(maximum).toBeVisible();
    await minimum.focus();
    await page.keyboard.press("ArrowRight");
    await expect(page).toHaveURL(/minPrice=/);
  });
});
