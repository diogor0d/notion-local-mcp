# Local Notion MCP for Codex

This setup connects Codex to Notion through the official local STDIO MCP server and a private Notion internal-integration token. It does **not** use Notion OAuth or link the Notion account to the ChatGPT account.

## Security model

- The Notion token is never stored in this project or in Codex `config.toml`.
- `scripts/set-token.ps1` encrypts it with Windows DPAPI for the current Windows user.
- A minimal native launcher decrypts it in memory and gives the Node MCP process Codex's STDIO handles directly.
- Notion controls which pages the integration can access.
- Codex prompts before tools marked as writes. Explicit block deletion, page moves, user-directory access, and data-source schema creation or updates are not exposed.
- Content returned by Notion becomes Codex task context; this is not an offline integration.

## Finish the setup

1. Open [Notion integrations](https://www.notion.so/profile/integrations) and create an **internal** integration.
2. Start with **Read content** only. After read access is verified, enable **Insert content** and **Update content** when you are ready for edits.
3. In the integration's **Access** tab, grant access only to the pages or databases Codex should work with.
4. Run `scripts/build-launcher.ps1` once to build the local native launcher.
5. Run `scripts/set-token.ps1` in PowerShell and paste the integration token into the hidden prompt. Do not paste the token into Codex or save it in a repository.
6. Restart Codex. Use `/mcp` to confirm that `notion_local` is connected.

Notion currently marks search and data-source queries as destructive even though they are read operations, so Codex may also ask for approval for those two operations.

## Token rotation

Run `scripts/set-token.ps1` again. The encrypted file is replaced for the current Windows user.

## Local verification

Run `pnpm verify-connection`. It starts the same DPAPI-backed launcher used by Codex and calls only Notion's read-only `get-self` endpoint. A successful check prints `NOTION_AUTH_OK` without displaying the token or workspace content.

## Package policy

The official `@notionhq/notion-mcp-server` package is pinned in `package.json` and `pnpm-lock.yaml`. Upgrade it deliberately and inspect its tool list before changing the Codex allow/deny policy.
