using System.Globalization;

namespace CvManagement.Data;

/// <summary>
/// PostgreSQL's <c>timestamp with time zone</c> columns accept UTC only, while a value parsed
/// from an HTML date input arrives with <see cref="DateTimeKind.Unspecified"/>. Every date a
/// user types therefore goes through here, so the DateTime kind is never the reason a save
/// fails. (Npgsql's legacy timestamp behaviour switch would hide the problem rather than
/// fix it.)
/// </summary>
public static class UtcDate
{
    /// <summary>Parses an HTML date input value, treating it as UTC. Invalid input is null.</summary>
    public static DateTime? FromInput(string? value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
            : null;

    public static DateTime FromParts(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Utc);
}
