[CmdletBinding()]
param(
    [string]$HostName = "test.api.volt.az",
    [string]$ApiPhysicalPath,
    [string]$SiteName,
    [string]$AppPoolName,
    [string]$CallbackUrl = "https://test.api.volt.az/api/meta-inbox/webhook",
    [string]$GraphApiVersion = "v26.0"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function ConvertFrom-VoltSecureString {
    param([Parameter(Mandatory)][Security.SecureString]$Value)

    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Value)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    }
}

function New-VoltVerifyToken {
    $bytes = New-Object byte[] 32
    $generator = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $generator.GetBytes($bytes) }
    finally { $generator.Dispose() }
    return ([BitConverter]::ToString($bytes) -replace "-", "").ToLowerInvariant()
}

function Resolve-VoltIisTarget {
    Import-Module WebAdministration -ErrorAction Stop

    $sites = @(Get-Website)
    $selectedSite = $null

    if ($SiteName) {
        $selectedSite = $sites | Where-Object { $_.Name -eq $SiteName } | Select-Object -First 1
        if (-not $selectedSite) {
            throw "IIS site '$SiteName' was not found."
        }
    }
    elseif (-not $ApiPhysicalPath) {
        $matchingSites = @($sites | Where-Object {
            $bindings = [string]$_.Bindings.Collection.bindingInformation
            $bindings -match [regex]::Escape($HostName)
        })

        if ($matchingSites.Count -ne 1) {
            Write-Host "IIS sites and bindings:" -ForegroundColor Yellow
            $sites | Select-Object Name, State, PhysicalPath, ApplicationPool, Bindings | Format-List
            throw "Could not uniquely find the IIS site for '$HostName'. Re-run with -SiteName or -ApiPhysicalPath."
        }

        $selectedSite = $matchingSites[0]
    }

    if ($selectedSite) {
        if (-not $ApiPhysicalPath) { $script:ApiPhysicalPath = [Environment]::ExpandEnvironmentVariables([string]$selectedSite.PhysicalPath) }
        if (-not $AppPoolName) { $script:AppPoolName = [string]$selectedSite.ApplicationPool }
        if (-not $SiteName) { $script:SiteName = [string]$selectedSite.Name }
    }
}

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run PowerShell as Administrator."
}

Resolve-VoltIisTarget

if (-not $ApiPhysicalPath) { throw "The API physical path could not be determined." }
$ApiPhysicalPath = [IO.Path]::GetFullPath($ApiPhysicalPath)
if (-not (Test-Path -LiteralPath $ApiPhysicalPath -PathType Container)) {
    throw "API directory does not exist: $ApiPhysicalPath"
}
if (-not (Test-Path -LiteralPath (Join-Path $ApiPhysicalPath "Volt.API.dll") -PathType Leaf)) {
    throw "Volt.API.dll was not found in '$ApiPhysicalPath'. Refusing to write outside the deployed API directory."
}

Write-Host "Target site: $SiteName" -ForegroundColor Cyan
Write-Host "Target app pool: $AppPoolName" -ForegroundColor Cyan
Write-Host "Target API directory: $ApiPhysicalPath" -ForegroundColor Cyan
Write-Host "Callback URL: $CallbackUrl" -ForegroundColor Cyan

$appSecret = ConvertFrom-VoltSecureString (Read-Host "Paste the Meta App Secret" -AsSecureString)
$pageAccessToken = ConvertFrom-VoltSecureString (Read-Host "Paste the Facebook Page Access Token" -AsSecureString)

if ([string]::IsNullOrWhiteSpace($appSecret)) { throw "Meta App Secret cannot be empty." }
if ([string]::IsNullOrWhiteSpace($pageAccessToken)) { throw "Page Access Token cannot be empty." }

$verifyToken = New-VoltVerifyToken
$configuration = [ordered]@{
    MetaInbox = [ordered]@{
        Enabled = $true
        AppSecret = $appSecret
        VerifyToken = $verifyToken
        PageAccessToken = $pageAccessToken
        GraphApiVersion = $GraphApiVersion
    }
}

$targetFile = Join-Path $ApiPhysicalPath "meta-inbox.production.json"
$temporaryFile = Join-Path $ApiPhysicalPath "meta-inbox.production.json.new"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupFile = "$targetFile.$timestamp.bak"

if (Test-Path -LiteralPath $targetFile) {
    Copy-Item -LiteralPath $targetFile -Destination $backupFile -Force
    Write-Host "Existing configuration backed up to: $backupFile" -ForegroundColor Yellow
}

$json = $configuration | ConvertTo-Json -Depth 4
$utf8WithoutBom = New-Object Text.UTF8Encoding($false)
[IO.File]::WriteAllText($temporaryFile, $json, $utf8WithoutBom)

try {
    $parsed = Get-Content -LiteralPath $temporaryFile -Raw | ConvertFrom-Json
    if (-not $parsed.MetaInbox.Enabled -or [string]::IsNullOrWhiteSpace([string]$parsed.MetaInbox.VerifyToken)) {
        throw "Generated configuration did not pass validation."
    }

    Move-Item -LiteralPath $temporaryFile -Destination $targetFile -Force

    if ($AppPoolName) {
        $appPoolIdentity = "IIS AppPool\$AppPoolName"
        & icacls.exe $targetFile /inheritance:r /grant:r "Administrators:(F)" "SYSTEM:(F)" "${appPoolIdentity}:(R)" | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "Could not set the configuration file permissions." }
        Restart-WebAppPool -Name $AppPoolName
        Write-Host "Restarted IIS app pool: $AppPoolName" -ForegroundColor Green
    }
    elseif ($SiteName) {
        Restart-Website -Name $SiteName
        Write-Host "Restarted IIS site: $SiteName" -ForegroundColor Green
    }
    else {
        Write-Warning "No IIS app pool or site was supplied. Restart the test API manually before testing."
    }

    $separator = if ($CallbackUrl.Contains("?")) { "&" } else { "?" }
    $verificationUrl = "$CallbackUrl${separator}hub.mode=subscribe&hub.verify_token=$([Uri]::EscapeDataString($verifyToken))&hub.challenge=volt-meta-webhook-ok"
    $response = Invoke-WebRequest -Uri $verificationUrl -Method Get -UseBasicParsing -TimeoutSec 20
    if ([string]$response.Content -ne "volt-meta-webhook-ok") {
        throw "Callback verification returned an unexpected response: $($response.Content)"
    }

    Write-Host ""
    Write-Host "Meta webhook configuration is ready." -ForegroundColor Green
    Write-Host "Callback URL: $CallbackUrl" -ForegroundColor Green
    Write-Host "Verify token: $verifyToken" -ForegroundColor Green
    Write-Host "Paste those two values into the Meta App Dashboard, then subscribe to the messages field." -ForegroundColor Green
}
finally {
    if (Test-Path -LiteralPath $temporaryFile) {
        Remove-Item -LiteralPath $temporaryFile -Force
    }
    $appSecret = $null
    $pageAccessToken = $null
}
