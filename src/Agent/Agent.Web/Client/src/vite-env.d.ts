/// <reference types="vite/client" />

interface ImportMetaEnv {
    readonly BASE_ROUTE: string;
    readonly SRE_UX_VERSION?: string;
}

interface ImportMeta {
    readonly env: ImportMetaEnv;
}
