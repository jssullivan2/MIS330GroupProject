using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using PawMatch.Api.Data;
using PawMatch.Api.Models;

namespace PawMatch.Api.Controllers;

/// <summary>Dashboard summary sourced from MySQL tables.</summary>
[ApiController]
[Route("api/stats")]
public sealed class StatsController : ControllerBase
{
    private readonly MySqlConnectionFactory _db;
    private readonly ILogger<StatsController> _logger;

    public StatsController(MySqlConnectionFactory db, ILogger<StatsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<SummaryDto>> GetSummary(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
              (SELECT COUNT(*) FROM Pet) AS TotalPetsListed,
              (
                SELECT COUNT(*)
                FROM Pet p
                WHERE NOT EXISTS (
                  SELECT 1 FROM AdoptionApplication a
                  WHERE a.PetID = p.PetID AND a.IsAdopted = TRUE
                )
              ) AS AvailableNow,
              (SELECT COUNT(*) FROM AdoptionApplication) AS ApplicationsThisMonth,
              (SELECT COUNT(*) FROM `User`) AS NewUsersThisMonth
            """;

        try
        {
            await using var conn = _db.CreateConnection();
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return Ok(new SummaryDto(0, 0, 0, 0));
            }

            var dto = new SummaryDto(
                reader.GetInt32(reader.GetOrdinal("TotalPetsListed")),
                reader.GetInt32(reader.GetOrdinal("AvailableNow")),
                reader.GetInt32(reader.GetOrdinal("ApplicationsThisMonth")),
                reader.GetInt32(reader.GetOrdinal("NewUsersThisMonth")));

            return Ok(dto);
        }
        catch (MySqlException ex)
        {
            _logger.LogError(ex, "Database error loading dashboard summary");
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
