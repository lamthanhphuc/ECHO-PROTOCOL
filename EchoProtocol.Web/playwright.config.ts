import { defineConfig } from "@playwright/test";

const baseURL = "http://127.0.0.1:3421";

export default defineConfig({
  testDir: "./tests/browser",
  use: { baseURL },
  webServer: {
    command: "npm run start -- --hostname 127.0.0.1 --port 3421",
    url: baseURL,
  },
});
