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
  if ((await page.title()) !== "EO-Home")
    throw new Error("Application title is not EO-Home");
  if ((await page.locator(".brand b").innerText()) !== "EO·Home")
    throw new Error("Application brand is not EO-Home");
  if (
    !(await page.locator(".brand img").evaluate((img) => img.complete && img.naturalWidth > 0))
  )
    throw new Error("House brand icon did not load");
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
  await page.getByRole("button", { name: /Salon TV/ }).click();
  await page.getByRole("heading", { name: "Salon TV" }).waitFor();
  await page.getByText("Bu cihaz için kontrol sürücüsü yok", { exact: false }).waitFor();
  await page.screenshot({ path: "artifacts/ui/network-device.png" });
  await page.getByRole("button", { name: /Örnek klima/ }).click();
  await page.getByRole("heading", { name: "Örnek klima" }).waitFor();
  const target = page.getByLabel("Klima hedef sıcaklığı");
  await target.fill("22.5");
  await target.dispatchEvent("change");
  await page.getByText("22.5", { exact: true }).waitFor();
  await page.getByRole("button", { name: "Yüksek" }).click();
  await page.getByRole("button", { name: "Yüksek" }).evaluate((el) => {
    if (!el.classList.contains("chosen")) throw new Error("Fan setting did not update");
  });
  await page.setViewportSize({ width: 1100, height: 780 });
  await page.screenshot({ path: "artifacts/ui/climate.png" });
  if (errors.length) throw new Error(errors.join("\n"));
  console.log(
    "PASS: lamp controls, network inventory, presets, rename, themes, climate demo controls, minimum width, no browser errors",
  );
} finally {
  await browser.close();
}
