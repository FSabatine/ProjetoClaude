/// <reference types="vitest" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // Same origin in development: the refresh cookie (SameSite=Strict, Path=/api/v1/auth) just works.
    proxy: { '/api': 'http://localhost:5080' },
  },
  // Pre-bundle every dependency at startup. Pages are lazy-loaded, so without this Vite discovers
  // libraries only when a page is opened and re-optimizes mid-session — the browser can then mix
  // two React copies and render a blank screen.
  optimizeDeps: {
    include: [
      'react',
      'react-dom/client',
      'react/jsx-dev-runtime',
      'react-router-dom',
      '@mantine/core',
      '@mantine/hooks',
      '@mantine/form',
      '@mantine/notifications',
      '@mantine/modals',
      '@mantine/dates',
      '@tabler/icons-react',
      '@tanstack/react-query',
      'axios',
      'dayjs',
      'dayjs/locale/pt-br',
      'react-imask',
    ],
  },
  build: {
    rollupOptions: {
      output: {
        manualChunks: {
          react: ['react', 'react-dom', 'react-router-dom'],
          mantine: ['@mantine/core', '@mantine/hooks', '@mantine/form', '@mantine/notifications', '@mantine/modals'],
          dates: ['@mantine/dates', 'dayjs'],
        },
      },
    },
  },
  test: {
    environment: 'node',
    include: ['src/**/*.test.ts'],
  },
});
