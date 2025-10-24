using System.Net.Http.Headers;
using System.Text;

namespace CodeHero.Tests;

[TestClass]
public class SpeechApiTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Tts_Returns_Wav_And_NoCacheHeaders()
    {
        var cancellationToken = new CancellationTokenSource(DefaultTimeout).Token;

        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.CodeHero_AppHost>(cancellationToken);
        appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();
            clientBuilder.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });
        });

        await using var app = await appHost.BuildAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        await app.StartAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);

        // Wait for web and tts container to be healthy
        await app.ResourceNotifications.WaitForResourceHealthyAsync("tts-http", cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("webfrontend", cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);

        var http = app.CreateHttpClient("webfrontend");
        var content = new StringContent("hello world", Encoding.UTF8, "text/plain");
        var resp = await http.PostAsync("/api/tts?voice=en-US-JennyNeural", content, cancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        Assert.AreEqual("audio/wav", resp.Content.Headers.ContentType?.MediaType);
        Assert.IsTrue(resp.Headers.CacheControl?.NoStore ?? false, "Cache-Control no-store expected");
        var bytes = await resp.Content.ReadAsByteArrayAsync(cancellationToken);
        Assert.IsTrue(bytes.Length > 100, "WAV data expected");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task Stt_Accepts_Short_Wav()
    {
        var cancellationToken = new CancellationTokenSource(DefaultTimeout).Token;

        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.CodeHero_AppHost>(cancellationToken);
        appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();
            clientBuilder.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });
        });

        await using var app = await appHost.BuildAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        await app.StartAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);

        // Wait for web and stt container to be healthy
        await app.ResourceNotifications.WaitForResourceHealthyAsync("stt-whisper", cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("webfrontend", cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);

        var http = app.CreateHttpClient("webfrontend");
        var wav = MakeSilentWav(seconds: 1.0, sampleRate: 16000);
        using var body = new ByteArrayContent(wav);
        body.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        var resp = await http.PostAsync("/api/stt", body, cancellationToken);

        // We accept200 with text or200 with empty string depending on STT model/text
        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        var text = await resp.Content.ReadAsStringAsync(cancellationToken);
        Assert.IsNotNull(text);
    }

    private static byte[] MakeSilentWav(double seconds, int sampleRate)
    {
        int samples = (int)(seconds * sampleRate);
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);

        int dataSize = samples * 2; //16-bit mono
        int fmtChunkSize = 16;
        int riffChunkSize = 4 + (8 + fmtChunkSize) + (8 + dataSize);

        // RIFF header
        bw.Write(Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(riffChunkSize);
        bw.Write(Encoding.ASCII.GetBytes("WAVE"));

        // fmt chunk
        bw.Write(Encoding.ASCII.GetBytes("fmt "));
        bw.Write(fmtChunkSize);
        bw.Write((short)1); // PCM
        bw.Write((short)1); // mono
        bw.Write(sampleRate);
        bw.Write(sampleRate * 2); // byte rate
        bw.Write((short)2); // block align
        bw.Write((short)16); // bits per sample

        // data chunk
        bw.Write(Encoding.ASCII.GetBytes("data"));
        bw.Write(dataSize);
        // write silence
        for (int i = 0; i < samples; i++) bw.Write((short)0);

        bw.Flush();
        return ms.ToArray();
    }
}