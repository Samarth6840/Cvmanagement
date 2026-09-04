using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace CvManagement.Data.Entities.Identity;

public class ApplicationRole : IdentityRole<Guid>
{
    [MaxLength(256)]
    public string Description { get; set; } = string.Empty;
}
