import { defineConfig } from 'vite';
import solidPlugin from 'vite-plugin-solid';

export default defineConfig({
  plugins: [solidPlugin()],
  server: {
    port: 3000,
    proxy: {
      '/dashboard': {
        target: 'http://127.0.0.1:5031',
        changeOrigin: true,
        bypass: (req) => {
          if (req.headers.accept?.includes('text/html')) return '/index.html';
        },
      },
      '/apps': {
        target: 'http://127.0.0.1:5031',
        changeOrigin: true,
        bypass: (req) => {
          if (req.headers.accept?.includes('text/html')) return '/index.html';
        },
      },
      '/auth': {
        target: 'http://127.0.0.1:5031',
        changeOrigin: true,
      },
      '/pilot': {
        target: 'http://127.0.0.1:5031',
        changeOrigin: true,
      }
    }
  },
  build: {
    target: 'esnext',
  },
});
