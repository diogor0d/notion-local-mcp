[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $projectRoot 'src\NotionMcpLauncher.cs'
$outputDirectory = Join-Path $projectRoot 'bin'
$outputPath = Join-Path $outputDirectory 'NotionMcpLauncher.exe'

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
if (Test-Path -LiteralPath $outputPath -PathType Leaf) {
    Remove-Item -LiteralPath $outputPath -Force
}

Add-Type `
    -Path $sourcePath `
    -OutputAssembly $outputPath `
    -OutputType ConsoleApplication `
    -ReferencedAssemblies @('System.dll', 'System.Core.dll', 'System.Security.dll')

Write-Host "Built native MCP launcher: $outputPath"

