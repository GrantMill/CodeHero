Param()
Write-Host "Verifying baseline: docs and tests"
if (-not (Test-Path "docs/VISION.md")) { Write-Error "MISSING: docs/VISION.md"; exit 1 }
$len = (Get-Content docs/VISION.md -Raw).Length
if ($len -lt 100) { Write-Error "VISION.md too short ($len chars)"; exit 1 }
if (-not (Test-Path "plan/BACKLOG.md")) { Write-Error "MISSING: plan/BACKLOG.md"; exit 1 }
if (-not (Select-String -Path plan/BACKLOG.md -Pattern '^## Human Tasks' -Quiet)) { Write-Error "Backlog missing '## Human Tasks'"; exit 1 }
if (-not (Select-String -Path plan/BACKLOG.md -Pattern '^## Agent Tasks' -Quiet)) { Write-Error "Backlog missing '## Agent Tasks'"; exit 1 }

Write-Host "Running solution tests..."
dotnet test CodeHero.sln --nologo
if ($LASTEXITCODE -ne 0) { Write-Error "dotnet test failed"; exit $LASTEXITCODE }
Write-Host "Baseline verification succeeded."