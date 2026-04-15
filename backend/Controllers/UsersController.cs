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
