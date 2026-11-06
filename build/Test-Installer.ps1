param([string]$PackageDirectory = (Join-Path $PSScriptRoot '../artifacts/beta'))
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true') { throw 'Installer verification runs only on a disposable GitHub Actions runner.' }
$repoRoot = Split-Path -Parent $PSScriptRoot
$version = [string]([xml](Get-Content -LiteralPath (Join-Path $repoRoot 'Version.props') -Raw)).Project.PropertyGroup.Version
$setup = Join-Path $PackageDirectory "PennCraft-$version-win-x64-setup.exe"
$root = Join-Path $env:RUNNER_TEMP ('PennCraft-install-' + [guid]::NewGuid().ToString('N'))
$install = Join-Path $root 'app'
$results = Join-Path $repoRoot 'artifacts/test-results'
New-Item -ItemType Directory -Path $root, $results -Force | Out-Null
$dataRoot = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'PennCraft'
New-Item -ItemType Directory -Path $dataRoot -Force | Out-Null
$sentinel = Join-Path $dataRoot ('installer-test-' + [guid]::NewGuid().ToString('N') + '.txt')
[IO.File]::WriteAllText($sentinel, 'settings must survive uninstall')
function RunInstaller([string]$exe, [string[]]$arguments) {
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -PassThru -WindowStyle Hidden
    try {
        if (-not $process.WaitForExit(180000)) { $process.Kill(); throw 'Installer verification timed out.' }
        if ($process.ExitCode -ne 0) { throw "Installer exited with $($process.ExitCode)" }
    } finally { $process.Dispose() }
}
try {
    $arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', ('/DIR="' + $install + '"'), ('/LOG="' + (Join-Path $results 'install.log') + '"'))
    RunInstaller $setup $arguments
    & (Join-Path $PSScriptRoot 'Invoke-Smoke.ps1') -ExecutablePath (Join-Path $install 'PennCraft.exe') -ExpectedVersion $version -ReportPath (Join-Path $results 'installed.json')
    RunInstaller $setup $arguments
    if ([IO.File]::ReadAllText($sentinel) -ne 'settings must survive uninstall') { throw 'Update changed user settings.' }
    RunInstaller (Join-Path $install 'unins000.exe') @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', ('/LOG="' + (Join-Path $results 'uninstall.log') + '"'))
    if (Test-Path -LiteralPath (Join-Path $install 'PennCraft.exe')) { throw 'Uninstall did not remove application binaries.' }
    if ([IO.File]::ReadAllText($sentinel) -ne 'settings must survive uninstall') { throw 'Uninstall changed user settings.' }
    Write-Host 'Verified install, installed-app smoke, reinstall and settings-preserving uninstall.'
} finally { if (Test-Path -LiteralPath $sentinel) { Remove-Item -LiteralPath $sentinel } }
