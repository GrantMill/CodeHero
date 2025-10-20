param(
  [string]$TtsUrl = "http://127.0.0.1:18010/tts",
  [string]$SttUrl = "http://127.0.0.1:18000/stt"
)

Write-Host "Warming TTS ($TtsUrl) and Whisper STT ($SttUrl)"

# 1) Ask TTS for a short WAV (tone placeholder)
$tempWav = [System.IO.Path]::GetTempFileName().Replace('.tmp','.wav')
try {
  $resp = Invoke-WebRequest -Uri $TtsUrl -Method Post -Body "hello" -ContentType "text/plain"
  [IO.File]::WriteAllBytes($tempWav, $resp.Content)
  Write-Host "Saved test WAV -> $tempWav"
}
catch {
  Write-Warning "TTS warmup failed: $($_.Exception.Message)"
}

# 2) Send WAV to STT to trigger model download/cache
try {
  $form = @{ file = Get-Item $tempWav }
  $resp2 = Invoke-WebRequest -Uri $SttUrl -Method Post -Form $form
  Write-Host "STT warm response: $($resp2.Content)"
}
catch {
  Write-Warning "STT warmup failed: $($_.Exception.Message)"
}
finally {
  if (Test-Path $tempWav) { Remove-Item $tempWav -Force }
}
