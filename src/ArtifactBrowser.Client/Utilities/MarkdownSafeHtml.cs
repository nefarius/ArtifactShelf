using System.Text.RegularExpressions;
using Markdig;
using Markdig.Renderers;

namespace ArtifactBrowser.Client.Utilities;

/// <summary>
/// Renders untrusted artifact Markdown to HTML. Raw HTML is disabled, generic attributes
/// are not enabled, and link/image URLs are rewritten on the HTML render path so
/// literal text is left unchanged.
/// </summary>
public static class MarkdownSafeHtml
{
    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder()
            .DisableHtml()
            .UsePipeTables()
            .UseEmphasisExtras()
            .UseAutoIdentifiers()
            .UseTaskLists()
            .Use<SafeLinkExtension>()
            .Build();

    private static readonly Regex DangerousScheme = new(
        @"^\s*(?:javascript|data|vbscript)\s*:",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string ToHtml(string? markdown)
        => Markdown.ToHtml(markdown ?? string.Empty, Pipeline);

    private static string RewriteDangerousUrl(string url)
        => DangerousScheme.IsMatch(url) ? "#" : url;

    private sealed class SafeLinkExtension : IMarkdownExtension
    {
        public void Setup(MarkdownPipelineBuilder pipeline)
        {
        }

        public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
        {
            if (renderer is HtmlRenderer htmlRenderer)
            {
                htmlRenderer.LinkRewriter = RewriteDangerousUrl;
            }
        }
    }
}
