import { chromium } from "@playwright/test";
import { mkdir } from "node:fs/promises";
await mkdir("artifacts/ui", { recursive: true });
const browser = await chromium.launch({ channel: "msedge", headless: true });
const page = await browser.newPage({
  viewport: { width: 1100, height: 780 },
  deviceScaleFactor: 1,
});
const errors = [];
page.on("pageerror", (e) => errors.push(String(e)));
try {
  await page.goto("http://127.0.0.1:5173/?demo");
  await page.getByRole("heading", { name: "Çalışma lambası", exact: true }).waitFor();
  if (!(await page.getByLabel("Özel renk seç").isDisabled()))
    throw new Error("Mono RGB enabled");
  if (!(await page.getByLabel("Beyaz ışık sıcaklığı").isDisabled()))
    throw new Error("Mono CT enabled");
  await page.screenshot({ path: "artifacts/ui/dark-mono.png" });
  await page.getByRole("button", { name: /Renkli lamba/ }).click();
  if (await page.getByLabel("Özel renk seç").isDisabled())
    throw new Error("RGB lamp controls disabled");
  await page.getByLabel("Parlaklık", { exact: true }).fill("74");
  await page.getByLabel("Parlaklık", { exact: true }).dispatchEvent("change");
  await page
    .getByRole("button", { name: "Ayar 2 için mevcut parlaklığı kaydet" })
    .click();
  if (!(await page.getByRole("button", { name: "AYAR 02 %74" }).count()))
    throw new Error("Preset did not save");
  await page.screenshot({ path: "artifacts/ui/dark-rgb.png" });
  await page.getByRole("button", { name: "Temayı değiştir" }).click();
  await page.screenshot({ path: "artifacts/ui/light-rgb.png" });
  await page.getByRole("button", { name: /Çalışma lambası Açık/ }).click();
  if (!(await page.getByRole("button", { name: "AYAR 02 %30" }).count()))
    throw new Error("Preset leaked across lamps");
  await page.getByRole("button", { name: "Ampul adı ve ayrıntıları" }).click();
  await page.locator("#device-name").fill("Yeni ad");
  await page.getByRole("button", { name: "İsmi kaydet" }).click();
  await page
    .getByRole("heading", { name: "Yeni ad", exact: true })
    .waitFor();
  await page.setViewportSize({ width: 820, height: 640 });
  await page.screenshot({ path: "artifacts/ui/small.png" });
  if (
    await page.evaluate(() => document.documentElement.scrollWidth > innerWidth)
  )
    throw new Error("Horizontal overflow");
  if (errors.length) throw new Error(errors.join("\n"));
  console.log(
    "PASS: mono/RGB capability gating, lamp selection, per-lamp presets, rename, themes, minimum width, no browser errors",
  );
} finally {
  await browser.close();
}
