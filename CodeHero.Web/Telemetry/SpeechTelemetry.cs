using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CodeHero.Web;

public static class SpeechTelemetry
{
 public static readonly ActivitySource Activity = new("CodeHero.Speech");
 private static readonly Meter Meter = new("CodeHero.Speech");

 public static readonly Histogram<double> TtsDurationMs = Meter.CreateHistogram<double>("speech_tts_duration_ms", unit: "ms", description: "TTS endpoint duration");
 public static readonly Histogram<double> SttDurationMs = Meter.CreateHistogram<double>("speech_stt_duration_ms", unit: "ms", description: "STT endpoint duration");

 public static readonly Histogram<long> TtsRequestBytes = Meter.CreateHistogram<long>("speech_tts_request_bytes", unit: "bytes", description: "TTS request size");
 public static readonly Histogram<long> TtsResponseBytes = Meter.CreateHistogram<long>("speech_tts_response_bytes", unit: "bytes", description: "TTS response size");
 public static readonly Histogram<long> SttRequestBytes = Meter.CreateHistogram<long>("speech_stt_request_bytes", unit: "bytes", description: "STT request size");
 public static readonly Histogram<long> SttResponseChars = Meter.CreateHistogram<long>("speech_stt_response_chars", unit: "chars", description: "STT response length in characters");

 public static readonly Counter<long> TtsErrors = Meter.CreateCounter<long>("speech_tts_errors", description: "TTS failures");
 public static readonly Counter<long> SttErrors = Meter.CreateCounter<long>("speech_stt_errors", description: "STT failures");
}
