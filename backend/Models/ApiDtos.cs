namespace PawMatch.Api.Models;

public sealed record PetDto(
    int Id,
    string Name,
    string Species,
    string Breed,
    int AgeYears,
    int ShelterId,
    string ShelterName,
    string Status,
    string? PhotoUrl,
    /// <summary>When <c>GET /api/pets?userId=…</c> is used: this adopter's row in <c>AdoptionApplication</c> (<c>pending</c> = IsAdopted 0, <c>adopted</c> = IsAdopted 1).</summary>
    string? MyApplicationStatus);

public sealed record ShelterDto(
    int Id,
    string Name,
    string City,
    string State,
    int PetsCount,
    int AdoptionsYtd,
    double? ApprovalRate);

public sealed record SummaryDto(
    int TotalPetsListed,
    int AvailableNow,
    int ApplicationsThisMonth,
    int NewUsersThisMonth);

/// <summary>Matches the <c>User</c> table (JSON uses camelCase).</summary>
public sealed record UserDto(int Id, string UserName, string UserEmail, string? TypePreference);

/// <summary>Staff session payload from <c>Employee</c> (no password).</summary>
public sealed record EmployeeDto(int Id, string EmployeeName, int? ShelterId);

public sealed record StaffUserListDto(int Id, string UserName, string UserEmail, string? TypePreference);

public sealed record StaffUserDetailDto(
    int Id,
    string UserName,
    string UserEmail,
    string? TypePreference,
    int ApplicationCount);

public sealed record StaffApplicationRowDto(
    int UserId,
    string UserEmail,
    string UserName,
    int PetId,
    string PetName,
    string PetType,
    bool IsAdopted,
    int? ShelterId);

public sealed record StaffShelterRowDto(int Id, string Name, string Address, int PetCount, int ApplicationCount);
