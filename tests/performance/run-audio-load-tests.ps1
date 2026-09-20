[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$AudioFile,

    [string]$BaseUrl = "http://localhost:5270",

    [ValidateNotNullOrEmpty()]
    [int[]]$Loads = @(5, 10, 20),

    [ValidateRange(0, 300)]
    [int]$CooldownSeconds = 15
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command k6 -ErrorAction SilentlyContinue)) {
    throw "k6 was not found. Install it with: winget install k6 --source winget"
}

if ($Loads | Where-Object { $_ -le 0 }) {
    throw "Every load value must be greater than zero."
}

$resolvedAudioFile = (Resolve-Path -LiteralPath $AudioFile).Path
$scriptPath = Join-Path $PSScriptRoot "audio-upload.js"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$resultsDirectory = Join-Path $repositoryRoot "artifacts\k6"
$failedLoads = [System.Collections.Generic.List[int]]::new()

New-Item -ItemType Directory -Path $resultsDirectory -Force | Out-Null

for ($index = 0; $index -lt $Loads.Count; $index++) {
    $load = $Loads[$index]
    $summaryPath = Join-Path $resultsDirectory "audio-upload-$load-vus.json"

    Write-Host ""
    Write-Host "Running audio upload test with $load concurrent requests..." -ForegroundColor Cyan

    & k6 run `
        --summary-export $summaryPath `
        -e "VUS=$load" `
        -e "AUDIO_FILE=$resolvedAudioFile" `
        -e "BASE_URL=$BaseUrl" `
        $scriptPath

    if ($LASTEXITCODE -ne 0) {
        $failedLoads.Add($load)
        Write-Warning "The $load-request test did not meet its functional thresholds."
    }

    if ($index -lt $Loads.Count - 1 -and $CooldownSeconds -gt 0) {
        Write-Host "Waiting $CooldownSeconds seconds before the next load level..."
        Start-Sleep -Seconds $CooldownSeconds
    }
}

Write-Host ""
Write-Host "JSON summaries were saved in $resultsDirectory" -ForegroundColor Green

if ($failedLoads.Count -gt 0) {
    throw "Some load levels failed: $($failedLoads -join ', ')"
}
