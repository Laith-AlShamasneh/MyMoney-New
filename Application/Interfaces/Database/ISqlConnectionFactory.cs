using System.Data;

namespace Application.Interfaces.Database;

public interface ISqlConnectionFactory
{
    IDbConnection CreateConnection();

    Task<IDbConnection> CreateConnectionAsync(CancellationToken ct = default);
}
