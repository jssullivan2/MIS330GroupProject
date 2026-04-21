using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using PawMatch.Api.Data;
using PawMatch.Api.Models;

namespace PawMatch.Api.Controllers;

/// <summary>Shelter directory from MySQL (<c>Shelter</c> table) with live pet/adoption metrics.</summary>
[ApiController]
[Route("api/shelters")]
public sealed class SheltersController : ControllerBase
{
    private readonly MySqlConnectionFactory _db;
    private readonly ILogger<SheltersController> _logger;

    public SheltersController(MySqlConnectionFactory db, ILogger<SheltersController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ShelterDto>>> GetAll(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
              s.ShelterID AS Id,
              s.ShelterName AS Name,
              s.ShelterAddress AS Address,
              (
                SELECT COUNT(*) FROM Pet p WHERE p.ShelterID = s.ShelterID
              ) AS PetsCount,
              (
                SELECT COUNT(*)
                FROM AdoptionApplication a
                INNER JOIN Pet p ON p.PetID = a.PetID
                WHERE p.ShelterID = s.ShelterID AND a.IsAdopted = TRUE
              ) AS CompletedAdoptions,
              (
                SELECT
                  CASE WHEN COUNT(*) = 0 THEN NULL
                  ELSE SUM(CASE WHEN a.IsAdopted THEN 1 ELSE 0 END) / COUNT(*)
                  END
                FROM AdoptionApplication a
                INNER JOIN Pet p ON p.PetID = a.PetID
                WHERE p.ShelterID = s.ShelterID
              ) AS ApprovalRate
            FROM Shelter s
            ORDER BY s.ShelterID
            """;

        try
        {
            await using var conn = _db.CreateConnection();
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var list = new List<ShelterDto>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var rateOrd = reader.GetOrdinal("ApprovalRate");
                list.Add(new ShelterDto(
                    reader.GetInt32(reader.GetOrdinal("Id")),
                    reader.GetString(reader.GetOrdinal("Name")),
                    reader.GetString(reader.GetOrdinal("Address")),
                    reader.GetInt32(reader.GetOrdinal("PetsCount")),
                    reader.GetInt32(reader.GetOrdinal("CompletedAdoptions")),
                    reader.IsDBNull(rateOrd) ? null : reader.GetDouble(rateOrd)));
            }

            return Ok(list);
        }
        catch (MySqlException ex)
        {
            _logger.LogError(ex, "Database error loading shelters");
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
