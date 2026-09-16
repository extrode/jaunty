using System.Runtime.CompilerServices;

using Extrode.Jaunty.Configuration;
using Extrode.Jaunty.Extensions.Npgsql;
using Extrode.Jaunty.Extensions.Reflection;

namespace Extrode.Jaunty.Tests.Helpers;

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