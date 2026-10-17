import { defineConfig } from "@playwright/test";

export default defineConfig({
  testDir: "tests/visual",
  snapshotPathTemplate: "tests/visual/__prints__/{arg}{ext}",
  expect: { toHaveScreenshot: { maxDiffPixelRatio: 0.002 } },
  use: { baseURL: "http://localhost:1420", colorScheme: "light", deviceScaleFactor: 1 },
  webServer: { command: "npx vite --port 1420 --strictPort", url: "http://localhost:1420", reuseExistingServer: true },
});
