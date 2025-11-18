Help me fix `npm run dev` in Agent.Web\Client folder. It complains that it cannot find rollup msvc dependency.

## Rules
- DO NOT clean any packages
- DO NOT clear any cache
- DO NOT change the package.json or package-lock.json
- Only install the missing module directly and fix
- Use `npm install --no-save` to avoid modifying package files

## Common Issues & Solutions

### 1. Missing Rollup Native Binding
**Error:** `Cannot find module @rollup/rollup-win32-x64-msvc`

**Solution:**
```powershell
cd Q:\src\SreAgentRuntime\src\Agent\Agent.Web\Client
npm list rollup  # Check rollup version (e.g., 4.44.0)
npm install --no-save @rollup/rollup-win32-x64-msvc@4.44.0
```

### 2. Missing SWC Native Binding
**Error:** `Failed to load native binding` from `@swc/core/binding.js`

**Root Cause:** The `@vitejs/plugin-react-swc` plugin has a **nested node_modules** with its own version of `@swc/core` due to version conflicts.

**Check for nesting:**
```powershell
cd Q:\src\SreAgentRuntime\src\Agent\Agent.Web\Client
npm list @swc/core
# Output shows:
# ├── @swc/core@1.11.29              (top-level)
# └─┬ @vitejs/plugin-react-swc@3.10.2
#   └── @swc/core@1.12.6             (nested)
```

**Solution:** Install the native binding in the **nested location** where the plugin's `@swc/core` is located:
```powershell
cd Q:\src\SreAgentRuntime\src\Agent\Agent.Web\Client\node_modules\@vitejs\plugin-react-swc
npm install --no-save @swc/core-win32-x64-msvc@1.12.6
```

### Why Nested node_modules?
npm creates nested `node_modules` when:
- Different packages require different versions of the same dependency
- The top-level project requires `@swc/core@1.11.29`
- But `@vitejs/plugin-react-swc` specifically needs `@swc/core@1.12.6`
- npm installs both: one at top-level, one nested in the plugin's folder

### Verification
After fixes, verify the build works:

First test just the npm build:
```powershell
cd Q:\src\SreAgentRuntime\src\Agent\Agent.Web\Client
npm run dev
```

Then the full .NET build:
```powershell
cd Q:\src\SreAgentRuntime\src\Agent\Agent.Web
dotnet build --no-restore
```


