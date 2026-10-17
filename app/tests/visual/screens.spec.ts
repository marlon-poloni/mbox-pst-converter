import { expect, test, type Page } from "@playwright/test";

const sizes = [
  { name: "1280", width: 1280, height: 800 },
  { name: "1024", width: 1024, height: 680 },
];

const click = (page: Page, text: string) => page.getByRole("button", { name: text, exact: true }).first().click();

async function open(page: Page, cenario = "instantaneo") {
  await page.goto(`/?cenario=${cenario}`);
  await page.evaluate(() => document.fonts.ready);
}

async function toFolders(page: Page, cenario = "instantaneo") {
  await open(page, cenario);
  await click(page, "Escolher arquivo");
  await click(page, "Continuar");
  await expect(page.locator(".etapas .atual")).toHaveText("Pastas");
}

async function noHorizontalScroll(page: Page) {
  const { sw, iw } = await page.evaluate(() => ({ sw: document.documentElement.scrollWidth, iw: innerWidth }));
  expect(sw).toBeLessThanOrEqual(iw);
}

for (const size of sizes) {
  test.describe(`${size.name}`, () => {
    test.use({ viewport: { width: size.width, height: size.height } });

    test("arquivo-inicio", async ({ page }) => {
      await open(page);
      await noHorizontalScroll(page);
      await expect(page).toHaveScreenshot(`${size.name}-1-arquivo-inicio.png`);
    });

    test("arquivo-lendo", async ({ page }) => {
      await open(page, "lento");
      await page.evaluate(() => (window as never as { __mock: { emit: (e: unknown) => void } }).__mock);
      await click(page, "Escolher arquivo");
      await page.evaluate(() => (window as unknown as { __mock: { emit: (e: unknown) => void } }).__mock.emit({ event: "scanProgress", bytesRead: 3460000000, sourceBytes: 6654983453 }));
      await page.evaluate(() => new Promise((r) => setTimeout(r, 30)));
      await expect(page.locator(".progresso-texto")).toContainText("lidos");
      await noHorizontalScroll(page);
      await expect(page).toHaveScreenshot(`${size.name}-2-arquivo-lendo.png`, { mask: [page.locator(".progresso")] });
    });

    test("arquivo-pronto", async ({ page }) => {
      await open(page);
      await click(page, "Escolher arquivo");
      await expect(page.locator(".numeros")).toContainText("6.614");
      await noHorizontalScroll(page);
      await expect(page).toHaveScreenshot(`${size.name}-3-arquivo-pronto.png`);
    });

    test("pastas", async ({ page }) => {
      await toFolders(page);
      await noHorizontalScroll(page);
      await expect(page).toHaveScreenshot(`${size.name}-4-pastas.png`);
    });

    test("editor", async ({ page }) => {
      await toFolders(page);
      await page.locator(".lista .item", { hasText: "Fornecedores" }).click();
      await expect(page.getByRole("dialog")).toBeVisible();
      await noHorizontalScroll(page);
      await expect(page).toHaveScreenshot(`${size.name}-5-editor.png`);
    });

    test("editor-erro", async ({ page }) => {
      await toFolders(page);
      await page.locator(".lista .item", { hasText: "Processos 2025" }).click();
      await page.getByLabel("Nome da pasta").fill("");
      await page.getByRole("dialog").getByRole("button", { name: "Aplicar" }).click();
      await expect(page.getByRole("dialog").locator(".erro")).toHaveText("Dê um nome para a pasta.");
      await expect(page).toHaveScreenshot(`${size.name}-6-editor-erro.png`);
    });

    test("pastas-spam-fora", async ({ page }) => {
      await toFolders(page);
      await page.locator(".lista .item", { hasText: "Spam" }).getByRole("button", { name: "Não converter" }).click();
      await click(page, "Só na primeira pasta");
      await expect(page.locator(".resumo")).toContainText("ficam fora");
      await noHorizontalScroll(page);
      await expect(page).toHaveScreenshot(`${size.name}-7-pastas-spam-fora.png`);
    });

    test("tamanho", async ({ page }) => {
      await toFolders(page);
      await click(page, "Dividir a cada 20 GB");
      await expect(page.getByRole("dialog")).toBeVisible();
      await expect(page).toHaveScreenshot(`${size.name}-8-tamanho.png`);
    });

    test("convertendo-e-confirmacao", async ({ page }) => {
      await toFolders(page);
      await page.evaluate(() => { (window as unknown as { __hold: boolean }).__hold = true; });
      await click(page, "Converter");
      await page.evaluate(() => (window as unknown as { __mock: { emit: (e: unknown) => void } }).__mock.emit({ event: "convertProgress", done: 3412, total: 6614, bytesRead: 3435973837, sourceBytes: 6654983453, megabytesPerSecond: 138, remainingSeconds: 23 }));
      await expect(page.locator(".contagem")).toContainText("3.412");
      await noHorizontalScroll(page);
      await expect(page).toHaveScreenshot(`${size.name}-9-convertendo.png`);
      await click(page, "Cancelar conversão");
      await expect(page.getByRole("dialog")).toBeVisible();
      await expect(page).toHaveScreenshot(`${size.name}-10-cancelar.png`);
    });

    test("resultado-ok", async ({ page }) => {
      await toFolders(page);
      await click(page, "Converter");
      await expect(page.locator("h1")).toHaveText("Conversão concluída", { timeout: 10000 });
      await noHorizontalScroll(page);
      await expect(page).toHaveScreenshot(`${size.name}-11-resultado.png`);
    });

    test("resultado-diferenca", async ({ page }) => {
      await toFolders(page, "diferenca");
      await click(page, "Converter");
      await expect(page.locator("h1")).toHaveText("Conversão concluída com diferenças", { timeout: 10000 });
      await expect(page).toHaveScreenshot(`${size.name}-12-resultado-diferenca.png`);
    });

    test("falha", async ({ page }) => {
      await toFolders(page, "disco-cheio");
      await click(page, "Converter");
      await expect(page.locator("h1")).toHaveText("A conversão não terminou");
      await noHorizontalScroll(page);
      await expect(page).toHaveScreenshot(`${size.name}-13-falha.png`);
    });
  });
}
