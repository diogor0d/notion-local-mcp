import { fileURLToPath } from "node:url";
import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StdioClientTransport } from "@modelcontextprotocol/sdk/client/stdio.js";

const launcher = fileURLToPath(
  new URL("../bin/NotionMcpLauncher.exe", import.meta.url),
);
const transport = new StdioClientTransport({
  command: launcher,
});

const client = new Client({
  name: "notion-local-connection-check",
  version: "1.0.0",
});

try {
  await client.connect(transport);
  const result = await client.callTool({
    name: "API-get-self",
    arguments: {},
  });

  if (result.isError) {
    throw new Error("Notion rejected the read-only authentication check.");
  }

  process.stdout.write("NOTION_AUTH_OK\n");
} finally {
  await client.close();
}
