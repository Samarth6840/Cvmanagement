namespace CvManagement.Data.Entities.Attributes;

/// <summary>
/// The fixed, seeded category list. Ids are stable and never database-generated, so they can
/// be referenced from code (the built-in attributes, the seed data) and from SQL without
/// renumbering anything.
///
/// These ids are also what the <c>AddAttributeCategoryLookup</c> migration inserts, and what
/// the pre-existing enum values were backfilled to.
/// </summary>
public static class AttributeCategoryCatalog
{
    public const int Certification = 1;
    public const int DomainKnowledge = 2;
    public const int PersonalInformation = 3;
    public const int SoftSkills = 4;
    public const int TechnicalSkills = 5;
    public const int Education = 6;
    public const int Experience = 7;
    public const int Other = 8;

    public static readonly (int Id, string Name)[] All =
    [
        (Certification, "Certification"),
        (DomainKnowledge, "Domain Knowledge"),
        (PersonalInformation, "Personal Information"),
        (SoftSkills, "Soft Skills"),
        (TechnicalSkills, "Technical Skills"),
        (Education, "Education"),
        (Experience, "Experience"),
        (Other, "Other")
    ];
}
