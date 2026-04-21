using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using PawMatch.Api.Data;
using PawMatch.Api.Models;

namespace PawMatch.Api.Controllers;

/// <summary>
/// Shelter staff operations. Requires header <c>X-Staff-Employee-Id</c> matching a row in <c>Employee</c>.
/// </summary>
[ApiController]
[Route("api/staff")]
public sealed class StaffController : ControllerBase
{
    private readonly MySqlConnectionFactory _db;
    private readonly ILogger<StaffController> _logger;

    public StaffController(MySqlConnectionFactory db, ILogger<StaffController> logger)
    {
        _db = db;
        _logger = logger;
    }

    private bool TryGetStaffId(out int staffId)
    {
        staffId = 0;
        if (!Request.Headers.TryGetValue("X-Staff-Employee-Id", out var raw) || string.IsNullOrWhiteSpace(raw))
            return false;
        return int.TryParse(raw.ToString(), out staffId) && staffId > 0;
    }

    private async Task<bool> StaffExistsAsync(MySqlConnection conn, int employeeId, CancellationToken cancellationToken)
    {
        await using var cmd = new MySqlCommand("SELECT 1 FROM Employee WHERE EmployeeID = @id LIMIT 1", conn);
        cmd.Parameters.AddWithValue("@id", employeeId);
        return await cmd.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private async Task<ActionResult?> RequireStaffAsync(CancellationToken cancellationToken)
    {
        if (!TryGetStaffId(out var id))
            return Unauthorized("Missing or invalid X-Staff-Employee-Id header.");

        await using var conn = _db.CreateConnection();
        await conn.OpenAsync(cancellationToken);
        if (!await StaffExistsAsync(conn, id, cancellationToken))
            return Unauthorized("Employee not found.");

        return null;
    }

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<StaffUserListDto>>> ListUsers(CancellationToken cancellationToken)
    {
        var auth = await RequireStaffAsync(cancellationToken);
        if (auth is not null) return auth;

        const string sql = """
            SELECT UserID AS Id, UserName, UserEmail, TypePreference
            FROM `User`
            ORDER BY UserID
            """;

        try
        {
            await using var conn = _db.CreateConnection();
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var list = new List<StaffUserListDto>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var tp = reader.GetOrdinal("TypePreference");
                list.Add(new StaffUserListDto(
                    reader.GetInt32(reader.GetOrdinal("Id")),
                    reader.GetString(reader.GetOrdinal("UserName")),
                    reader.GetString(reader.GetOrdinal("UserEmail")),
                    reader.IsDBNull(tp) ? null : reader.GetString(tp)));
            }

            return Ok(list);
        }
        catch (MySqlException ex)
        {
            _logger.LogError(ex, "Staff list users failed");
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpGet("users/{userId:int}")]
    public async Task<ActionResult<StaffUserDetailDto>> GetUser(int userId, CancellationToken cancellationToken)
    {
        var auth = await RequireStaffAsync(cancellationToken);
        if (auth is not null) return auth;

        const string sql = """
            SELECT
              u.UserID AS Id,
              u.UserName,
              u.UserEmail,
              u.TypePreference,
              (SELECT COUNT(*) FROM AdoptionApplication a WHERE a.UserID = u.UserID) AS ApplicationCount
            FROM `User` u
            WHERE u.UserID = @id
            LIMIT 1
            """;

        try
        {
            await using var conn = _db.CreateConnection();
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", userId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return NotFound();

            var tp = reader.GetOrdinal("TypePreference");
            var dto = new StaffUserDetailDto(
                reader.GetInt32(reader.GetOrdinal("Id")),
                reader.GetString(reader.GetOrdinal("UserName")),
                reader.GetString(reader.GetOrdinal("UserEmail")),
                reader.IsDBNull(tp) ? null : reader.GetString(tp),
                reader.GetInt32(reader.GetOrdinal("ApplicationCount")));

            return Ok(dto);
        }
        catch (MySqlException ex)
        {
            _logger.LogError(ex, "Staff get user failed");
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpGet("pets")]
    public async Task<ActionResult<IReadOnlyList<PetDto>>> ListPets(CancellationToken cancellationToken)
    {
        var auth = await RequireStaffAsync(cancellationToken);
        if (auth is not null) return auth;

        var sql = $"""
            SELECT
              p.PetID AS Id,
              p.PetName AS Name,
              COALESCE(p.PetType, '') AS Species,
              COALESCE(p.PetBreed, '') AS Breed,
              0 AS AgeYears,
              COALESCE(p.ShelterID, 0) AS ShelterId,
              CASE
                WHEN p.ShelterID IS NULL THEN 'Shelter not set'
                ELSE CONCAT('Shelter ', p.ShelterID)
              END AS ShelterName,
              CASE
                WHEN EXISTS (
                  SELECT 1 FROM AdoptionApplication a
                  WHERE a.PetID = p.PetID AND a.IsAdopted = TRUE
                ) THEN 'Adopted'
                WHEN EXISTS (
                  SELECT 1 FROM AdoptionApplication a
                  WHERE a.PetID = p.PetID AND a.IsAdopted = FALSE
                ) THEN 'Pending'
                ELSE 'Available'
              END AS Status,
              {PetQueryFragments.SelectPhotoUrlColumn}
            FROM Pet p
            ORDER BY p.PetID
            """;

        try
        {
            await using var conn = _db.CreateConnection();
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var list = new List<PetDto>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var photoOrd = reader.GetOrdinal("PhotoUrl");
                list.Add(new PetDto(
                    reader.GetInt32(reader.GetOrdinal("Id")),
                    reader.GetString(reader.GetOrdinal("Name")),
                    reader.GetString(reader.GetOrdinal("Species")),
                    reader.GetString(reader.GetOrdinal("Breed")),
                    reader.GetInt32(reader.GetOrdinal("AgeYears")),
                    reader.GetInt32(reader.GetOrdinal("ShelterId")),
                    reader.GetString(reader.GetOrdinal("ShelterName")),
                    reader.GetString(reader.GetOrdinal("Status")),
                    reader.IsDBNull(photoOrd) ? null : reader.GetString(photoOrd)));
            }

            return Ok(list);
        }
        catch (MySqlException ex)
        {
            _logger.LogError(ex, "Staff list pets failed");
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpPost("pets")]
    public async Task<ActionResult> CreatePet([FromBody] CreatePetStaffRequest body, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var auth = await RequireStaffAsync(cancellationToken);
        if (auth is not null) return auth;
        TryGetStaffId(out var staffId);

        var empId = body.EmployeeId ?? staffId;
        const string sql = """
            INSERT INTO Pet (PetName, PetBreed, PetType, ShelterID, EmployeeID)
            VALUES (@name, @breed, @type, @shelterId, @empId)
            """;

        try
        {
            await using var conn = _db.CreateConnection();
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@name", body.PetName.Trim());
            cmd.Parameters.AddWithValue("@breed", string.IsNullOrWhiteSpace(body.PetBreed) ? DBNull.Value : body.PetBreed.Trim());
            cmd.Parameters.AddWithValue("@type", body.PetType);
            cmd.Parameters.AddWithValue("@shelterId", body.ShelterId.HasValue ? body.ShelterId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@empId", empId);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            return StatusCode(StatusCodes.Status201Created, new { id = (int)cmd.LastInsertedId });
        }
        catch (MySqlException ex)
        {
            _logger.LogError(ex, "Staff create pet failed");
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpPut("pets/{petId:int}")]
    public async Task<ActionResult> UpdatePet(int petId, [FromBody] UpdatePetStaffRequest body, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var auth = await RequireStaffAsync(cancellationToken);
        if (auth is not null) return auth;
        TryGetStaffId(out var staffId);
        var empId = body.EmployeeId ?? staffId;

        const string sql = """
            UPDATE Pet SET
              PetName = @name,
              PetBreed = @breed,
              PetType = @type,
              ShelterID = @shelterId,
              EmployeeID = @empId
            WHERE PetID = @petId
            """;

        try
        {
            await using var conn = _db.CreateConnection();
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@petId", petId);
            cmd.Parameters.AddWithValue("@name", body.PetName.Trim());
            cmd.Parameters.AddWithValue("@breed", string.IsNullOrWhiteSpace(body.PetBreed) ? DBNull.Value : body.PetBreed.Trim());
            cmd.Parameters.AddWithValue("@type", body.PetType);
            cmd.Parameters.AddWithValue("@shelterId", body.ShelterId.HasValue ? body.ShelterId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@empId", empId);
            var n = await cmd.ExecuteNonQueryAsync(cancellationToken);
            return n == 0 ? NotFound() : NoContent();
        }
        catch (MySqlException ex)
        {
            _logger.LogError(ex, "Staff update pet failed");
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpDelete("pets/{petId:int}")]
    public async Task<ActionResult> DeletePet(int petId, CancellationToken cancellationToken)
    {
        var auth = await RequireStaffAsync(cancellationToken);
        if (auth is not null) return auth;

        try
        {
            await using var conn = _db.CreateConnection();
            await conn.OpenAsync(cancellationToken);
            await using var tx = await conn.BeginTransactionAsync(cancellationToken);
            await using (var delA = new MySqlCommand("DELETE FROM AdoptionApplication WHERE PetID = @petId", conn, (MySqlTransaction)tx))
            {
                delA.Parameters.AddWithValue("@petId", petId);
                await delA.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var delP = new MySqlCommand("DELETE FROM Pet WHERE PetID = @petId", conn, (MySqlTransaction)tx))
            {
                delP.Parameters.AddWithValue("@petId", petId);
                var n = await delP.ExecuteNonQueryAsync(cancellationToken);
                if (n == 0)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return NotFound();
                }
            }

            await tx.CommitAsync(cancellationToken);
            return NoContent();
        }
        catch (MySqlException ex)
        {
            _logger.LogError(ex, "Staff delete pet failed");
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpGet("applications")]
    public async Task<ActionResult<IReadOnlyList<StaffApplicationRowDto>>> ListApplications(CancellationToken cancellationToken)
    {
        var auth = await RequireStaffAsync(cancellationToken);
        if (auth is not null) return auth;

        const string sql = """
            SELECT
              a.UserID AS UserId,
              u.UserEmail,
              u.UserName,
              a.PetID AS PetId,
              p.PetName,
              p.PetType,
              a.IsAdopted,
              p.ShelterID AS ShelterId
            FROM AdoptionApplication a
            INNER JOIN `User` u ON u.UserID = a.UserID
            INNER JOIN Pet p ON p.PetID = a.PetID
            ORDER BY a.UserID, a.PetID
            """;

        try
        {
            await using var conn = _db.CreateConnection();
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var list = new List<StaffApplicationRowDto>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var sh = reader.GetOrdinal("ShelterId");
                list.Add(new StaffApplicationRowDto(
                    reader.GetInt32(reader.GetOrdinal("UserId")),
                    reader.GetString(reader.GetOrdinal("UserEmail")),
                    reader.GetString(reader.GetOrdinal("UserName")),
                    reader.GetInt32(reader.GetOrdinal("PetId")),
                    reader.GetString(reader.GetOrdinal("PetName")),
                    reader.GetString(reader.GetOrdinal("PetType")),
                    reader.GetBoolean(reader.GetOrdinal("IsAdopted")),
                    reader.IsDBNull(sh) ? null : reader.GetInt32(sh)));
            }

            return Ok(list);
        }
        catch (MySqlException ex)
        {
            _logger.LogError(ex, "Staff list applications failed");
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpPost("applications")]
    public async Task<ActionResult> UpsertApplication([FromBody] StaffApplicationUpsertRequest body, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var auth = await RequireStaffAsync(cancellationToken);
        if (auth is not null) return auth;

        const string sql = """
            INSERT INTO AdoptionApplication (UserID, PetID, IsAdopted)
            VALUES (@u, @p, @ia)
            ON DUPLICATE KEY UPDATE IsAdopted = @ia
            """;

        try
        {
            await using var conn = _db.CreateConnection();
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@u", body.UserId);
            cmd.Parameters.AddWithValue("@p", body.PetId);
            cmd.Parameters.AddWithValue("@ia", body.IsAdopted);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            return NoContent();
        }
        catch (MySqlException ex)
        {
            _logger.LogError(ex, "Staff upsert application failed");
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpDelete("applications")]
    public async Task<ActionResult> DeleteApplication([FromBody] StaffApplicationDeleteRequest body, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var auth = await RequireStaffAsync(cancellationToken);
        if (auth is not null) return auth;

        const string sql = "DELETE FROM AdoptionApplication WHERE UserID = @u AND PetID = @p";

        try
        {
            await using var conn = _db.CreateConnection();
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@u", body.UserId);
            cmd.Parameters.AddWithValue("@p", body.PetId);
            var n = await cmd.ExecuteNonQueryAsync(cancellationToken);
            return n == 0 ? NotFound() : NoContent();
        }
        catch (MySqlException ex)
        {
            _logger.LogError(ex, "Staff delete application failed");
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpGet("shelters")]
    public async Task<ActionResult<IReadOnlyList<StaffShelterRowDto>>> ListShelters(CancellationToken cancellationToken)
    {
        var auth = await RequireStaffAsync(cancellationToken);
        if (auth is not null) return auth;

        const string sql = """
            SELECT
              s.ShelterID AS Id,
              s.ShelterName AS Name,
              s.ShelterAddress AS Address,
              (SELECT COUNT(*) FROM Pet p WHERE p.ShelterID = s.ShelterID) AS PetCount,
              (
                SELECT COUNT(*)
                FROM AdoptionApplication a
                INNER JOIN Pet p ON p.PetID = a.PetID
                WHERE p.ShelterID = s.ShelterID
              ) AS ApplicationCount
            FROM Shelter s
            ORDER BY s.ShelterID
            """;

        try
        {
            await using var conn = _db.CreateConnection();
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var list = new List<StaffShelterRowDto>();
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(new StaffShelterRowDto(
                    reader.GetInt32(reader.GetOrdinal("Id")),
                    reader.GetString(reader.GetOrdinal("Name")),
                    reader.GetString(reader.GetOrdinal("Address")),
                    reader.GetInt32(reader.GetOrdinal("PetCount")),
                    reader.GetInt32(reader.GetOrdinal("ApplicationCount"))));
            }

            return Ok(list);
        }
        catch (MySqlException ex)
        {
            _logger.LogError(ex, "Staff list shelters failed");
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
