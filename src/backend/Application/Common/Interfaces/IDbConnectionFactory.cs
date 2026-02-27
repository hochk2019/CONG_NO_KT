using System.Data.Common;

namespace CongNoGolden.Application.Common.Interfaces;

public interface IDbConnectionFactory
{
    DbConnection Create();

    DbConnection CreateRead()
    {
        return Create();
    }

    DbConnection CreateWrite()
    {
        return Create();
    }
}
