namespace CodeHero.Web.Services;

public sealed class AudioCapabilities
{
 public bool secure { get; set; }
 public bool hasMediaDevices { get; set; }
 public bool hasGetUserMedia { get; set; }
 public bool hasMediaRecorder { get; set; }
 public string? preferred { get; set; }
 public string? micPerm { get; set; }
}
