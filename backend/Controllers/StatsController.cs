using Microsoft.AspNetCore.Mvc;
using PawMatch.Api.Models;

namespace PawMatch.Api.Controllers;

/// <summary>
/// Dashboard summary is not read from MySQL — only Pet + User data are pulled from the database.
/// The SPA uses local mock values for this card row; this endpoint remains for compatibility / testing.
/// </summary>
[ApiController]
[Route("api/stats")]
public sealed class StatsController : ControllerBase
{
    private static readonly SummaryDto StaticSummary = new(
        TotalPetsListed: 0,
        AvailableNow: 0,
        ApplicationsThisMonth: 0,
        NewUsersThisMonth: 0);

    [HttpGet("summary")]
    public ActionResult<SummaryDto> GetSummary() => Ok(StaticSummary);
}
