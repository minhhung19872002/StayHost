import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// The SPA is served by ASP.NET Core from wwwroot, so the build lands there.
// emptyOutDir stays off because wwwroot also holds runtime image uploads.
export default defineConfig({
  plugins: [react()],
  build: {
    outDir: '../wwwroot',
    emptyOutDir: false,
    assetsDir: 'assets',
    sourcemap: false,
    rollupOptions: {
      output: {
        manualChunks: {
          react: ['react', 'react-dom', 'react-router-dom'],
          leaflet: ['leaflet']
        }
      }
    }
  },
  server: {
    port: 5273,
    // `npm run dev` talks to the .NET API running on 5199.
    proxy: {
      '/api': 'http://localhost:5199',
      '/uploads': 'http://localhost:5199',
      '/health': 'http://localhost:5199'
    }
  }
});
