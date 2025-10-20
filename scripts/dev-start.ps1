param(
  [switch]$Build
)

Push-Location (Join-Path $PSScriptRoot '..' 'containers')
try {
  if ($Build) { docker compose build }
  docker compose up -d
}
finally { Pop-Location }

Write-Host 'Whisper STT: http://127.0.0.1:18000/stt'
Write-Host 'HTTP TTS  : http://127.0.0.1:18010/tts'
