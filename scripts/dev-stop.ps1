Push-Location (Join-Path $PSScriptRoot '..' 'containers')
try { docker compose down } finally { Pop-Location }
