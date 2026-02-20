using System.Reflection;

namespace Jaunty;

public static partial class Jaunty
{
    static Jaunty()
    {
        TryEnableReflectionMapping();
    }

    private static void TryEnableReflectionMapping()
    {
        try
        {
            // Auto-discover and enable reflection mapping if the extension assembly is present
            var assembly = Assembly.Load(new AssemblyName("Jaunty.Extensions.Reflection"));
            var type = assembly.GetType("Jaunty.Extensions.Reflection.JauntyReflectionExtensions");
            var method = type?.GetMethod("UseReflectionMapping", BindingFlags.Public | BindingFlags.Static);
            method?.Invoke(null, null);
        }
        catch
        {
            // Extension not present, which is fine for 100% Source Gen users
        }
    }
}
