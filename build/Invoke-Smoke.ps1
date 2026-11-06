param(
    [Parameter(Mandatory)][string]$ExecutablePath,
    [Parameter(Mandatory)][string]$ExpectedVersion,
    [Parameter(Mandatory)][string]$ReportPath
)
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $ReportPath) { throw "Smoke report already exists: $ReportPath" }
$process = Start-Process -FilePath $ExecutablePath -ArgumentList @('--smoke-test', ('"' + $ReportPath + '"')) -PassThru -WindowStyle Hidden
try {
    if (-not $process.WaitForExit(120000)) { $process.Kill(); throw 'The isolated release smoke test timed out.' }
    if (-not (Test-Path -LiteralPath $ReportPath)) { throw "Smoke report missing. Exit code: $($process.ExitCode)" }
    $report = Get-Content -LiteralPath $ReportPath -Raw | ConvertFrom-Json
    if ($process.ExitCode -ne 0 -or -not $report.Success -or $report.Version -ne $ExpectedVersion -or -not $report.SelfContained) {
        throw "Published app verification failed: $(Get-Content -LiteralPath $ReportPath -Raw)"
    }
    Write-Host "Verified PennCraft $ExpectedVersion with its bundled runtime."
} finally { $process.Dispose() }
