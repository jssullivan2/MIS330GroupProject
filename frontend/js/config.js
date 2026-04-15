/**
 * ASP.NET Core API (see backend/Properties/launchSettings.json).
 * Use the same scheme/host/port as `dotnet run` (HTTP profile: port 5102).
 * Open the site as http://localhost:5102 — not https:// — or the browser may show "invalid response".
 * Must match where the API is listening, or fetches will fail and the UI falls back to demo data.
 */
export const API_BASE_URL = 'http://localhost:5102';
