using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using PawMatch.Api.Data;
using PawMatch.Api.Models;

namespace PawMatch.Api.Controllers;

[ApiController]
[Route("api/pets")]
public sealed class PetsController : ControllerBase
{
    private readonly MySqlConnectionFactory _db;
    private readonly ILogger<PetsController> _logger;

    public PetsController(MySqlConnectionFactory db, ILogger<PetsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Adopter-facing list: pets without a completed adoption (<c>IsAdopted</c> true). Status uses <c>AdoptionApplication</c>.</summary>
    /// <param name="userId">Optional adopter id; when set, each pet includes <c>myApplicationStatus</c> (<c>pending</c> = IsAdopted 0, <c>adopted</c> = IsAdopted 1).</param>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PetDto>>> GetAll([FromQuery] int? userId, CancellationToken cancellationToken)
    {
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
              CASE
                WHEN @viewerUserId IS NULL THEN CAST(NULL AS CHAR(20))
                ELSE (
                  SELECT CASE WHEN a.IsAdopted THEN 'adopted' ELSE 'pending' END
                  FROM AdoptionApplication a
                  WHERE a.PetID = p.PetID AND a.UserID = @viewerUserId
                  LIMIT 1
                )
              END AS MyApplicationStatus
            FROM Pet p
            WHERE NOT EXISTS (
              SELECT 1 FROM AdoptionApplication ax
              WHERE ax.PetID = p.PetID AND ax.IsAdopted = TRUE
            )
            ORDER BY p.PetID
            """;

        try
        {
            await using var conn = _db.CreateConnection();
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new MySqlCommand(sql, conn);
            var viewer = userId is > 0 ? userId.Value : (object?)DBNull.Value;
            cmd.Parameters.AddWithValue("@viewerUserId", viewer);
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
            _logger.LogError(ex, "Database error loading pets");
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    /// <summary>
    /// Submits an adoption request: inserts or keeps <c>AdoptionApplication</c> with <c>IsAdopted = FALSE</c> (0).
    /// Shelter staff approves by setting <c>IsAdopted = TRUE</c> (1). Pet must not already be adopted by anyone.
    /// </summary>
    [HttpPost("{petId:int}/adopt")]
    public async Task<ActionResult<PetDto>> Adopt(int petId, [FromBody] AdoptPetRequest body, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            await using var conn = _db.CreateConnection();
            await conn.OpenAsync(cancellationToken);

            const string userSql = "SELECT 1 FROM `User` WHERE UserID = @userId LIMIT 1";
            await using (var userCmd = new MySqlCommand(userSql, conn))
            {
                userCmd.Parameters.AddWithValue("@userId", body.UserId);
                var exists = await userCmd.ExecuteScalarAsync(cancellationToken);
                if (exists is null)
                    return NotFound("User was not found.");
            }

            const string petSql = "SELECT 1 FROM Pet WHERE PetID = @petId LIMIT 1";
            await using (var petCmd = new MySqlCommand(petSql, conn))
            {
                petCmd.Parameters.AddWithValue("@petId", petId);
                var petExists = await petCmd.ExecuteScalarAsync(cancellationToken);
                if (petExists is null)
                    return NotFound("Pet was not found.");
            }

            const string adoptedSql = """
                SELECT COUNT(*) FROM AdoptionApplication
                WHERE PetID = @petId AND IsAdopted = TRUE
                """;
            await using (var adoptedCmd = new MySqlCommand(adoptedSql, conn))
            {
                adoptedCmd.Parameters.AddWithValue("@petId", petId);
                var taken = Convert.ToInt32(await adoptedCmd.ExecuteScalarAsync(cancellationToken));
                if (taken > 0)
                    return Conflict("This pet is already adopted. Staff cannot accept new applications for it.");
            }

            const string mineSql = """
                SELECT IsAdopted FROM AdoptionApplication
                WHERE UserID = @userId AND PetID = @petId
                LIMIT 1
                """;
            await using (var mineCmd = new MySqlCommand(mineSql, conn))
            {
                mineCmd.Parameters.AddWithValue("@userId", body.UserId);
                mineCmd.Parameters.AddWithValue("@petId", petId);
                var mine = await mineCmd.ExecuteScalarAsync(cancellationToken);
                if (mine is bool already && already)
                    return Conflict("You have already completed adoption for this pet.");
            }

            await using var tx = await conn.BeginTransactionAsync(cancellationToken);
            const string upsertSql = """
                INSERT INTO AdoptionApplication (UserID, PetID, IsAdopted)
                VALUES (@userId, @petId, FALSE)
                ON DUPLICATE KEY UPDATE IsAdopted = IF(IsAdopted, TRUE, FALSE)
                """;
            await using (var upCmd = new MySqlCommand(upsertSql, conn, (MySqlTransaction)tx))
            {
                upCmd.Parameters.AddWithValue("@userId", body.UserId);
                upCmd.Parameters.AddWithValue("@petId", petId);
                await upCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);

            var selectSql = $"""
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
                  (
                    SELECT CASE WHEN a.IsAdopted THEN 'adopted' ELSE 'pending' END
                    FROM AdoptionApplication a
                    WHERE a.PetID = p.PetID AND a.UserID = @userId
                    LIMIT 1
                  ) AS MyApplicationStatus
                FROM Pet p
                WHERE p.PetID = @petId
                """;
            await using var selCmd = new MySqlCommand(selectSql, conn);
            selCmd.Parameters.AddWithValue("@petId", petId);
            selCmd.Parameters.AddWithValue("@userId", body.UserId);
            await using var reader = await selCmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return NotFound();

            var photoOrd = reader.GetOrdinal("PhotoUrl");
            var myAppOrd = reader.GetOrdinal("MyApplicationStatus");
            var dto = new PetDto(
                reader.GetInt32(reader.GetOrdinal("Id")),
                reader.GetString(reader.GetOrdinal("Name")),
                reader.GetString(reader.GetOrdinal("Species")),
                reader.GetString(reader.GetOrdinal("Breed")),
                reader.GetInt32(reader.GetOrdinal("AgeYears")),
                reader.GetInt32(reader.GetOrdinal("ShelterId")),
                reader.GetString(reader.GetOrdinal("ShelterName")),
                reader.GetString(reader.GetOrdinal("Status")),
                reader.IsDBNull(photoOrd) ? null : reader.GetString(photoOrd),
                reader.IsDBNull(myAppOrd) ? null : reader.GetString(myAppOrd));

            return Ok(dto);
        }
        catch (MySqlException ex)
        {
            _logger.LogError(ex, "Database error submitting adoption request for pet {PetId}", petId);
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
