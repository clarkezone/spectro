using System.Net;
using System.Security.Cryptography;
using System.Text;
using Spectro.Domain;

namespace Spectro.Infrastructure;

/// <summary>A bounded, disk-only lookup cache for HTTPS story thumbnails.</summary>
public sealed class StoryImageCache
{
    private const int MaxImages = 100;
    private const int MaxImageBytes = 3 * 1024 * 1024;
    private const long MaxCacheBytes = 50L * 1024 * 1024;
    private readonly HttpClient _httpClient;
    private readonly string _directory;
    private readonly Action<Exception>? _onError;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <param name="httpClient">
    /// Caller-owned, dedicated anonymous client. Its handler MUST disable cookies,
    /// credentials (including client certificates), and automatic redirects.
    /// Use <see cref="CreateHttpClient"/> for a correctly configured transport.
    /// HttpClient does not expose handler settings, so an injected transport cannot be verified here.
    /// </param>
    /// <param name="cacheDirectory">A dedicated application-owned cache directory.</param>
    /// <param name="onError">
    /// Receives download, validation, and filesystem failures. With no callback, failures propagate.
    /// Caller cancellation always propagates. Callback exceptions also propagate.
    /// </param>
    public StoryImageCache(HttpClient httpClient, string cacheDirectory, Action<Exception>? onError = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);
        _httpClient = httpClient;
        _directory = Path.GetFullPath(cacheDirectory);
        _onError = onError;
    }

    /// <summary>Creates an anonymous client owned and disposed by the caller.</summary>
    public static HttpClient CreateHttpClient() => new(new SocketsHttpHandler
    {
        UseCookies = false,
        Credentials = null,
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.None
    })
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    /// <summary>
    /// Caches at most 100 unique supported URLs per run, serializing runs on this instance.
    /// Unsupported URLs are ignored; each download has a ten-second deadline including streaming.
    /// </summary>
    public async Task CacheAsync(IEnumerable<Story> stories, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stories);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Default headers are merged by HttpClient after SendAsync is called.
            if (_httpClient.DefaultRequestHeaders.Contains("Authorization")
                || _httpClient.DefaultRequestHeaders.Contains("Proxy-Authorization")
                || _httpClient.DefaultRequestHeaders.Contains("Cookie"))
            {
                throw new InvalidOperationException("The thumbnail client must not contain credential or cookie headers.");
            }

            try
            {
                Directory.CreateDirectory(_directory);
                TrimCache(MaxCacheBytes, cancellationToken);
            }
            catch (Exception error) when (IsIoFailure(error) && _onError is not null)
            {
                _onError(error);
                return;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var story in stories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TryGetUri(story.ImageUri, out var uri) || !seen.Add(uri.AbsoluteUri))
                {
                    continue;
                }

                try
                {
                    if (GetCachedPathCore(GetPath(uri)) is null)
                    {
                        await DownloadAsync(uri, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException error) when (_onError is not null)
                {
                    _onError(new TimeoutException("The thumbnail request timed out.", error));
                }
                catch (Exception error) when (IsIoFailure(error) && _onError is not null)
                {
                    _onError(error);
                }

                if (seen.Count == MaxImages)
                {
                    break;
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Returns an existing bounded bitmap-signature file in the cache, or null. Never uses the network.
    /// This does not decode images; the UI must handle corrupt images and files evicted after lookup.
    /// </summary>
    public string? GetCachedPath(string? imageUri)
    {
        if (!TryGetUri(imageUri, out var uri))
        {
            return null;
        }

        try
        {
            return GetCachedPathCore(GetPath(uri));
        }
        catch (Exception error) when (IsIoFailure(error) && _onError is not null)
        {
            _onError(error);
            return null;
        }
    }

    private static string? GetCachedPathCore(string path)
    {
        try
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                return null;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            if (stream.Length is <= 0 or > MaxImageBytes)
            {
                return null;
            }
            Span<byte> header = stackalloc byte[12];
            var length = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
            return GetBitmapMime(header[..length]) is not null ? path : null;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    private async Task DownloadAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        var token = deadline.Token;
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Accept.ParseAdd("image/png, image/jpeg, image/gif, image/webp");
        using var response = await _httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.RequestMessage?.RequestUri is Uri finalUri && finalUri != uri)
        {
            throw new InvalidDataException("Redirected thumbnail responses are not allowed.");
        }

        var mime = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
        if (mime is not ("image/png" or "image/jpeg" or "image/gif" or "image/webp"))
        {
            throw new InvalidDataException("The thumbnail response is not a supported bitmap MIME type.");
        }
        if (response.Content.Headers.ContentLength is > MaxImageBytes or 0)
        {
            throw new InvalidDataException("The thumbnail response exceeds the size limit or is empty.");
        }

        await using var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        var header = new byte[12];
        var headerLength = await input.ReadAtLeastAsync(header, header.Length, false, token).ConfigureAwait(false);
        if (GetBitmapMime(header.AsSpan(0, headerLength)) != mime)
        {
            throw new InvalidDataException("The thumbnail signature does not match its bitmap MIME type.");
        }

        // Reserve space for the temporary file as well as committed images.
        TrimCache(MaxCacheBytes - MaxImageBytes, token);
        var temporaryPath = Path.Combine(_directory, $"{Guid.NewGuid():N}.tmp");
        try
        {
            long total = headerLength;
            await using (var output = new FileStream(
                temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await output.WriteAsync(header.AsMemory(0, headerLength), token).ConfigureAwait(false);
                var buffer = new byte[81920];
                int read;
                while ((read = await input.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
                {
                    total += read;
                    if (total > MaxImageBytes)
                    {
                        throw new InvalidDataException("The thumbnail stream exceeds the size limit.");
                    }
                    await output.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                }
                if (response.Content.Headers.ContentLength is long expected && total != expected)
                {
                    throw new InvalidDataException("The thumbnail response was truncated.");
                }
                await output.FlushAsync(token).ConfigureAwait(false);
            }

            token.ThrowIfCancellationRequested();
            File.Move(temporaryPath, GetPath(uri), overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private void TrimCache(long budget, CancellationToken cancellationToken)
    {
        var files = new DirectoryInfo(_directory).GetFiles("*.img")
            .OrderBy(file => file.LastWriteTimeUtc)
            .ThenBy(file => file.Name, StringComparer.Ordinal)
            .ToArray();
        var total = files.Sum(file => file.Length);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (total <= budget)
            {
                break;
            }
            var length = file.Length;
            file.Delete();
            total -= length;
        }
    }

    private string GetPath(Uri uri) => Path.Combine(
        _directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri))) + ".img");

    private static bool TryGetUri(string? value, out Uri uri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var parsed)
            && parsed.Scheme == Uri.UriSchemeHttps && parsed.UserInfo.Length == 0)
        {
            uri = parsed;
            return true;
        }
        uri = null!;
        return false;
    }

    private static string? GetBitmapMime(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 8 && header[0] == 0x89 && header[1..8].SequenceEqual("PNG\r\n\x1a\n"u8))
        {
            return "image/png";
        }
        if (header.Length >= 3 && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff)
        {
            return "image/jpeg";
        }
        if (header.StartsWith("GIF87a"u8) || header.StartsWith("GIF89a"u8))
        {
            return "image/gif";
        }
        return header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8)
            && header[8..12].SequenceEqual("WEBP"u8) ? "image/webp" : null;
    }

    private static bool IsIoFailure(Exception error) =>
        error is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException;
}
