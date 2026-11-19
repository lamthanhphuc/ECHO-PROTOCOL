import { expect, test } from "@playwright/test";

test("landing navigation, maps and gallery work by keyboard and pointer", async ({ page }) => {
  await page.goto("/");
  const header = page.locator("header");
  await header.getByRole("link", { name: "Bản đồ" }).click();
  await expect(page).toHaveURL(/#maps$/);
  await expect.poll(async () => Math.round((await header.boundingBox())!.y)).toBe(0);

  const maps = page.locator("#maps");
  await maps.getByRole("button", { name: "Bản đồ tiếp theo" }).click();
  await expect(maps.getByRole("heading", { name: "Vùng ngập" })).toBeVisible();
  await maps.getByRole("button", { name: "Ảnh 5 / 5", exact: true }).click();
  await expect(maps.getByRole("img", { name: "Quái vật bò sát" })).toBeVisible();

  await header.getByRole("link", { name: "Trưng bày" }).click();
  const gallery = page.locator("#gallery");
  await page.route("https://www.youtube-nocookie.com/**", (route) => route.fulfill({ body: "Player stub" }));
  await expect(gallery.locator("iframe")).toHaveCount(0);
  await gallery.getByRole("button", { name: /Phát ECHO PROTOCOL/ }).click();
  await expect(gallery.locator("iframe")).toHaveAttribute("src", /eWeeLHnXCnA/);

  await gallery.getByRole("button", { name: "Hình ảnh" }).click();
  const menu = gallery.getByRole("button", { name: "Menu chính" });
  await menu.focus();
  await page.keyboard.press("Enter");
  const dialog = page.getByRole("dialog");
  await expect(dialog).toBeVisible();
  await page.keyboard.press("ArrowRight");
  await expect(dialog.getByRole("img", { name: "Phòng chờ" })).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(dialog).toBeHidden();

  await header.getByRole("link", { name: "Đăng nhập" }).click();
  await expect(page).toHaveURL(/\/login$/);
  await page.goto("/admin");
  await expect(page).toHaveURL(/\/login\?next=%2Fadmin/);
});

test("landing fits a narrow phone viewport", async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 800 });
  await page.goto("/");
  await page.locator("header").getByRole("link", { name: "Bản đồ" }).click();
  await expect(page).toHaveURL(/#maps$/);
  await page.getByRole("button", { name: "Bản đồ tiếp theo" }).click();
  await expect(page.getByRole("heading", { name: "Vùng ngập" })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(360);
});

test("browser Back from login always opens the landing page", async ({ page }) => {
  await page.goto("/admin");
  await expect(page).toHaveURL(/\/login\?next=%2Fadmin/);
  await page.goBack();
  await expect(page).toHaveURL("/");

  await page.goto("/");
  await page.locator("header").getByRole("link", { name: "Đăng nhập" }).click();
  await expect(page).toHaveURL(/\/login$/);
  await page.goBack();
  await expect(page).toHaveURL("/");
});
