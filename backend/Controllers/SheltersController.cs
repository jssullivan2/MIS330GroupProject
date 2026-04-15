using Microsoft.AspNetCore.Mvc;
using PawMatch.Api.Models;

namespace PawMatch.Api.Controllers;

/// <summary>
/// Static shelter list for the UI filter — not loaded from MySQL (only Pet + User are read from the database per project setup).
/// </summary>
[ApiController]
[Route("api/shelters")]
public sealed class SheltersController : ControllerBase
{
    private static readonly IReadOnlyList<ShelterDto> StaticShelters =
    [
        new(1, "River City Rescue", "Columbus, OH (demo)", "", 0, 0, null),
        new(2, "Northside Shelter", "Cleveland, OH (demo)", "", 0, 0, null),
        new(3, "Small Paws Haven", "Cincinnati, OH (demo)", "", 0, 0, null),
    ];

    [HttpGet]
    public ActionResult<IReadOnlyList<ShelterDto>> GetAll() => Ok(StaticShelters);
}
