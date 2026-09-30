import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";

for (const viewport of [
  { width: 360, height: 800 },
  { width: 1300, height: 900 },
]) {
  test(`published vehicle detail keeps catalog filters at ${viewport.width}px`, async ({
    page,
  }) => {
    await page.setViewportSize(viewport);
    await page.goto("/catalogo?sort=recent");

    const firstCard = page.getByTestId("vehicle-card").first();
    await expect(firstCard).toBeVisible();
    await firstCard.getByRole("link", { name: /Ver detalle de/i }).click();

    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
    await expect(page.getByRole("heading", { name: "Especificaciones" })).toBeVisible();
    const accessibility = await new AxeBuilder({ page }).analyze();
    expect(accessibility.violations).toEqual([]);

    const similar = page.getByRole("heading", { name: "Vehículos similares" });
    await expect(similar).toBeVisible();
    await similar
      .locator("xpath=..")
      .getByRole("link", { name: /Ver detalle de/i })
      .first()
      .click();
    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
    await page.getByRole("link", { name: "Volver al catálogo" }).click();
    await expect(page).toHaveURL(/\/catalogo\?sort=recent/);
  });
}
