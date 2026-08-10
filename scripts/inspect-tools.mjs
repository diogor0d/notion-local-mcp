import { fileURLToPath } from "node:url";
import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StdioClientTransport } from "@modelcontextprotocol/sdk/client/stdio.js";

const serverCli = fileURLToPath(
  new URL("../node_modules/@notionhq/notion-mcp-server/bin/cli.mjs", import.meta.url),
);

const transport = new StdioClientTransport({
  command: process.execPath,
  args: [serverCli, "--transport", "stdio"],
  env: {
    ...process.env,
    NOTION_TOKEN: "ntn_tool_discovery_only",
  },
});

const client = new Client({
  name: "notion-local-tool-inspector",
  version: "1.0.0",
});

try {
  await client.connect(transport);
  const { tools } = await client.listTools();
  const summary = tools.map(({ name, annotations }) => ({
    name,
    readOnlyHint: annotations?.readOnlyHint ?? null,
    destructiveHint: annotations?.destructiveHint ?? null,
  }));
  process.stdout.write(`${JSON.stringify(summary, null, 2)}\n`);
} finally {
  await client.close();
}

