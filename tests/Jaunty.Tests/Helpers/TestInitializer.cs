using System.Runtime.CompilerServices;

using Jaunty.Configuration;
using Jaunty.Extensions.Npgsql;
using Jaunty.Extensions.Reflection;

namespace Jaunty.Tests.Helpers;

public static class TestInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        JauntyReflectionExtensions.UseReflectionMapping();
        SpecialTypeMappers.Register();

        // The PostgreSQL client-side COPY path used to find Npgsql by reflection; it now needs a
        // registered provider. Without this the live [Postgres] import tests would take the
        // server-side fallback - or, since the guard makes that loud, throw.
        JauntyNpgsql.Use();
    }
}