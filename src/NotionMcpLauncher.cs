using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

internal static class NotionMcpLauncher
{
    private const int StdInputHandle = -10;
    private const int StdOutputHandle = -11;
    private const int StdErrorHandle = -12;
    private const uint StartfUseStdHandles = 0x00000100;
    private const uint HandleFlagInherit = 0x00000001;
    private const uint CreateNoWindow = 0x08000000;
    private const uint Infinite = 0xFFFFFFFF;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public uint dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public uint dwProcessId;
        public uint dwThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int standardHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetHandleInformation(IntPtr handle, uint mask, uint flags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcess(
        string applicationName,
        StringBuilder commandLine,
        IntPtr processAttributes,
        IntPtr threadAttributes,
        bool inheritHandles,
        uint creationFlags,
        IntPtr environment,
        string currentDirectory,
        ref StartupInfo startupInfo,
        out ProcessInformation processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetExitCodeProcess(IntPtr process, out uint exitCode);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    public static int Main()
    {
        byte[] encryptedBytes = null;
        byte[] plainBytes = null;

        try
        {
            string secretPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Codex",
                "NotionMcp",
                "notion-token.dpapi");

            if (!File.Exists(secretPath))
            {
                throw new FileNotFoundException(
                    "Notion token is not configured. Run scripts\\set-token.ps1 first.",
                    secretPath);
            }

            encryptedBytes = DecodeHex(File.ReadAllText(secretPath).Trim());
            plainBytes = ProtectedData.Unprotect(
                encryptedBytes,
                null,
                DataProtectionScope.CurrentUser);
            string notionToken = Encoding.Unicode.GetString(plainBytes);

            if (!notionToken.StartsWith("ntn_", StringComparison.Ordinal) &&
                !notionToken.StartsWith("secret_", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The decrypted value is not a Notion integration token.");
            }

            string projectRoot = Directory.GetParent(
                AppDomain.CurrentDomain.BaseDirectory.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar)).FullName;
            string serverCli = Path.Combine(
                projectRoot,
                "node_modules",
                "@notionhq",
                "notion-mcp-server",
                "bin",
                "cli.mjs");

            if (!File.Exists(serverCli))
            {
                throw new FileNotFoundException(
                    "Notion MCP dependencies are missing. Run pnpm install.",
                    serverCli);
            }

            string nodePath = FindNodeExecutable();
            Environment.SetEnvironmentVariable(
                "NOTION_TOKEN",
                notionToken,
                EnvironmentVariableTarget.Process);

            try
            {
                return StartServer(nodePath, serverCli, projectRoot);
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    "NOTION_TOKEN",
                    null,
                    EnvironmentVariableTarget.Process);
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("Notion MCP launcher failed: " + exception.Message);
            return 1;
        }
        finally
        {
            if (encryptedBytes != null)
            {
                Array.Clear(encryptedBytes, 0, encryptedBytes.Length);
            }
            if (plainBytes != null)
            {
                Array.Clear(plainBytes, 0, plainBytes.Length);
            }
        }
    }

    private static int StartServer(string nodePath, string serverCli, string workingDirectory)
    {
        StartupInfo startupInfo = new StartupInfo();
        startupInfo.cb = Marshal.SizeOf(typeof(StartupInfo));
        startupInfo.dwFlags = StartfUseStdHandles;
        startupInfo.hStdInput = GetStdHandle(StdInputHandle);
        startupInfo.hStdOutput = GetStdHandle(StdOutputHandle);
        startupInfo.hStdError = GetStdHandle(StdErrorHandle);

        MarkInheritable(startupInfo.hStdInput);
        MarkInheritable(startupInfo.hStdOutput);
        MarkInheritable(startupInfo.hStdError);

        StringBuilder commandLine = new StringBuilder();
        commandLine.Append(Quote(nodePath));
        commandLine.Append(' ');
        commandLine.Append(Quote(serverCli));
        commandLine.Append(" --transport stdio");

        ProcessInformation processInformation;
        bool started = CreateProcess(
            nodePath,
            commandLine,
            IntPtr.Zero,
            IntPtr.Zero,
            true,
            CreateNoWindow,
            IntPtr.Zero,
            workingDirectory,
            ref startupInfo,
            out processInformation);

        if (!started)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Could not start the Notion MCP process.");
        }

        try
        {
            CloseHandle(processInformation.hThread);
            WaitForSingleObject(processInformation.hProcess, Infinite);

            uint exitCode;
            if (!GetExitCodeProcess(processInformation.hProcess, out exitCode))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Could not read the Notion MCP exit code.");
            }
            return unchecked((int)exitCode);
        }
        finally
        {
            CloseHandle(processInformation.hProcess);
        }
    }

    private static void MarkInheritable(IntPtr handle)
    {
        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
        {
            throw new InvalidOperationException(
                "A required MCP standard stream is unavailable.");
        }
        if (!SetHandleInformation(handle, HandleFlagInherit, HandleFlagInherit))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Could not inherit an MCP standard stream.");
        }
    }

    private static string FindNodeExecutable()
    {
        string configured = Environment.GetEnvironmentVariable("NOTION_MCP_NODE");
        if (!String.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return configured;
        }

        string bundled = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cache",
            "codex-runtimes",
            "codex-primary-runtime",
            "dependencies",
            "node",
            "bin",
            "node.exe");
        if (File.Exists(bundled))
        {
            return bundled;
        }

        string pathValue = Environment.GetEnvironmentVariable("PATH") ?? String.Empty;
        foreach (string directory in pathValue.Split(Path.PathSeparator))
        {
            if (String.IsNullOrWhiteSpace(directory))
            {
                continue;
            }
            string candidate = Path.Combine(directory.Trim(), "node.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(
            "Node.js was not found. Set NOTION_MCP_NODE to node.exe.");
    }

    private static byte[] DecodeHex(string value)
    {
        if (String.IsNullOrWhiteSpace(value) || value.Length % 2 != 0)
        {
            throw new InvalidDataException("The DPAPI token file is malformed.");
        }

        byte[] bytes = new byte[value.Length / 2];
        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = Convert.ToByte(value.Substring(index * 2, 2), 16);
        }
        return bytes;
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}

