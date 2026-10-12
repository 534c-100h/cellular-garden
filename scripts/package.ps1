param([ValidatePattern('^\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?$')][string]$Version = '0.1.0')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $projectRoot 'artifacts'

& (Join-Path $PSScriptRoot 'check.ps1')

$desktopZip = Join-Path $artifactRoot "cellular-garden-windows-x64-$Version.zip"
$webZip = Join-Path $artifactRoot "cellular-garden-web-$Version.zip"
$desktopFiles = @(
    (Join-Path $artifactRoot 'check/CellularGarden.exe'),
    (Join-Path $projectRoot 'desktop/README.md')
)
Compress-Archive -LiteralPath $desktopFiles -DestinationPath $desktopZip -Force
$webFiles = @('index.html', 'style.css', 'engine.js', 'app.js') | ForEach-Object { Join-Path $projectRoot "dist/$_" }
Compress-Archive -LiteralPath $webFiles -DestinationPath $webZip -Force

$checksumPath = Join-Path $artifactRoot "SHA256SUMS-$Version.txt"
$checksumLines = @($desktopZip, $webZip) | ForEach-Object {
    $digest = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()
    "$digest  $([IO.Path]::GetFileName($_))"
}
[IO.File]::WriteAllLines($checksumPath, $checksumLines, [Text.UTF8Encoding]::new($false))
Write-Output "Packages: $desktopZip, $webZip"
Write-Output "Checksums: $checksumPath"
