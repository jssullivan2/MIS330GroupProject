using System.Net.Mail;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using PawMatch.Api.Data;
using PawMatch.Api.Models;

namespace PawMatch.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly MySqlConnectionFactory _db;
    private readonly ILogger<UsersController> _logger;

    public UsersController(MySqlConnectionFactory db, ILogger<UsersController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Creates a row in <c>User</c> (MySQL reserved name — table is quoted in SQL).</summary>
    [HttpPost]
    public async Task<ActionResult<UserDto>> Register([FromBody] CreateUserRequest body, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var email = body.UserEmail.Trim();
        var userName = body.UserName.Trim();
        if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(email) || !IsPlausibleEmail(email))
            return BadRequest("A valid user name and email are required.");

        var typePref = string.IsNullOrWhiteSpace(body.TypePreference) ? null : body.TypePreference.Trim();

        const string insertSql = """
            INSERT INTO `User` (UserName, UserEmail, Password, TypePreference)
            VALUES (@userName, @userEmail, @password, @typePreference)
            """;

        try
        {
            await using var conn = _db.CreateConnection();
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new MySqlCommand(insertSql, conn);
            cmd.Parameters.AddWithValue("@userName", userName);
            cmd.Parameters.AddWithValue("@userEmail", email);
            cmd.Parameters.AddWithValue("@password", body.Password);
            cmd.Parameters.AddWithValue("@typePreference", typePref is null ? DBNull.Value : typePref);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            var newId = (int)cmd.LastInsertedId;

            const string selectSql = """
                SELECT UserID AS Id, UserName, UserEmail, TypePreference
                FROM `User`
                WHERE UserID = @id
                """;
            cmd.CommandText = selectSql;
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@id", newId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return Problem(statusCode: StatusCodes.Status500InternalServerError);

            var tpOrd = reader.GetOrdinal("TypePreference");
            var dto = new UserDto(
                reader.GetInt32(reader.GetOrdinal("Id")),
                reader.GetString(reader.GetOrdinal("UserName")),
                reader.GetString(reader.GetOrdinal("UserEmail")),
                reader.IsDBNull(tpOrd) ? null : reader.GetString(tpOrd));

            return StatusCode(StatusCodes.Status201Created, dto);
        }
        catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.DuplicateKeyEntry)
        {
            return Conflict("An account with this email already exists.");
        }
        catch (MySqlException ex)
        {
            _logger.LogError(ex, "Database error creating user");
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpGet("{userId:int}/applications")]
    public async Task<ActionResult<IReadOnlyList<UserApplicationDto>>> ListUserApplications(
        int userId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
              a.PetID AS PetId,
              p.PetName,
              p.PetType,
              COALESCE(s.ShelterName, 'Shelter not set') AS ShelterName,
              a.IsAdopted,
              CASE
                WHEN a.IsAdopted THEN 'Accepted'
                WHEN EXISTS (
                  SELECT 1
                  FROM AdoptionApplication ax
                  WHERE ax.PetID = a.PetID AND ax.IsAdopted = TRUE
                ) THEN 'Denied'
                ELSE 'Pending'
              END AS ApplicationStatus
            FROM AdoptionApplication a
            INNER JOIN Pet p ON p.PetID = a.PetID
            LEFT JOIN Shelter s ON s.ShelterID = p.ShelterID
            WHERE a.UserID = @userId
            ORDER BY a.PetID
            """;

        try
        {
            await using var conn = _db.CreateConnection();
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@userId", userId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var list = new List<UserApplicationDto>();
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(new UserApplicationDto(
                    reader.GetInt32(reader.GetOrdinal("PetId")),
                    reader.GetString(reader.GetOrdinal("PetName")),
                    reader.GetString(reader.GetOrdinal("PetType")),
                    reader.GetString(reader.GetOrdinal("ShelterName")),
                    reader.GetBoolean(reader.GetOrdinal("IsAdopted")),
                    reader.GetString(reader.GetOrdinal("ApplicationStatus"))));
            }

            return Ok(list);
        }
        catch (MySqlException ex)
        {
            _logger.LogError(ex, "Database error loading applications for user {UserId}", userId);
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static bool IsPlausibleEmail(string email)
    {
        try
        {
            _ = new MailAddress(email);
            return email.Contains('@', StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }
}
