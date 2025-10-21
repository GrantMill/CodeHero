namespace CodeHero.Web.Services;

public sealed class SpeechDiagnosticsMonitor
{
 public sealed record CallInfo(DateTimeOffset Timestamp, int Status, long RequestBytes, long ResponseSize, double DurationMs, string? Note);

 private readonly object _gate = new();
 private CallInfo? _tts;
 private CallInfo? _stt;

 public void UpdateTts(int status, long reqBytes, long respBytes, double durationMs, string? note = null)
 {
 lock (_gate)
 {
 _tts = new CallInfo(DateTimeOffset.UtcNow, status, reqBytes, respBytes, durationMs, note);
 }
 }

 public void UpdateStt(int status, long reqBytes, long respChars, double durationMs, string? note = null)
 {
 lock (_gate)
 {
 _stt = new CallInfo(DateTimeOffset.UtcNow, status, reqBytes, respChars, durationMs, note);
 }
 }

 public CallInfo? GetLastTts()
 {
 lock (_gate) return _tts;
 }

 public CallInfo? GetLastStt()
 {
 lock (_gate) return _stt;
 }
}
