using System.Data;

namespace Application.Interfaces.Database;

public interface ISqlConnectionFactory
{
    IDbConnection CreateConnection();
}
