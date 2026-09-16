using System.Runtime.CompilerServices;

using Extrode.Jaunty.Extensions.Reflection;

namespace Extrode.Jaunty.FlatFiles.DuckDB.Tests.Helpers;

public static class TestInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        JauntyReflectionExtensions.UseReflectionMapping();
    }
}