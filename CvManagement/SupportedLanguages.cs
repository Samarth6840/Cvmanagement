namespace CvManagement;

public static class SupportedLanguages
{
    public record Language(string Code, string NativeName, string EnglishName);

    public static readonly Language[] All =
    [
        new("en", "English", "English"),
        new("pl", "Polski", "Polish")
    ];

    public static bool IsSupported(string? code) =>
        code is not null && All.Any(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));

    public static string Normalize(string? code) =>
        IsSupported(code) ? All.First(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase)).Code : "en";
}
