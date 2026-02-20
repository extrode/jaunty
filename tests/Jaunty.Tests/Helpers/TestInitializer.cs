using System.Runtime.CompilerServices;
using Jaunty.Configuration;
using Jaunty.Extensions.Reflection;

namespace Jaunty.Tests.Helpers;

public static class TestInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        JauntyReflectionExtensions.UseReflectionMapping();
    }
}
