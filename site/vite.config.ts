import { defineConfig } from "vite";

// The preview runs behind a proxy on 443, so listen on all interfaces,
// accept the proxied host name, and point HMR at the public wss endpoint.
export default defineConfig({
  server: {
    host: "0.0.0.0",
    port: 5173,
    strictPort: true,
    allowedHosts: true,
    hmr: { clientPort: 443, protocol: "wss" },
  },
  preview: {
    host: "0.0.0.0",
    port: 5173,
    strictPort: true,
    allowedHosts: true,
  },
});
