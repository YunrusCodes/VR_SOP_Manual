import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

// Built assets land under /admin/ so FastAPI's StaticFiles mount can serve them
// from the same origin as the API. Dev proxy forwards the API paths to the
// uvicorn process running on :8000.
export default defineConfig({
  base: '/admin/',
  plugins: [react(), tailwindcss()],
  build: {
    outDir: 'dist',
    emptyOutDir: true,
  },
  server: {
    port: 5173,
    proxy: {
      '/companies': 'http://localhost:8000',
      '/healthz': 'http://localhost:8000',
    },
  },
});
