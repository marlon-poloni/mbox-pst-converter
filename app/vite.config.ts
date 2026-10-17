import { defineConfig } from "vite";

export default defineConfig({
  clearScreen: false,
  server: { port: 1420, strictPort: true },
  build: { target: "es2022", outDir: "dist" },
  test: { environment: "jsdom", globals: true, include: ["tests/**/*.spec.ts"], exclude: ["tests/visual/**"] },
});
