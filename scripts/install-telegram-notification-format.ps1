<#
Installs the focused Telegram notification-format assembly update into the
production API. Run from an elevated PowerShell session on the IIS host.
The existing assembly is retained in a timestamped rollback directory.
#>
[CmdletBinding()]
param(
    [string]$ApiRoot = "C:\inetpub\wwwroot\apivoltaz"
)

$ErrorActionPreference = "Stop"
$sourceAssembly = Join-Path $PSScriptRoot "payload\Volt.Infrastructure.dll"
$targetAssembly = Join-Path $ApiRoot "Volt.Infrastructure.dll"
$offlineFile = Join-Path $ApiRoot "app_offline.htm"
$backupDirectory = Join-Path $ApiRoot ("releases\telegram-format-{0}" -f (Get-Date -Format "yyyyMMdd-HHmmss"))

if (-not (Test-Path -LiteralPath $sourceAssembly -PathType Leaf)) {
    throw "Package assembly was not found: $sourceAssembly"
}
if (-not (Test-Path -LiteralPath $targetAssembly -PathType Leaf)) {
    throw "Production API assembly was not found: $targetAssembly"
}

New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null
Copy-Item -LiteralPath $targetAssembly -Destination (Join-Path $backupDirectory "Volt.Infrastructure.dll") -Force

try {
    Set-Content -LiteralPath $offlineFile -Value "Volt API is updating. Please retry in a moment." -Encoding UTF8
    Start-Sleep -Seconds 3
    Copy-Item -LiteralPath $sourceAssembly -Destination $targetAssembly -Force
}
catch {
    Copy-Item -LiteralPath (Join-Path $backupDirectory "Volt.Infrastructure.dll") -Destination $targetAssembly -Force
    throw
}
finally {
    Remove-Item -LiteralPath $offlineFile -Force -ErrorAction SilentlyContinue
}

Start-Sleep -Seconds 5
Write-Host "Telegram notification format deployed. Rollback assembly: $backupDirectory\Volt.Infrastructure.dll"
