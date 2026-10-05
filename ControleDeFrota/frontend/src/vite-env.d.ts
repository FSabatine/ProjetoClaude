/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Map tile URL template (default: OpenStreetMap). */
  readonly VITE_MAP_TILE_URL?: string;
  readonly VITE_MAP_TILE_ATTRIBUTION?: string;
}
