namespace CvManagement.Data.Entities.Identity;

public static class RoleNames
{
    public const string Candidate = "Candidate";
    public const string Recruiter = "Recruiter";
    public const string Administrator = "Administrator";

    public static readonly IReadOnlyList<string> All = [Candidate, Recruiter, Administrator];

    public static string ToNormalized(string role) => role.ToUpperInvariant();
}
