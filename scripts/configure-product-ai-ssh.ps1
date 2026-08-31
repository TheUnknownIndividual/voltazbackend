param(
    [Parameter(Mandatory = $true)]
    [string]$SourceConfigPath,

    [Parameter(Mandatory = $true)]
    [string]$TargetPath
)

$ErrorActionPreference = "Stop"

$AllowedTargets = @(
    "C:\inetpub\wwwroot\testapivoltaz",
    "C:\inetpub\wwwroot\apivoltaz"
)
$ResolvedTarget = [System.IO.Path]::GetFullPath($TargetPath).TrimEnd('\')
if ($AllowedTargets -notcontains $ResolvedTarget) {
    throw "Refusing to configure an unexpected API target: $ResolvedTarget"
}
if (-not (Test-Path -LiteralPath $SourceConfigPath -PathType Leaf)) {
    throw "Protected Product AI configuration was not uploaded."
}

$TargetConfig = Join-Path $ResolvedTarget "openai.production.json"
$OfflineFile = Join-Path $ResolvedTarget "app_offline.htm"
$BackupFile = $null
$Utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)

try {
    $RawConfig = Get-Content -LiteralPath $SourceConfigPath -Raw
    $Config = $RawConfig | ConvertFrom-Json
    $ProductAi = $Config.ProductAiImport
    if ($null -eq $ProductAi -or $ProductAi.Enabled -ne $true) {
        throw "ProductAiImport.Enabled must be true."
    }
    if ([string]::IsNullOrWhiteSpace([string]$ProductAi.ApiKey) -or -not ([string]$ProductAi.ApiKey).StartsWith("sk-")) {
        throw "A valid replacement OpenAI project API key is required."
    }
    if ([string]::IsNullOrWhiteSpace([string]$ProductAi.Model)) {
        throw "A Product AI model is required."
    }
    if ([string]$ProductAi.TrustedAssetBaseUrl -ne "https://cloudfiles.volt.az/") {
        throw "The trusted asset base URL must remain https://cloudfiles.volt.az/."
    }
    if ([int]$ProductAi.RequestTimeoutSeconds -lt 30 -or [int]$ProductAi.RequestTimeoutSeconds -gt 600) {
        throw "Product AI request timeout must be between 30 and 600 seconds."
    }
    if ([int]$ProductAi.MaxConcurrentJobs -lt 1 -or [int]$ProductAi.MaxConcurrentJobs -gt 4) {
        throw "Product AI concurrency must be between 1 and 4 jobs."
    }

    try {
        if (Test-Path -LiteralPath $TargetConfig -PathType Leaf) {
            $BackupFile = Join-Path $ResolvedTarget "openai.production.backup.json"
            Copy-Item -LiteralPath $TargetConfig -Destination $BackupFile -Force
        }

        [System.IO.File]::WriteAllText(
            $OfflineFile,
            "Volt API configuration is updating. Please retry shortly.`r`n",
            $Utf8WithoutBom
        )
        Start-Sleep -Seconds 3
        [System.IO.File]::WriteAllText($TargetConfig, $RawConfig, $Utf8WithoutBom)
    }
    catch {
        if ($BackupFile -and (Test-Path -LiteralPath $BackupFile -PathType Leaf)) {
            Copy-Item -LiteralPath $BackupFile -Destination $TargetConfig -Force -ErrorAction SilentlyContinue
        }
        throw
    }
}
finally {
    Remove-Item -LiteralPath $OfflineFile -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $SourceConfigPath -Force -ErrorAction SilentlyContinue
}

Write-Host "Product AI protected configuration installed." -ForegroundColor Green
Write-Host "Target: $TargetConfig"
if ($BackupFile) { Write-Host "Rollback: $BackupFile" }
Write-Host "No API key was printed or logged."
