using System.Net.Mail;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using PawMatch.Api.Data;
using PawMatch.Api.Models;

namespace PawMatch.Api.Controllers;

/// <summary>
/// Dedicated auth routes (avoids nested paths like <c>/api/users/login</c> that some hosts mis-route).
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly MySqlConnectionFactory _db;
    private readonly ILogger<AuthController> _logger;

    public AuthController(MySqlConnectionFactory db, ILogger<AuthController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Adopter login — <c>User</c> table.</summary>
    [HttpPost("adopter")]
    public async Task<ActionResult<UserDto>> AdopterLogin([FromBody] UserLoginRequest body, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var email = body.UserEmail.Trim();
        if (string.IsNullOrEmpty(email) || !IsPlausibleEmail(email))
            return Unauthorized("Invalid email or password.");

        const string sql = """
            SELECT UserID AS Id, UserName, UserEmail, TypePreference
            FROM `User`
            WHERE UserEmail = @email AND Password = @password
            LIMIT 1
            """;

        try
        {
            await using var conn = _db.CreateConnection();
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@email", email);
            cmd.Parameters.AddWithValue("@password", body.Password);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return Unauthorized("Invalid email or password.");

            var tpOrd = reader.GetOrdinal("TypePreference");
            var dto = new UserDto(
                reader.GetInt32(reader.GetOrdinal("Id")),
                reader.GetString(reader.GetOrdinal("UserName")),
                reader.GetString(reader.GetOrdinal("UserEmail")),
                reader.IsDBNull(tpOrd) ? null : reader.GetString(tpOrd));

            return Ok(dto);
        }
        catch (MySqlException ex)
        {
            _logger.LogError(ex, "Database error during adopter login");
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    /// <summary>Staff login — <c>Employee</c> table.</summary>
    [HttpPost("staff")]
    public async Task<ActionResult<EmployeeDto>> StaffLogin([FromBody] EmployeeLoginRequest body, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var name = body.EmployeeName.Trim();
        if (string.IsNullOrEmpty(name))
            return Unauthorized("Invalid employee name or password.");

        const string sql = """
            SELECT EmployeeID AS Id, EmployeeName, ShelterID AS ShelterId
            FROM Employee
            WHERE EmployeeName = @name AND Password = @password
            LIMIT 1
            """;

        try
        {
            await using var conn = _db.CreateConnection();
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@password", body.Password);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return Unauthorized("Invalid employee name or password.");

            var shOrd = reader.GetOrdinal("ShelterId");
            var dto = new EmployeeDto(
                reader.GetInt32(reader.GetOrdinal("Id")),
                reader.GetString(reader.GetOrdinal("EmployeeName")),
                reader.IsDBNull(shOrd) ? null : reader.GetInt32(shOrd));

            return Ok(dto);
        }
        catch (MySqlException ex)
        {
            _logger.LogError(ex, "Database error during staff login");
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
