param(
    [Parameter(Mandatory)][string]$AssetDirectory,
    [Parameter(Mandatory)][string]$Tag,
    [Parameter(Mandatory)][string]$SourceCommit
)
$ErrorActionPreference = 'Stop'
if ($Tag -notmatch '^v[0-9]+[.][0-9]+[.][0-9]+-(beta|rc)[.][0-9]+$' -or $SourceCommit -notmatch '^[0-9a-f]{40}$') { throw 'Invalid release identity.' }
$version = $Tag.Substring(1)
$expected = @("PennCraft-$version-win-x64-portable.zip", "PennCraft-$version-win-x64-setup.exe", 'SHA256SUMS.txt', 'BUILDINFO.json')
$assets = @(foreach ($name in $expected) {
    $target = Join-Path $AssetDirectory $name
    if (-not (Test-Path -LiteralPath $target -PathType Leaf)) { throw "Missing release asset: $name" }
    $target
})
$info = Get-Content -LiteralPath (Join-Path $AssetDirectory 'BUILDINFO.json') -Raw | ConvertFrom-Json
if ($info.Version -ne $version -or $info.SourceCommit -ne $SourceCommit -or -not $info.SelfContained -or $info.Signed) { throw 'Build identity does not match the release.' }
foreach ($line in Get-Content -LiteralPath (Join-Path $AssetDirectory 'SHA256SUMS.txt')) {
    if ($line -notmatch '^([0-9a-f]{64})  ([A-Za-z0-9._-]+)$') { throw 'Invalid checksum manifest.' }
    $name = $Matches[2]; $expectedHash = $Matches[1]
    if ($name -notin $expected) { throw 'Unexpected checksum entry.' }
    if ((Get-FileHash -LiteralPath (Join-Path $AssetDirectory $name) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expectedHash) { throw "Checksum mismatch: $name" }
}
$existingTag = & git ls-remote --tags origin ("refs/tags/" + $Tag)
if ($LASTEXITCODE -ne 0 -or $existingTag) { throw 'The tag must be new; existing tags are never modified.' }
$notes = Join-Path $PSScriptRoot "../docs/releases/$Tag.md"
& gh release create $Tag @assets --repo $env:GITHUB_REPOSITORY --target $SourceCommit --title "PennCraft $version" --notes-file $notes --draft --prerelease --latest=false
if ($LASTEXITCODE -ne 0) { throw 'Release creation failed. Existing releases are never overwritten; inspect any draft before retrying.' }
$verified = $false
for ($attempt = 0; $attempt -lt 10; $attempt++) {
    $response = & gh api "repos/$env:GITHUB_REPOSITORY/releases"
    if ($LASTEXITCODE -ne 0) { throw 'Could not verify the draft release.' }
    $release = @($response | ConvertFrom-Json) | Where-Object { $_.tag_name -eq $Tag } | Select-Object -First 1
    if ($release -and $release.draft -and $release.prerelease -and $release.assets.Count -eq $expected.Count) {
        $verified = $true
        foreach ($asset in $assets) {
            $name = [IO.Path]::GetFileName($asset)
            $remote = @($release.assets | Where-Object { $_.name -eq $name })
            $hash = (Get-FileHash -LiteralPath $asset -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($remote.Count -ne 1 -or $remote[0].digest -ne "sha256:$hash" -or $remote[0].size -ne (Get-Item -LiteralPath $asset).Length) { $verified = $false }
        }
        if ($verified) { break }
    }
    Start-Sleep -Seconds 2
}
if (-not $verified) { throw 'Draft asset verification failed. The release was left unpublished.' }
& gh release edit $Tag --repo $env:GITHUB_REPOSITORY --draft=false --prerelease --latest=false
if ($LASTEXITCODE -ne 0) { throw 'Could not publish the verified draft.' }
$published = & gh api "repos/$env:GITHUB_REPOSITORY/releases/tags/$Tag" | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or $published.draft -or -not $published.prerelease) { throw 'Publication confirmation failed.' }
$tagRef = & gh api "repos/$env:GITHUB_REPOSITORY/git/ref/tags/$Tag" | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or $tagRef.object.sha -ne $SourceCommit) { throw 'Published tag identity does not match the verified source.' }
"Published [$Tag]($($published.html_url)) from $SourceCommit with verified SHA-256 asset digests." | Add-Content $env:GITHUB_STEP_SUMMARY
Write-Host $published.html_url
