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

    /// <summary>True when the pet belongs to the same non-null <c>ShelterID</c> as the employee.</summary>
    private static async Task<bool> EmployeePetSameShelterAsync(
        MySqlConnection conn,
        int employeeId,
        int petId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT 1
            FROM Pet p
            INNER JOIN Employee e ON e.EmployeeID = @eid
            WHERE p.PetID = @pid
              AND e.ShelterID IS NOT NULL
              AND p.ShelterID IS NOT NULL
              AND p.ShelterID = e.ShelterID
            LIMIT 1
            """;
        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@eid", employeeId);
        cmd.Parameters.AddWithValue("@pid", petId);
        return await cmd.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task<int?> GetEmployeeShelterIdAsync(
        MySqlConnection conn,
        int employeeId,
        CancellationToken cancellationToken)
    {
        await using var cmd = new MySqlCommand(
            "SELECT ShelterID FROM Employee WHERE EmployeeID = @id LIMIT 1", conn);
        cmd.Parameters.AddWithValue("@id", employeeId);
        var o = await cmd.ExecuteScalarAsync(cancellationToken);
        if (o is null or DBNull)
            return null;
        return Convert.ToInt32(o);
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
        if (!TryGetStaffId(out var listStaffId))
            return Unauthorized("Missing or invalid X-Staff-Employee-Id header.");

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
              {PetQueryFragments.SelectPhotoUrlColumn},
              CAST(NULL AS CHAR(20)) AS MyApplicationStatus
            FROM Pet p
            INNER JOIN Employee e ON e.EmployeeID = @empId
            WHERE e.ShelterID IS NOT NULL
              AND p.ShelterID IS NOT NULL
              AND p.ShelterID = e.ShelterID
            ORDER BY p.PetID
            """;

        try
        {
            await using var conn = _db.CreateConnection();
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@empId", listStaffId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var list = new List<PetDto>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var photoOrd = reader.GetOrdinal("PhotoUrl");
                var myAppOrd = reader.GetOrdinal("MyApplicationStatus");
                list.Add(new PetDto(
                    reader.GetInt32(reader.GetOrdinal("Id")),
                    reader.GetString(reader.GetOrdinal("Name")),
                    reader.GetString(reader.GetOrdinal("Species")),
                    reader.GetString(reader.GetOrdinal("Breed")),
                    reader.GetInt32(reader.GetOrdinal("AgeYears")),
                    reader.GetInt32(reader.GetOrdinal("ShelterId")),
                    reader.GetString(reader.GetOrdinal("ShelterName")),
                    reader.GetString(reader.GetOrdinal("Status")),
                    reader.IsDBNull(photoOrd) ? null : reader.GetString(photoOrd),
                    reader.IsDBNull(myAppOrd) ? null : reader.GetString(myAppOrd)));
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
        if (!TryGetStaffId(out var createStaffId))
            return Unauthorized("Missing or invalid X-Staff-Employee-Id header.");

        var empId = body.EmployeeId ?? createStaffId;
        const string sql = """
            INSERT INTO Pet (PetName, PetBreed, PetType, ShelterID, EmployeeID)
            VALUES (@name, @breed, @type, @shelterId, @empId)
            """;

        try
        {
            await using var conn = _db.CreateConnection();
            await conn.OpenAsync(cancellationToken);
            var myShelter = await GetEmployeeShelterIdAsync(conn, createStaffId, cancellationToken);
            if (myShelter is null)
                return Problem(
                    detail: "Your employee record has no shelter assigned; you cannot create pet listings.",
                    statusCode: StatusCodes.Status403Forbidden);
            if (body.ShelterId.HasValue && body.ShelterId.Value != myShelter.Value)
                return Problem(
                    detail: "You can only create pets for your own shelter.",
                    statusCode: StatusCodes.Status403Forbidden);

            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@name", body.PetName.Trim());
            cmd.Parameters.AddWithValue("@breed", string.IsNullOrWhiteSpace(body.PetBreed) ? DBNull.Value : body.PetBreed.Trim());
            cmd.Parameters.AddWithValue("@type", body.PetType);
            cmd.Parameters.AddWithValue("@shelterId", myShelter.Value);
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
        if (!TryGetStaffId(out var updateStaffId))
            return Unauthorized("Missing or invalid X-Staff-Employee-Id header.");
        var empId = body.EmployeeId ?? updateStaffId;

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
            var myShelter = await GetEmployeeShelterIdAsync(conn, updateStaffId, cancellationToken);
            if (myShelter is null)
                return Problem(
                    detail: "Your employee record has no shelter assigned; you cannot edit pet listings.",
                    statusCode: StatusCodes.Status403Forbidden);
            await using (var existsPet = new MySqlCommand("SELECT 1 FROM Pet WHERE PetID = @id LIMIT 1", conn))
            {
                existsPet.Parameters.AddWithValue("@id", petId);
                if (await existsPet.ExecuteScalarAsync(cancellationToken) is null)
                    return NotFound();
            }

            if (!await EmployeePetSameShelterAsync(conn, updateStaffId, petId, cancellationToken))
                return Problem(
                    detail: "You can only update pets from your shelter.",
                    statusCode: StatusCodes.Status403Forbidden);
            if (body.ShelterId.HasValue && body.ShelterId.Value != myShelter.Value)
                return Problem(
                    detail: "You cannot move a pet to another shelter.",
                    statusCode: StatusCodes.Status403Forbidden);

            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@petId", petId);
            cmd.Parameters.AddWithValue("@name", body.PetName.Trim());
            cmd.Parameters.AddWithValue("@breed", string.IsNullOrWhiteSpace(body.PetBreed) ? DBNull.Value : body.PetBreed.Trim());
            cmd.Parameters.AddWithValue("@type", body.PetType);
            cmd.Parameters.AddWithValue("@shelterId", myShelter.Value);
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
        if (!TryGetStaffId(out var deleteStaffId))
            return Unauthorized("Missing or invalid X-Staff-Employee-Id header.");

        try
        {
            await using var conn = _db.CreateConnection();
            await conn.OpenAsync(cancellationToken);
            await using (var existsPet = new MySqlCommand("SELECT 1 FROM Pet WHERE PetID = @id LIMIT 1", conn))
            {
                existsPet.Parameters.AddWithValue("@id", petId);
                if (await existsPet.ExecuteScalarAsync(cancellationToken) is null)
                    return NotFound();
            }

            if (!await EmployeePetSameShelterAsync(conn, deleteStaffId, petId, cancellationToken))
                return Problem(
                    detail: "You can only delete pets from your shelter.",
                    statusCode: StatusCodes.Status403Forbidden);

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
        if (!TryGetStaffId(out var staffId))
            return Unauthorized("Missing or invalid X-Staff-Employee-Id header.");

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
            INNER JOIN Employee e ON e.EmployeeID = @empId
            WHERE e.ShelterID IS NOT NULL
              AND p.ShelterID IS NOT NULL
              AND p.ShelterID = e.ShelterID
            ORDER BY a.UserID, a.PetID
            """;

        try
        {
            await using var conn = _db.CreateConnection();
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@empId", staffId);
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
        if (!TryGetStaffId(out var staffId))
            return Unauthorized("Missing or invalid X-Staff-Employee-Id header.");

        const string sql = """
            INSERT INTO AdoptionApplication (UserID, PetID, IsAdopted)
            VALUES (@u, @p, @ia)
            ON DUPLICATE KEY UPDATE IsAdopted = @ia
            """;

        try
        {
            await using var conn = _db.CreateConnection();
            await conn.OpenAsync(cancellationToken);
            if (!await EmployeePetSameShelterAsync(conn, staffId, body.PetId, cancellationToken))
                return Problem(
                    detail: "You can only create or update applications for pets assigned to your shelter.",
                    statusCode: StatusCodes.Status403Forbidden);

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
        if (!TryGetStaffId(out var staffId))
            return Unauthorized("Missing or invalid X-Staff-Employee-Id header.");

        const string sql = "DELETE FROM AdoptionApplication WHERE UserID = @u AND PetID = @p";

        try
        {
            await using var conn = _db.CreateConnection();
            await conn.OpenAsync(cancellationToken);
            if (!await EmployeePetSameShelterAsync(conn, staffId, body.PetId, cancellationToken))
                return Problem(
                    detail: "You can only delete applications for pets assigned to your shelter.",
                    statusCode: StatusCodes.Status403Forbidden);

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

    [HttpGet("profile")]
    public async Task<ActionResult<StaffProfileDto>> GetProfile(CancellationToken cancellationToken)
    {
        var auth = await RequireStaffAsync(cancellationToken);
        if (auth is not null) return auth;
        if (!TryGetStaffId(out var staffId))
            return Unauthorized("Missing or invalid X-Staff-Employee-Id header.");

        const string employeeSql = """
            SELECT EmployeeID, EmployeeName, ShelterID
            FROM Employee
            WHERE EmployeeID = @id
            LIMIT 1
            """;
        const string petsSql = """
            SELECT
              p.PetID AS PetId,
              p.PetName,
              p.PetType,
              p.PetBreed,
              p.ShelterID AS ShelterId
            FROM Pet p
            WHERE p.EmployeeID = @id
            ORDER BY p.PetID
            """;
        const string decisionsSql = """
            SELECT
              a.UserID AS UserId,
              u.UserName,
              u.UserEmail,
              a.PetID AS PetId,
              p.PetName,
              a.IsAdopted,
              CASE
                WHEN a.IsAdopted THEN 'Accepted'
                WHEN EXISTS (
                  SELECT 1
                  FROM AdoptionApplication ax
                  WHERE ax.PetID = a.PetID AND ax.IsAdopted = TRUE
                ) THEN 'Denied'
                ELSE 'Pending'
              END AS DecisionStatus
            FROM AdoptionApplication a
            INNER JOIN `User` u ON u.UserID = a.UserID
            INNER JOIN Pet p ON p.PetID = a.PetID
            INNER JOIN Employee e ON e.EmployeeID = @id
            WHERE e.ShelterID IS NOT NULL
              AND p.ShelterID = e.ShelterID
              AND (
                a.IsAdopted = TRUE
                OR EXISTS (
                  SELECT 1
                  FROM AdoptionApplication ax
                  WHERE ax.PetID = a.PetID AND ax.IsAdopted = TRUE
                )
              )
            ORDER BY a.PetID, a.UserID
            """;

        try
        {
            await using var conn = _db.CreateConnection();
            await conn.OpenAsync(cancellationToken);

            int employeeId;
            string employeeName;
            int? shelterId;
            await using (var cmd = new MySqlCommand(employeeSql, conn))
            {
                cmd.Parameters.AddWithValue("@id", staffId);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                    return NotFound();
                employeeId = reader.GetInt32(reader.GetOrdinal("EmployeeID"));
                employeeName = reader.GetString(reader.GetOrdinal("EmployeeName"));
                var shelterOrd = reader.GetOrdinal("ShelterID");
                shelterId = reader.IsDBNull(shelterOrd) ? null : reader.GetInt32(shelterOrd);
            }

            var petsAdded = new List<StaffProfilePetDto>();
            await using (var cmd = new MySqlCommand(petsSql, conn))
            {
                cmd.Parameters.AddWithValue("@id", staffId);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var shelterOrd = reader.GetOrdinal("ShelterId");
                    var breedOrd = reader.GetOrdinal("PetBreed");
                    petsAdded.Add(new StaffProfilePetDto(
                        reader.GetInt32(reader.GetOrdinal("PetId")),
                        reader.GetString(reader.GetOrdinal("PetName")),
                        reader.GetString(reader.GetOrdinal("PetType")),
                        reader.IsDBNull(breedOrd) ? null : reader.GetString(breedOrd),
                        reader.IsDBNull(shelterOrd) ? null : reader.GetInt32(shelterOrd)));
                }
            }

            var decisions = new List<StaffProfileDecisionDto>();
            await using (var cmd = new MySqlCommand(decisionsSql, conn))
            {
                cmd.Parameters.AddWithValue("@id", staffId);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    decisions.Add(new StaffProfileDecisionDto(
                        reader.GetInt32(reader.GetOrdinal("UserId")),
                        reader.GetString(reader.GetOrdinal("UserName")),
                        reader.GetString(reader.GetOrdinal("UserEmail")),
                        reader.GetInt32(reader.GetOrdinal("PetId")),
                        reader.GetString(reader.GetOrdinal("PetName")),
                        reader.GetBoolean(reader.GetOrdinal("IsAdopted")),
                        reader.GetString(reader.GetOrdinal("DecisionStatus"))));
                }
            }

            var accepted = decisions.Count((d) => d.IsAdopted);
            var deniedOrPending = decisions.Count - accepted;
            var dto = new StaffProfileDto(
                employeeId,
                employeeName,
                shelterId,
                petsAdded.Count,
                accepted,
                deniedOrPending,
                petsAdded,
                decisions);
            return Ok(dto);
        }
        catch (MySqlException ex)
        {
            _logger.LogError(ex, "Staff profile load failed");
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
