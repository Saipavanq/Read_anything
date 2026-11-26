using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ReadAloud.Services;

namespace ReadAloud.Tests;

public sealed class EdgeSynthesizerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "readaloud-synthesizer-tests-" + Guid.NewGuid().ToString("N"));

    public EdgeSynthesizerTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Temporary files are best-effort.
        }
        catch (UnauthorizedAccessException)
        {
            // Temporary files are best-effort.
        }
    }

    [Fact]
    public async Task EdgeSynthesizer_RejectsEmptyAudio()
    {
        var client = new FakeEdgeTtsClient { WriteEmptyFile = true };
        var synthesizer = new EdgeSynthesizer(client, TimeSpan.FromSeconds(1), _root);

        await Assert.ThrowsAnyAsync<Exception>(() => synthesizer.SynthesizeAsync(
            "text", "voice", "+0%", CancellationToken.None));

        Assert.Empty(Directory.GetFiles(_root, "*.mp3"));
    }

    [Fact]
    public async Task EdgeSynthesizer_TimesOutHungSynthesis()
    {
        var client = new FakeEdgeTtsClient { Hang = true };
        var synthesizer = new EdgeSynthesizer(client, TimeSpan.FromMilliseconds(100), _root);

        await Assert.ThrowsAnyAsync<Exception>(() => synthesizer.SynthesizeAsync(
            "text", "voice", "+0%", CancellationToken.None));

        Assert.True(client.CancellationObserved, "The hung synthesis must be cancelled.");
        Assert.Empty(Directory.GetFiles(_root, "*.mp3"));
    }

    private sealed class FakeEdgeTtsClient : IEdgeTtsClient
    {
        public bool WriteEmptyFile { get; init; }

        public bool Hang { get; init; }

        public volatile bool CancellationObserved;

        public async Task SaveAsync(
            string text,
            string voice,
            string rate,
            string target,
            CancellationToken token)
        {
            File.WriteAllText(target, WriteEmptyFile ? string.Empty : "fake mp3");
            if (!Hang)
            {
                return;
            }

            var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = token.Register(() =>
            {
                CancellationObserved = true;
                blocked.TrySetCanceled(token);
            });
            await blocked.Task.ConfigureAwait(false);
        }
    }
}
