import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig(({ mode }) => {
  const environment = loadEnv(mode, ".", "LYFE_");
  const serverOrigin = environment.LYFE_API_ORIGIN ?? "http://localhost:5080";
  return {
    plugins: [react()],
    server: {
      port: 5173,
      strictPort: true,
      proxy: {
        "/api": serverOrigin,
        "/health": serverOrigin,
      },
    },
  };
});
