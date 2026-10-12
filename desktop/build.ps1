param(
    [string]$OutputName = 'CellularGarden.exe',
    [string]$OutputDirectory = $PSScriptRoot
)
$ErrorActionPreference = 'Stop'
$sourceRoot = $PSScriptRoot
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) { throw 'The Windows .NET Framework 4.x C# compiler was not found.' }
$resolvedOutput = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($resolvedOutput) | Out-Null
$outputPath = Join-Path $resolvedOutput $OutputName
$sourceFiles = Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' | ForEach-Object { $_.FullName }
& $compilerPath /nologo /target:winexe /platform:x64 /optimize+ /debug:pdbonly /codepage:65001 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll /reference:System.Runtime.Serialization.dll "/out:$outputPath" $sourceFiles
if ($LASTEXITCODE -ne 0) { throw 'Desktop build failed.' }
Write-Output "Built: $outputPath"
