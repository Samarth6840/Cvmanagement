namespace CvManagement.Data.Entities.Attributes;

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
