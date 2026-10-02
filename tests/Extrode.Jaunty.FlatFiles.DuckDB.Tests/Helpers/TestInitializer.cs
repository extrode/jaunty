using System.Runtime.CompilerServices;

using Extrode.Jaunty.Extensions.Reflection;
using Extrode.Jaunty.Configuration;

namespace Extrode.Jaunty.FlatFiles.DuckDB.Tests.Helpers;

public static class TestInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        JauntyConfig.Reconfigure(jc => jc.UseReflectionMapping());
    }
}