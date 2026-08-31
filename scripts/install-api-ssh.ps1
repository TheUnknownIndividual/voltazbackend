param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath,

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
    throw "Refusing to deploy to an unexpected API target: $ResolvedTarget"
}

if (-not (Test-Path -LiteralPath $PackagePath -PathType Leaf)) {
    throw "API package was not found: $PackagePath"
}

if (-not (Test-Path -LiteralPath $ResolvedTarget -PathType Container)) {
    throw "API target was not found: $ResolvedTarget"
}

$Timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$TargetName = Split-Path -Leaf $ResolvedTarget
$WorkRoot = Join-Path $env:USERPROFILE "api-deployments"
$StagePath = Join-Path $WorkRoot "$TargetName-stage-$Timestamp"
$BackupPath = Join-Path $WorkRoot "$TargetName-backup-$Timestamp"
$OfflinePath = Join-Path $ResolvedTarget "app_offline.htm"
$BackupReady = $false

New-Item -ItemType Directory -Path $WorkRoot -Force | Out-Null
New-Item -ItemType Directory -Path $StagePath -Force | Out-Null

try {
    Write-Host "Extracting API package..." -ForegroundColor Cyan
    Expand-Archive -LiteralPath $PackagePath -DestinationPath $StagePath -Force

    $RequiredFiles = @(
        (Join-Path $StagePath "Volt.API.dll"),
        (Join-Path $StagePath "web.config"),
        (Join-Path $StagePath "appsettings.json")
    )

    foreach ($RequiredFile in $RequiredFiles) {
        if (-not (Test-Path -LiteralPath $RequiredFile -PathType Leaf)) {
            throw "API package validation failed; missing: $RequiredFile"
        }
    }

    [System.IO.File]::WriteAllText(
        $OfflinePath,
        "Volt API is updating. Please retry in a moment.`r`n",
        (New-Object System.Text.UTF8Encoding($false))
    )

    # Give the ASP.NET Core module time to release the running assemblies.
    Start-Sleep -Seconds 5

    Write-Host "Creating API rollback copy..." -ForegroundColor Cyan
    New-Item -ItemType Directory -Path $BackupPath -Force | Out-Null
    & robocopy.exe $ResolvedTarget $BackupPath /E /COPY:DAT /DCOPY:DAT /R:2 /W:1 /XF app_offline.htm /NFL /NDL /NJH /NJS
    $BackupResult = $LASTEXITCODE
    if ($BackupResult -ge 8) {
        throw "API rollback copy failed with robocopy exit code $BackupResult."
    }
    $BackupReady = $true

    Write-Host "Copying API files..." -ForegroundColor Cyan
    & robocopy.exe $StagePath $ResolvedTarget /E /COPY:DAT /DCOPY:DAT /R:3 /W:2 /XF app_offline.htm /NFL /NDL /NJH /NJS
    $DeployResult = $LASTEXITCODE
    if ($DeployResult -ge 8) {
        throw "API copy failed with robocopy exit code $DeployResult."
    }

    Remove-Item -LiteralPath $OfflinePath -Force

    Write-Host "API deployment completed; IIS is starting the application." -ForegroundColor Green
    Write-Host "Target:   $ResolvedTarget"
    Write-Host "Rollback: $BackupPath"

    Remove-Item -LiteralPath $StagePath -Recurse -Force
    Remove-Item -LiteralPath $PackagePath -Force
}
catch {
    Write-Host "API deployment failed." -ForegroundColor Red

    if ($BackupReady -and (Test-Path -LiteralPath $BackupPath)) {
        Write-Host "Restoring overwritten files from the rollback copy..." -ForegroundColor Yellow
        & robocopy.exe $BackupPath $ResolvedTarget /E /COPY:DAT /DCOPY:DAT /R:3 /W:2 /NFL /NDL /NJH /NJS
        $RestoreResult = $LASTEXITCODE
        if ($RestoreResult -ge 8) {
            Write-Host "Automatic restore also failed with robocopy exit code $RestoreResult." -ForegroundColor Red
        }
    }

    Write-Host "Package retained at: $PackagePath" -ForegroundColor Yellow
    Write-Host "Stage retained at:   $StagePath" -ForegroundColor Yellow
    throw
}
finally {
    if (Test-Path -LiteralPath $OfflinePath) {
        Remove-Item -LiteralPath $OfflinePath -Force -ErrorAction SilentlyContinue
    }
}
