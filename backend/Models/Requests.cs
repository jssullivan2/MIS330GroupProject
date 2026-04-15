using System.ComponentModel.DataAnnotations;

namespace PawMatch.Api.Models;

public sealed class CreateUserRequest
{
    [Required]
    [MaxLength(100)]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(100)]
    public string UserEmail { get; set; } = string.Empty;

    /// <summary>Stored as plain text per course schema (<c>Password VARCHAR(255)</c>).</summary>
    [Required]
    [MinLength(4)]
    [MaxLength(255)]
    public string Password { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? TypePreference { get; set; }
}

public sealed class UserLoginRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(100)]
    public string UserEmail { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string Password { get; set; } = string.Empty;
}

public sealed class EmployeeLoginRequest
{
    [Required]
    [MaxLength(100)]
    public string EmployeeName { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string Password { get; set; } = string.Empty;
}

public sealed class AdoptPetRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int UserId { get; set; }
}

public sealed class CreatePetStaffRequest
{
    [Required]
    [MaxLength(100)]
    public string PetName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? PetBreed { get; set; }

    [Required]
    [RegularExpression("^(Cat|Dog)$", ErrorMessage = "PetType must be Cat or Dog.")]
    public string PetType { get; set; } = string.Empty;

    public int? ShelterId { get; set; }

    public int? EmployeeId { get; set; }
}

public sealed class UpdatePetStaffRequest
{
    [Required]
    [MaxLength(100)]
    public string PetName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? PetBreed { get; set; }

    [Required]
    [RegularExpression("^(Cat|Dog)$", ErrorMessage = "PetType must be Cat or Dog.")]
    public string PetType { get; set; } = string.Empty;

    public int? ShelterId { get; set; }

    public int? EmployeeId { get; set; }
}

public sealed class StaffApplicationUpsertRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int UserId { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int PetId { get; set; }

    public bool IsAdopted { get; set; }
}

public sealed class StaffApplicationDeleteRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int UserId { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int PetId { get; set; }
}
