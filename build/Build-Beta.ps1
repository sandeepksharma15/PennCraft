param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../artifacts/beta'))
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$versionXml = [xml](Get-Content -LiteralPath (Join-Path $repoRoot 'Version.props') -Raw)
$version = [string]$versionXml.Project.PropertyGroup.Version
$fileVersion = [string]$versionXml.Project.PropertyGroup.FileVersion
if ($version -notmatch '^[0-9]+[.][0-9]+[.][0-9]+-(beta|rc)[.][0-9]+$') { throw 'A beta/rc version is required.' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$baseName = "PennCraft-$version-win-x64"
$zip = Join-Path $OutputDirectory "$baseName-portable.zip"
$setup = Join-Path $OutputDirectory "$baseName-setup.exe"
foreach ($target in @($zip, $setup, (Join-Path $OutputDirectory 'SHA256SUMS.txt'), (Join-Path $OutputDirectory 'BUILDINFO.json'))) {
    if (Test-Path -LiteralPath $target) { throw "Use an empty output directory; refusing to overwrite $target" }
}
$stageRoot = Join-Path $OutputDirectory ('stage-' + [guid]::NewGuid().ToString('N'))
$publish = Join-Path $stageRoot $baseName
New-Item -ItemType Directory -Path $publish -Force | Out-Null
& dotnet publish (Join-Path $repoRoot 'src/PennCraft.Desktop/PennCraft.Desktop.csproj') -c Release -r win-x64 --self-contained true -o $publish -p:PublishSingleFile=false -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw 'Self-contained publish failed.' }
foreach ($required in @('PennCraft.exe', 'PennCraft.dll', 'PennCraft.runtimeconfig.json', 'coreclr.dll', 'hostfxr.dll', 'PresentationFramework.dll', 'System.Windows.Forms.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $publish $required))) { throw "Release file missing: $required" }
}
Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination (Join-Path $publish 'LICENSE-PennCraft.txt')
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/beta-guide.md') -Destination (Join-Path $publish 'READ-ME-FIRST.md')
Copy-Item -LiteralPath (Join-Path $repoRoot ("docs/releases/v" + $version + ".md")) -Destination (Join-Path $publish 'RELEASE-NOTES.md')
$runtime = Get-Content -LiteralPath (Join-Path $publish 'PennCraft.runtimeconfig.json') -Raw | ConvertFrom-Json
if (-not $runtime.runtimeOptions.includedFrameworks) { throw 'Runtime config is not self-contained.' }
$legal = Join-Path $publish 'licenses'
New-Item -ItemType Directory -Path $legal -Force | Out-Null
$nuget = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages' }
foreach ($framework in $runtime.runtimeOptions.includedFrameworks) {
    $pack = $framework.name.ToLowerInvariant() + '.runtime.win-x64'
    $packRoot = Join-Path (Join-Path $nuget $pack) $framework.version
    $notices = @(Get-ChildItem -LiteralPath $packRoot -File -Recurse | Where-Object { $_.Name -match '^(LICENSE|THIRD.PARTY.NOTICES)' })
    if ($notices.Count -eq 0) { throw "Runtime license/notice files not found in $packRoot" }
    foreach ($notice in $notices) { Copy-Item -LiteralPath $notice.FullName -Destination (Join-Path $legal ($pack + '-' + $notice.Name)) -Force }
}
$commit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'A source commit is required for release traceability.' }
$info = @{
    Version = $version; SourceCommit = $commit; RuntimeIdentifier = 'win-x64'; SelfContained = $true; Signed = $false
    Frameworks = $runtime.runtimeOptions.includedFrameworks; Sdk = (& dotnet --version).Trim()
}
$info | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'BUILDINFO.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $OutputDirectory 'BUILDINFO.json') -Destination (Join-Path $publish 'BUILDINFO.json')
$smokeDir = Join-Path $repoRoot 'artifacts/test-results'
New-Item -ItemType Directory -Path $smokeDir -Force | Out-Null
& (Join-Path $PSScriptRoot 'Invoke-Smoke.ps1') -ExecutablePath (Join-Path $publish 'PennCraft.exe') -ExpectedVersion $version -ReportPath (Join-Path $smokeDir 'published.json')
$iscc = Join-Path ([Environment]::GetEnvironmentVariable('ProgramFiles(x86)')) 'Inno Setup 6/ISCC.exe'
if (-not (Test-Path -LiteralPath $iscc)) { throw 'Install Inno Setup 6 before building the installer.' }
& $iscc "/DAppVersion=$version" "/DFileVersion=$fileVersion" "/DPackageSource=$publish" "/DPackageOutput=$OutputDirectory" (Join-Path $PSScriptRoot 'PennCraft.iss')
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $setup)) { throw 'Installer compilation failed.' }
Compress-Archive -LiteralPath $publish -DestinationPath $zip -CompressionLevel Optimal
$extract = Join-Path $stageRoot 'portable-check'
Expand-Archive -LiteralPath $zip -DestinationPath $extract
& (Join-Path $PSScriptRoot 'Invoke-Smoke.ps1') -ExecutablePath (Join-Path (Join-Path $extract $baseName) 'PennCraft.exe') -ExpectedVersion $version -ReportPath (Join-Path $smokeDir 'portable.json')
$hashes = foreach ($asset in @($zip, $setup, (Join-Path $OutputDirectory 'BUILDINFO.json'))) {
    $hash = (Get-FileHash -LiteralPath $asset -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([IO.Path]::GetFileName($asset))"
}
$hashes | Set-Content -LiteralPath (Join-Path $OutputDirectory 'SHA256SUMS.txt') -Encoding ascii
Write-Host "Built and smoke-tested portable ZIP and installer for $version."
