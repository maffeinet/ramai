namespace Ramai.Infrastructure.Configuration;

public sealed record RedisOptions(string Host, int Port)
{
    public string Endpoint => $"{Host}:{Port}";
}
