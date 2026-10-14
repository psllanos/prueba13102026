import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // Proxied so the browser talks to one origin in development and there is no CORS hole.
    // In production the CDN/API Gateway owns this routing.
    proxy: {
      // The client calls `/api/...`; the API itself serves `/providers/available` and `/optimize`
      // with NO prefix, so the rewrite strips it. Without this the API returns 404.
      '/api': {
        target: process.env.VITE_API_TARGET ?? 'http://localhost:8080',
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/api/, ''),
      },
    },
  },
});
