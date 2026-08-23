using System.Text;
using ArtifactBrowser.Client.Models;
using ArtifactBrowser.Options;
using Microsoft.Extensions.Options;

namespace ArtifactBrowser.Features.Files;

/// <summary>Produces bounded, safe previews of text/markdown files without exposing physical paths.</summary>
public sealed class PreviewService(PathGuard pathGuard, IOptions<ArtifactBrowserOptions> options)
{
    private readonly ArtifactBrowserOptions _options = options.Value;

    public async Task<PreviewDto> GetPreviewAsync(string? virtualPath, CancellationToken cancellationToken)
    {
        var resolved = pathGuard.Resolve(virtualPath);
        if (!File.Exists(resolved.PhysicalPath))
        {
            throw new FileNotFoundException("File not found", resolved.VirtualPath);
        }

        var extension = Path.GetExtension(resolved.PhysicalPath);
        var category = MimeHelper.Categorize(extension);
        var fileInfo = new FileInfo(resolved.PhysicalPath);

        if (category is MediaCategory.Image)
        {
            return new PreviewDto { Kind = PreviewKind.Image, MimeType = MimeHelper.GetContentType(extension), Size = fileInfo.Length };
        }

        if (category is MediaCategory.Audio)
        {
            return new PreviewDto { Kind = PreviewKind.Audio, MimeType = MimeHelper.GetContentType(extension), Size = fileInfo.Length };
        }

        if (category is MediaCategory.Video)
        {
            return new PreviewDto { Kind = PreviewKind.Video, MimeType = MimeHelper.GetContentType(extension), Size = fileInfo.Length };
        }

        if (category is MediaCategory.Pdf)
        {
            return new PreviewDto { Kind = PreviewKind.Pdf, MimeType = MimeHelper.GetContentType(extension), Size = fileInfo.Length };
        }

        if (!MimeHelper.IsPreviewableAsText(extension))
        {
            return new PreviewDto { Kind = PreviewKind.Unsupported, MimeType = MimeHelper.GetContentType(extension), Size = fileInfo.Length };
        }

        if (fileInfo.Length > _options.MaxTextPreviewBytes)
        {
            return new PreviewDto
            {
                Kind = PreviewKind.TooLarge,
                MimeType = MimeHelper.GetContentType(extension),
                Size = fileInfo.Length,
                Truncated = true,
            };
        }

        var isMarkdown = category is MediaCategory.Markdown;
        var maxBytes = (int)Math.Min(_options.MaxTextPreviewBytes, int.MaxValue);

        await using var stream = new FileStream(resolved.PhysicalPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 8192, useAsync: true);
        var buffer = new byte[Math.Min(maxBytes, checked((int)Math.Min(fileInfo.Length, maxBytes)))];
        var read = await stream.ReadAtLeastAsync(buffer.AsMemory(0, buffer.Length), buffer.Length, throwOnEndOfStream: false, cancellationToken);
        var truncated = fileInfo.Length > read;

        var text = DecodeText(buffer, read);

        return new PreviewDto
        {
            Kind = isMarkdown ? PreviewKind.Markdown : PreviewKind.Text,
            Content = text,
            Truncated = truncated,
            MimeType = MimeHelper.GetContentType(extension),
            Size = fileInfo.Length,
        };
    }

    private static string DecodeText(byte[] buffer, int length)
    {
        // Strip a UTF-8 BOM if present; treat content as UTF-8 with replacement for invalid sequences.
        var span = buffer.AsSpan(0, length);
        if (span.Length >= 3 && span[0] == 0xEF && span[1] == 0xBB && span[2] == 0xBF)
        {
            span = span[3..];
        }

        return Encoding.UTF8.GetString(span);
    }
}
