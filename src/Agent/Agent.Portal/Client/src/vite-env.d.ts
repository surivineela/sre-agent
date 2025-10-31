/// <reference types="vite/client" />

interface ImportMetaEnv {
    readonly SRE_AGENT_PORTAL_VERSION?: string;
}

interface ImportMeta {
    readonly env: ImportMetaEnv;
}
