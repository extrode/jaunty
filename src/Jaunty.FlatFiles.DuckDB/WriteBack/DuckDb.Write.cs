using System.Text;

using DuckDB.NET.Data;

using Jaunty.Core;
using Jaunty.FlatFiles.DuckDB.Internals;

namespace Jaunty.FlatFiles.DuckDB;

public sealed partial class DuckDb
{
    /// <inheritdoc />
    public int Insert<T>(T entity) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(entity);

        var source = GetSourceOrThrow<T>();
        TablePromoter.EnsurePromotedToTable(_connection, source, _dialect);

        var mappings = ColumnMappingCache.Get(typeof(T));
        var columns = new StringBuilder(mappings.Count * 20);
        var values = new StringBuilder(mappings.Count * 10);
        var parameters = new List<DuckDBParameter>(mappings.Count);

        var mappingList = mappings.Values.ToList();
        for (int i = 0; i < mappingList.Count; i++)
        {
            if (i > 0) { columns.Append(", "); values.Append(", "); }
            var mapping = mappingList[i];
            columns.Append($"\"{mapping.ColumnName}\"");
            values.Append($"${i + 1}");
            parameters.Add(new DuckDBParameter { Value = mapping.Getter(entity) ?? DBNull.Value });
        }

        var sql = $"INSERT INTO \"{source.TableName}\" ({columns}) VALUES ({values})";
        var result = NonQueryExecutor.Execute(_connection, sql, parameters);
        _modified.TryAdd(typeof(T), true);
        return result;
    }

    /// <inheritdoc />
    public int Insert<T>(T entity, CommandOptions options) where T : class, new()
    {
        return Insert(entity);
    }

    /// <inheritdoc />
    public int Insert<T>(IEnumerable<T> entities) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(entities);

        var entityList = entities switch
        {
            IList<T> list => list,
            ICollection<T> collection => collection.ToList(),
            _ => entities.ToList()
        };

        if (entityList.Count == 0) return 0;

        var source = GetSourceOrThrow<T>();
        TablePromoter.EnsurePromotedToTable(_connection, source, _dialect);

        var mappings = ColumnMappingCache.Get(typeof(T));
        var columns = new StringBuilder(mappings.Count * 20);
        var mappingList = mappings.Values.ToList();
        for (int i = 0; i < mappingList.Count; i++)
        {
            if (i > 0) columns.Append(", ");
            columns.Append($"\"{mappingList[i].ColumnName}\"");
        }

        var sb = new StringBuilder(entityList.Count * mappings.Count * 10);
        var parameters = new List<DuckDBParameter>(entityList.Count * mappings.Count);
        var paramCounter = 0;

        for (int row = 0; row < entityList.Count; row++)
        {
            if (row > 0) sb.Append(", ");
            sb.Append('(');
            var entity = entityList[row];

            for (int col = 0; col < mappingList.Count; col++)
            {
                if (col > 0) sb.Append(", ");
                paramCounter++;
                sb.Append($"${paramCounter}");
                parameters.Add(new DuckDBParameter { Value = mappingList[col].Getter(entity) ?? DBNull.Value });
            }
            sb.Append(')');
        }

        var sql = $"INSERT INTO \"{source.TableName}\" ({columns}) VALUES {sb}";
        var totalInserted = NonQueryExecutor.Execute(_connection, sql, parameters);

        if (totalInserted > 0) _modified.TryAdd(typeof(T), true);
        return totalInserted;
    }

    /// <inheritdoc />
    public int Insert<T>(IEnumerable<T> entities, CommandOptions options) where T : class, new()
    {
        return Insert(entities);
    }
}