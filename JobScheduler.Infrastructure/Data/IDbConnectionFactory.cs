using System.Data.Common;

namespace JobScheduler.Infrastructure.Data;

public interface IDbConnectionFactory
{
    DbConnection Create(Database database, Intent intent);
}
