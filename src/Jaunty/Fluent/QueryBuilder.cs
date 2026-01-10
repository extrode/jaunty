using System.Data;

namespace Jaunty;

public static partial class Jaunty
{
    extension(IDbConnection connection)
    {
        public IQueryable<T> From<T>(string tableName = null) where T : new()
        {
            // Placeholder for future fluent API implementation
            throw new NotImplementedException("Fluent API is not yet implemented");
        }
    }
}
