using System.Text.RegularExpressions;
using Markdig;

namespace ArtifactBrowser.Client.Utilities;

/// <summary>
/// Renders untrusted artifact Markdown to HTML. Raw HTML is disabled, generic attributes
/// are not enabled, and a final pass strips event-handler attributes and script URLs.
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
            .Build();

    private static readonly Regex EventHandlerAttributes = new(
        @"\s+on[a-z]+\s*=\s*(?:""[^""]*""|'[^']*'|[^\s>]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DangerousUrlAttribute = new(
        @"(?<=\b(?:href|src)\s*=\s*(['""]?))\s*(?:javascript|data|vbscript)\s*:",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string ToHtml(string? markdown)
    {
        var html = Markdown.ToHtml(markdown ?? string.Empty, Pipeline);
        html = EventHandlerAttributes.Replace(html, string.Empty);
        return DangerousUrlAttribute.Replace(html, "#");
    }
}
