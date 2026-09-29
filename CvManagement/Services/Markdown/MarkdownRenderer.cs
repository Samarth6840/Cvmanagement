using Markdig;

namespace CvManagement.Services.Markdown;

public class MarkdownRenderer : IMarkdownRenderer
{
    // DisableHtml is required: all Markdown sources are user-supplied, and the rendered
    // result is emitted as raw HTML in MarkdownPreview.razor.
    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .DisableHtml()
            .Build();

    public string ToHtml(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return string.Empty;

        return Markdig.Markdown.ToHtml(markdown, Pipeline);
    }
}
