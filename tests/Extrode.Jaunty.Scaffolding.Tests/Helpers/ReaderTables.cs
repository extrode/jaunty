using System.Data;

namespace Extrode.Jaunty.Scaffolding.Tests.Helpers;

internal static class ReaderTables
{
    public static DataTable Build(params (string Name, Type Type)[] columns)
    {
        var table = new DataTable();
        foreach ((string name, Type type) in columns)
            table.Columns.Add(new DataColumn(name, type) { AllowDBNull = true });
        return table;
    }

    public static DataTable With(this DataTable table, params object?[][] rows)
    {
        foreach (object?[] row in rows)
            table.Rows.Add(row.Select(v => v ?? DBNull.Value).ToArray());
        return table;
    }
}
