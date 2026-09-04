namespace CvManagement.Services.Markdown;

public interface IMarkdownRenderer
{
    string ToHtml(string markdown);
}
