# Inspect downloaded CI artifact contents and summarize key files
$trxPath = "artifacts\run-19432867654\TestResults\test-results.trx"
if (Test-Path $trxPath) {
    [xml]$trx = Get-Content $trxPath
    $c = $trx.TestRun.ResultSummary.Counters
    $obj = [pscustomobject]@{
        Total = $c.total
        Executed = $c.executed
        Passed = $c.passed
        Failed = $c.failed
        Inconclusive = $c.inconclusive
        NotExecuted = $c.notExecuted
    }
    Write-Output "--- TRX Summary (JSON) ---"
    $obj | ConvertTo-Json
} else {
    Write-Output "TRX not found: $trxPath"
}

$cov = Get-ChildItem -Path "artifacts\run-19432867654\TestResults" -Recurse -Filter "coverage.cobertura.xml" | Select-Object -First 1
if ($cov) {
    [xml]$cx = Get-Content $cov.FullName
    $root = $cx.coverage
    $lineRate = [double]$root.'@line-rate'
    $linesCovered = [int]$root.'@lines-covered'
    $linesValid = [int]$root.'@lines-valid'
    $overall = [math]::Round($lineRate * 100, 2)
    Write-Output "--- Coverage Summary ---"
    Write-Output ("Overall coverage: {0}% ({1}/{2} lines)" -f $overall, $linesCovered, $linesValid)
    $pkgs = @()
    foreach ($p in $root.packages.package) {
        $pkg = [pscustomobject]@{
            package = $p.'@name'
            pct = [math]::Round(([double]$p.'@line-rate') * 100, 2)
            covered = [int]$p.'@lines-covered'
            valid = [int]$p.'@lines-valid'
        }
        $pkgs += $pkg
    }
    Write-Output "--- Per-package coverage (JSON) ---"
    $pkgs | ConvertTo-Json -Depth 4
} else {
    Write-Output "Coverage not found in artifact"
}
