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

    /// <summary>Returns rows from <c>Pet</c> only (no <c>Shelter</c> join). Status uses <c>AdoptionApplication</c> for adoption workflow.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PetDto>>> GetAll(CancellationToken cancellationToken)
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
            _logger.LogError(ex, "Database error loading pets");
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    /// <summary>Sets <c>AdoptionApplication.IsAdopted</c> for this user/pet; pet must not already be adopted.</summary>
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
                    return Conflict("This pet is not available for adoption.");
            }

            await using var tx = await conn.BeginTransactionAsync(cancellationToken);
            const string upsertSql = """
                INSERT INTO AdoptionApplication (UserID, PetID, IsAdopted)
                VALUES (@userId, @petId, TRUE)
                ON DUPLICATE KEY UPDATE IsAdopted = TRUE
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
                  {PetQueryFragments.SelectPhotoUrlColumn}
                FROM Pet p
                WHERE p.PetID = @petId
                """;
            await using var selCmd = new MySqlCommand(selectSql, conn);
            selCmd.Parameters.AddWithValue("@petId", petId);
            await using var reader = await selCmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return NotFound();

            var photoOrd = reader.GetOrdinal("PhotoUrl");
            var dto = new PetDto(
                reader.GetInt32(reader.GetOrdinal("Id")),
                reader.GetString(reader.GetOrdinal("Name")),
                reader.GetString(reader.GetOrdinal("Species")),
                reader.GetString(reader.GetOrdinal("Breed")),
                reader.GetInt32(reader.GetOrdinal("AgeYears")),
                reader.GetInt32(reader.GetOrdinal("ShelterId")),
                reader.GetString(reader.GetOrdinal("ShelterName")),
                reader.GetString(reader.GetOrdinal("Status")),
                reader.IsDBNull(photoOrd) ? null : reader.GetString(photoOrd));

            return Ok(dto);
        }
        catch (MySqlException ex)
        {
            _logger.LogError(ex, "Database error adopting pet {PetId}", petId);
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
