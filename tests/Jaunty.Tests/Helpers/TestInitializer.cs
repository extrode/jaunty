using System.Runtime.CompilerServices;
using Jaunty.Configuration;
using Jaunty.Extensions.Reflection;

namespace Jaunty.Tests.Helpers;

public static class TestInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        // Force registration by direct call
        JauntyReflectionExtensions.UseReflectionMapping();
    }
}

#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    internal sealed class ModuleInitializerAttribute : Attribute { }
}
#endif
