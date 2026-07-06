import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  // Base public path. Defaults to '/' (served by the API at its root, or a root/custom-domain site).
  // The GitHub Pages workflow sets VITE_BASE='/<repo>/' for a project-site subpath. loadEnv reads it
  // (from the shell or a .env file) without needing @types/node in this config.
  const env = loadEnv(mode, '.', 'VITE_');
  return {
    base: env.VITE_BASE ?? '/',
    plugins: [react()],
    server: { port: 5199, open: true },
  };
});
