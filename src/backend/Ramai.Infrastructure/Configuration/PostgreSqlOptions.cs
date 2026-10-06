using Npgsql;

namespace Ramai.Infrastructure.Configuration;

public sealed record PostgreSqlOptions(
    string Host,
    int Port,
    string Database,
    string Username,
    string Password)
{
    public string ToConnectionString()
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = Host,
            Port = Port,
            Database = Database,
            Username = Username,
            Password = Password,
            ApplicationName = "RAMAI",
            IncludeErrorDetail = false
        };

        return builder.ConnectionString;
    }
}
