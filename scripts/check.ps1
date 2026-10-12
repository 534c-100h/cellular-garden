$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$checkRoot = Join-Path $projectRoot 'artifacts/check'

& node (Join-Path $projectRoot 'test-engine.cjs')
if ($LASTEXITCODE -ne 0) { throw 'Web engine tests failed.' }

& (Join-Path $projectRoot 'desktop/build.ps1') -OutputDirectory $checkRoot
$testExe = Join-Path $checkRoot 'CellularGarden.exe'
$testProcess = Start-Process -FilePath $testExe -ArgumentList '--self-test' -WindowStyle Hidden -PassThru
if (-not $testProcess.WaitForExit(60000)) {
    $testProcess.Kill()
    throw 'Desktop self-tests timed out after 60 seconds.'
}
$testReport = Join-Path $checkRoot 'test-results.txt'
if (Test-Path -LiteralPath $testReport) { Get-Content -LiteralPath $testReport }
if ($testProcess.ExitCode -ne 0) { throw "Desktop self-tests failed with exit code $($testProcess.ExitCode)." }
Write-Output 'All checks passed.'
