import { rmSync } from 'node:fs';
import { resolve } from 'node:path';
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// The SPA is served by ASP.NET Core from wwwroot, so the build lands there.
// emptyOutDir stays off because wwwroot also holds runtime image uploads.
// emptyOutDir has to stay off (wwwroot also holds runtime uploads), so the
// hashed assets from previous builds would pile up. This clears just that one
// directory before each build.
function cleanAssets(dir) {
  return {
    name: 'clean-assets',
    apply: 'build',
    buildStart() {
      rmSync(resolve(import.meta.dirname, dir), { recursive: true, force: true });
    }
  };
}

export default defineConfig({
  plugins: [react(), cleanAssets('../wwwroot/assets')],
  build: {
    outDir: '../wwwroot',
    emptyOutDir: false,
    assetsDir: 'assets',
    sourcemap: false,
    rollupOptions: {
      output: {
        // Rolldown (Vite 8) only accepts the function form of manualChunks.
        // React and Leaflet change far less often than app code, so splitting
        // them out keeps their hashed chunks cached across deploys.
        manualChunks: id => {
          if (!id.includes('node_modules')) return undefined;
          if (/[\\/]node_modules[\\/](react|react-dom|react-router|react-router-dom|scheduler)[\\/]/.test(id)) return 'react';
          if (/[\\/]node_modules[\\/]leaflet[\\/]/.test(id)) return 'leaflet';
          return undefined;
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
