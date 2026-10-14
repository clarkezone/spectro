using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Spectro.Domain;

namespace Spectro.Infrastructure.Tests;

public sealed class StoryImageCacheTests : IDisposable
{
    private const string ImageUrl = "https://images.example/story.png";
    private const int MaxImageBytes = 3 * 1024 * 1024;
    private const long MaxCacheBytes = 50L * 1024 * 1024;
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "Spectro.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CacheHitUsesStableNameAcrossInstancesWithoutRedownloading()
    {
        using var handler = new FakeHandler((_, _) => Task.FromResult(Response(Png)));
        using var client = new HttpClient(handler);
        var cache = new StoryImageCache(client, _directory);
        Assert.Null(cache.GetCachedPath(ImageUrl));

        await cache.CacheAsync([Story(ImageUrl), Story(ImageUrl)]);
        var reopened = new StoryImageCache(client, _directory);
        await reopened.CacheAsync([Story(ImageUrl)]);

        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(ExpectedPath(ImageUrl), reopened.GetCachedPath(ImageUrl));
        Assert.Equal(Png, await File.ReadAllBytesAsync(ExpectedPath(ImageUrl)));
        Assert.Single(Directory.GetFiles(_directory));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("http://images.example/story.png")]
    [InlineData("file:///tmp/story.png")]
    [InlineData("data:image/png;base64,AAAA")]
    [InlineData("https://user:password@images.example/story.png")]
    public async Task UnsupportedUrlsNeverUseNetwork(string? url)
    {
        using var handler = new FakeHandler((_, _) => throw new InvalidOperationException("Unexpected request"));
        using var client = new HttpClient(handler);
        var cache = new StoryImageCache(client, _directory);

        await cache.CacheAsync([Story(url)]);

        Assert.Null(cache.GetCachedPath(url));
        Assert.Equal(0, handler.RequestCount);
        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Theory]
    [InlineData("image/svg+xml")]
    [InlineData("text/html")]
    [InlineData("application/octet-stream")]
    [InlineData(null)]
    public async Task UntrustedMimeIsReportedAndNeverCached(string? mime)
    {
        var errors = new List<Exception>();
        using var handler = new FakeHandler((_, _) => Task.FromResult(Response(Png, mime)));
        using var client = new HttpClient(handler);
        var cache = new StoryImageCache(client, _directory, errors.Add);

        await cache.CacheAsync([Story(ImageUrl)]);

        Assert.IsType<InvalidDataException>(Assert.Single(errors));
        Assert.Null(cache.GetCachedPath(ImageUrl));
        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Theory]
    [InlineData("image/png", "<html>not a bitmap</html>")]
    [InlineData("image/jpeg", "<svg>not a bitmap</svg>")]
    public async Task SpoofedBitmapMimeIsRejected(string mime, string body)
    {
        var errors = new List<Exception>();
        using var handler = new FakeHandler((_, _) =>
            Task.FromResult(Response(Encoding.UTF8.GetBytes(body), mime)));
        using var client = new HttpClient(handler);
        var cache = new StoryImageCache(client, _directory, errors.Add);

        await cache.CacheAsync([Story(ImageUrl)]);

        Assert.IsType<InvalidDataException>(Assert.Single(errors));
        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Theory]
    [InlineData("image/jpeg", "FFD8FFE000104A4649460001")]
    [InlineData("image/gif", "474946383961010001008000")]
    [InlineData("image/webp", "524946460C000000574542505650384C")]
    public async Task SupportedBitmapSignaturesCanBeLookedUp(string mime, string hex)
    {
        var bytes = Convert.FromHexString(hex);
        using var handler = new FakeHandler((_, _) => Task.FromResult(Response(bytes, mime)));
        using var client = new HttpClient(handler);
        var cache = new StoryImageCache(client, _directory);

        await cache.CacheAsync([Story(ImageUrl)]);

        Assert.Equal(ExpectedPath(ImageUrl), cache.GetCachedPath(ImageUrl));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(ExpectedPath(ImageUrl)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OversizedKnownOrStreamingBodyIsReportedAndCleanedUp(bool knownLength)
    {
        var bytes = new byte[MaxImageBytes + 1];
        Png.CopyTo(bytes, 0);
        var errors = new List<Exception>();
        using var handler = new FakeHandler((_, _) =>
        {
            var response = Response(bytes);
            if (!knownLength)
            {
                response.Content.Dispose();
                response.Content = new StreamContent(new NonSeekableStream(new MemoryStream(bytes)));
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                Assert.Null(response.Content.Headers.ContentLength);
            }
            return Task.FromResult(response);
        });
        using var client = new HttpClient(handler);
        var cache = new StoryImageCache(client, _directory, errors.Add);

        await cache.CacheAsync([Story(ImageUrl)]);

        Assert.IsType<InvalidDataException>(Assert.Single(errors));
        Assert.Null(cache.GetCachedPath(ImageUrl));
        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task ExactSizeLimitIsAccepted()
    {
        var bytes = new byte[MaxImageBytes];
        Png.CopyTo(bytes, 0);
        using var handler = new FakeHandler((_, _) => Task.FromResult(Response(bytes)));
        using var client = new HttpClient(handler);
        var cache = new StoryImageCache(client, _directory);

        await cache.CacheAsync([Story(ImageUrl)]);

        Assert.Equal(MaxImageBytes, new FileInfo(cache.GetCachedPath(ImageUrl)!).Length);
    }

    [Fact]
    public async Task CancellationDuringStreamingPropagatesAndRemovesTemporaryFile()
    {
        using var cancellation = new CancellationTokenSource();
        using var body = new BlockingStream(Png);
        using var handler = new FakeHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            return Task.FromResult(response);
        });
        using var client = new HttpClient(handler);
        var errors = new List<Exception>();
        var cache = new StoryImageCache(client, _directory, errors.Add);

        var run = cache.CacheAsync([Story(ImageUrl)], cancellation.Token);
        await body.Blocked.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Single(Directory.GetFiles(_directory, "*.tmp"));
        Assert.Null(cache.GetCachedPath(ImageUrl));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.Empty(errors);
        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task NetworkFailureIsReportedAndNextImageStillDownloads()
    {
        var failure = new HttpRequestException("Offline");
        var errors = new List<Exception>();
        using var handler = new FakeHandler((request, _) =>
            request.RequestUri!.AbsoluteUri == ImageUrl
                ? Task.FromException<HttpResponseMessage>(failure)
                : Task.FromResult(Response(Png)));
        using var client = new HttpClient(handler);
        var cache = new StoryImageCache(client, _directory, errors.Add);
        const string otherUrl = "https://images.example/other.png";

        await cache.CacheAsync([Story(ImageUrl), Story(otherUrl)]);

        Assert.Same(failure, Assert.Single(errors));
        Assert.Null(cache.GetCachedPath(ImageUrl));
        Assert.NotNull(cache.GetCachedPath(otherUrl));
        Assert.Single(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task FailureWithoutCallbackPropagates()
    {
        using var handler = new FakeHandler((_, _) => throw new HttpRequestException("Offline"));
        using var client = new HttpClient(handler);
        var cache = new StoryImageCache(client, _directory);

        await Assert.ThrowsAsync<HttpRequestException>(() => cache.CacheAsync([Story(ImageUrl)]));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Redirect)]
    public async Task FailedOrRedirectResponseNeverCreatesFile(HttpStatusCode status)
    {
        var errors = new List<Exception>();
        using var handler = new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(status)));
        using var client = new HttpClient(handler);
        var cache = new StoryImageCache(client, _directory, errors.Add);

        await cache.CacheAsync([Story(ImageUrl)]);

        Assert.IsType<HttpRequestException>(Assert.Single(errors));
        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task TruncatedBodyIsNotCommitted()
    {
        var errors = new List<Exception>();
        using var handler = new FakeHandler((_, _) =>
        {
            var response = Response(Png);
            response.Content.Headers.ContentLength = Png.Length + 1;
            return Task.FromResult(response);
        });
        using var client = new HttpClient(handler);
        var cache = new StoryImageCache(client, _directory, errors.Add);

        await cache.CacheAsync([Story(ImageUrl)]);

        Assert.IsType<InvalidDataException>(Assert.Single(errors));
        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task RunsAreSerializedAndWaitingRunCanBeCancelled()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new FakeHandler(async (_, token) =>
        {
            entered.SetResult();
            await release.Task.WaitAsync(token);
            return Response(Png);
        });
        using var client = new HttpClient(handler);
        var cache = new StoryImageCache(client, _directory);
        var first = cache.CacheAsync([Story(ImageUrl)]);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource();
        var cancelled = cache.CacheAsync([Story(ImageUrl)], cancellation.Token);
        var second = cache.CacheAsync([Story(ImageUrl)]);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        Assert.Equal(1, handler.RequestCount);
        release.SetResult();

        await Task.WhenAll(first, second);

        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task LimitsRunToOneHundredUniqueUrls()
    {
        using var handler = new FakeHandler((_, _) => Task.FromResult(Response(Png)));
        using var client = new HttpClient(handler);
        var cache = new StoryImageCache(client, _directory);
        var stories = Enumerable.Range(0, 150).SelectMany(index => new[]
        {
            Story($"https://images.example/{index}.png"), Story($"https://images.example/{index}.png")
        });

        await cache.CacheAsync(stories);

        Assert.Equal(100, handler.RequestCount);
        Assert.Equal(100, Directory.GetFiles(_directory, "*.img").Length);
    }

    [Fact]
    public async Task DiskBudgetEvictsOldestImagesAndPreservesUnrelatedFiles()
    {
        Directory.CreateDirectory(_directory);
        for (var index = 0; index < 18; index++)
        {
            var path = Path.Combine(_directory, $"{index:D2}.img");
            using var file = File.Create(path);
            file.SetLength(MaxImageBytes);
        }
        var oldest = Path.Combine(_directory, "00.img");
        File.SetLastWriteTimeUtc(oldest, DateTime.UtcNow.AddDays(-10));
        var unrelated = Path.Combine(_directory, "keep.txt");
        await File.WriteAllTextAsync(unrelated, "keep");
        using var handler = new FakeHandler((_, _) => Task.FromResult(Response(Png)));
        using var client = new HttpClient(handler);
        var cache = new StoryImageCache(client, _directory);

        await cache.CacheAsync([Story(ImageUrl)]);

        Assert.False(File.Exists(oldest));
        Assert.True(File.Exists(unrelated));
        Assert.NotNull(cache.GetCachedPath(ImageUrl));
        Assert.InRange(new DirectoryInfo(_directory).GetFiles("*.img").Sum(file => file.Length), 1, MaxCacheBytes);
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public async Task EmptyRunAlsoEnforcesDiskBudget()
    {
        Directory.CreateDirectory(_directory);
        using (var file = File.Create(Path.Combine(_directory, "old.img")))
        {
            file.SetLength(MaxCacheBytes + 1);
        }
        using var handler = new FakeHandler((_, _) => throw new InvalidOperationException("Unexpected request"));
        using var client = new HttpClient(handler);

        await new StoryImageCache(client, _directory).CacheAsync([]);

        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task LookupRejectsNonBitmapFilesAndDownloadAtomicallyReplacesThem()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(ExpectedPath(ImageUrl), "<html>bad cached file</html>");
        using var handler = new FakeHandler((_, _) => Task.FromResult(Response(Png)));
        using var client = new HttpClient(handler);
        var cache = new StoryImageCache(client, _directory);

        Assert.Null(cache.GetCachedPath(ImageUrl));
        Assert.Equal(0, handler.RequestCount);
        await cache.CacheAsync([Story(ImageUrl)]);

        Assert.NotNull(cache.GetCachedPath(ImageUrl));
        Assert.Equal(Png, await File.ReadAllBytesAsync(ExpectedPath(ImageUrl)));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Theory]
    [InlineData("Authorization")]
    [InlineData("Proxy-Authorization")]
    [InlineData("Cookie")]
    public async Task RejectsClientCredentialHeadersBeforeNetwork(string name)
    {
        using var handler = new FakeHandler((_, _) => throw new InvalidOperationException("Unexpected request"));
        using var client = new HttpClient(handler);
        client.DefaultRequestHeaders.TryAddWithoutValidation(name, "secret");
        var cache = new StoryImageCache(client, _directory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.CacheAsync([Story(ImageUrl)]));

        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task TransportTimeoutIsReportedWithoutPartialFiles()
    {
        var errors = new List<Exception>();
        using var handler = new FakeHandler((_, _) => throw new TaskCanceledException("Transport timeout"));
        using var client = new HttpClient(handler);
        var cache = new StoryImageCache(client, _directory, errors.Add);

        await cache.CacheAsync([Story(ImageUrl)]);

        Assert.IsType<TimeoutException>(Assert.Single(errors));
        Assert.Empty(Directory.GetFiles(_directory));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private string ExpectedPath(string url) => Path.Combine(
        _directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(new Uri(url).AbsoluteUri))) + ".img");

    private static Story Story(string? url) => new(
        "story", 1, null, null, "Title", null, null, "", "", url, DateTimeOffset.UnixEpoch, false, false);

    private static HttpResponseMessage Response(byte[] bytes, string? mime = "image/png")
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        if (mime is not null)
        {
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(mime);
        }
        return response;
    }

    private sealed class FakeHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            RequestCount++;
            Assert.Equal(Uri.UriSchemeHttps, request.RequestUri!.Scheme);
            Assert.Null(request.Headers.Authorization);
            Assert.False(request.Headers.Contains("Cookie"));
            return send(request, token);
        }
    }

    private class NonSeekableStream(Stream inner) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    private sealed class BlockingStream(byte[] prefix) : NonSeekableStream(new MemoryStream(prefix))
    {
        public TaskCompletionSource Blocked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await base.ReadAsync(buffer, cancellationToken);
            if (read != 0)
            {
                return read;
            }
            Blocked.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
    }
}
