/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Set to 'true' by the static demo build (GitHub Pages): skip the /v1 engine probe. */
  readonly VITE_DEMO?: string;
  /** Base public path for the build (Vite `base`); e.g. '/tmforge/' for a project site. */
  readonly VITE_BASE?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
