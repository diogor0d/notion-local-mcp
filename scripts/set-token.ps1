[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $env:LOCALAPPDATA) {
    throw 'LOCALAPPDATA is not available for the current Windows user.'
}

$secretDirectory = Join-Path $env:LOCALAPPDATA 'Codex\NotionMcp'
$secretPath = Join-Path $secretDirectory 'notion-token.dpapi'

New-Item -ItemType Directory -Path $secretDirectory -Force | Out-Null

$secureToken = Read-Host -Prompt 'Paste the Notion internal integration token' -AsSecureString
$tokenPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureToken)

try {
    $plainToken = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($tokenPointer)
    if ([string]::IsNullOrWhiteSpace($plainToken) -or $plainToken -notmatch '^(ntn_|secret_)') {
        throw 'The value does not look like a Notion internal integration token.'
    }
}
finally {
    if ($tokenPointer -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($tokenPointer)
    }
    Remove-Variable plainToken -ErrorAction SilentlyContinue
}

$encryptedToken = ConvertFrom-SecureString -SecureString $secureToken
Set-Content -LiteralPath $secretPath -Value $encryptedToken -Encoding UTF8 -NoNewline

Write-Host "Encrypted the Notion token for this Windows user at: $secretPath"
Write-Host 'Restart Codex after the MCP configuration has been installed.'

