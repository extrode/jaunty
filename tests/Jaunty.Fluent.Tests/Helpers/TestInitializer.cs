using System.Runtime.CompilerServices;

using Jaunty.Extensions.Reflection;

namespace Jaunty.Fluent.Tests.Helpers;

public static class TestInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        JauntyReflectionExtensions.UseReflectionMapping();
    }
}