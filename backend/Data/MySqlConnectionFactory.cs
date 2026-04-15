using MySqlConnector;

namespace PawMatch.Api.Data;

/// <summary>
/// Creates MySQL connections using the configured connection string (direct SQL, no ORM).
/// </summary>
public sealed class MySqlConnectionFactory
{
    private readonly string _connectionString;

    public MySqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("MySql")
            ?? throw new InvalidOperationException(
                "Missing ConnectionStrings:MySql in appsettings. See README or sql/schema_and_seed.sql.");
    }

    public MySqlConnection CreateConnection() => new(_connectionString);
}
