# Notion MCP, kept local

Connect **Codex ↔ Notion** through Notion's STDIO MCP server, without putting an integration token in a repository or in Codex configuration.

```text
Codex ── STDIO ──► Windows launcher ── STDIO ──► Notion MCP server ── HTTPS ──► Notion
                       │
                       └── decrypts a per-user DPAPI token at startup
```

This is a small, Windows-specific launcher and setup for people who prefer a Notion internal integration over account linking. The launcher starts the pinned upstream server locally; Notion content and tool calls still travel to Notion over the network.

## What it provides

- **No token in `config.toml`:** a hidden PowerShell prompt saves the token under `%LOCALAPPDATA%\Codex\NotionMcp\notion-token.dpapi`, encrypted for the current Windows user.
- **Direct STDIO connection:** the native launcher passes Codex's standard streams to the Node server and exits with its status.
- **Scoped access:** the Notion integration sees only the pages and databases you grant it; the example Codex config exposes a selected tool set.
- **Read-only connection check:** `pnpm verify-connection` calls `API-get-self` and prints `NOTION_AUTH_OK` on success.

## Requirements

- Windows and PowerShell
- Node.js and pnpm
- Codex with local MCP server configuration
- A Notion **internal integration** and access to the pages or databases you want to use

## Set up

1. Install the pinned dependencies from the lockfile:

   ```powershell
   pnpm install --frozen-lockfile
   ```

2. Create an [internal integration in Notion](https://www.notion.so/profile/integrations). Start with **Read content**. In its **Access** tab, grant only the pages or databases you need. Add **Insert content** and **Update content** later if you want Codex to edit them.

3. Build the Windows launcher, then store the token through the hidden prompt:

   ```powershell
   .\scripts\build-launcher.ps1
   .\scripts\set-token.ps1
   ```

4. Copy the section from [`config/notion-local.toml.example`](config/notion-local.toml.example) into `%USERPROFILE%\.codex\config.toml`. Replace `<ABSOLUTE_PATH>` with this repository's absolute Windows path. In the example's double-quoted TOML string, keep each path separator doubled (`\\`). The resulting `command` must point to `bin\NotionMcpLauncher.exe`.

5. Restart Codex and check `/mcp` for `notion_local`. Then run:

   ```powershell
   pnpm verify-connection
   ```

   The check calls only Notion's `get-self` tool. It does not print the token or workspace content.

If the launcher cannot find Node, set `NOTION_MCP_NODE` to the absolute path of `node.exe` in the environment that starts Codex, then restart Codex.

## Security boundaries

The token stays outside this repository and is decrypted in the launcher process when it starts the MCP server. **DPAPI protects the saved token at rest for this Windows user; it does not make a compromised user session safe.** The Node process receives the token in its environment so it can authenticate with Notion.

The example config asks for approval on tools marked as writes and exposes a selected set of tools. Review that list before enabling edit permissions. Notion may mark `API-post-search` and `API-query-data-source` as destructive despite their read behavior, so Codex may prompt for those calls too. Explicit block deletion, page moves, user-directory access, and data-source schema changes are absent from the example list.

Notion responses become Codex task context. Avoid granting the integration access to content you do not want Codex to process.

## Maintenance

- **Rotate the token:** run `.\scripts\set-token.ps1` again and restart Codex.
- **Inspect upstream tools:** run `pnpm inspect-tools` to list tool names and their read/destructive annotations. It uses a discovery-only placeholder token and does not call a Notion workspace.
- **Upgrade deliberately:** `@notionhq/notion-mcp-server` is pinned in `package.json` and `pnpm-lock.yaml`. Reinspect tools and revisit the Codex allowlist after an upgrade.

This repository is an example setup for a local Windows user, not a hosted Notion service.
