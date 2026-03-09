using System.Data;

using Jaunty.Internals.Parameters;

namespace Jaunty;

internal static class CommandExtensions
{
    internal static void BindParameters(this IDbCommand command, object? parameters)
    {
        if (parameters is null)
            return;

        ParameterBinder.Bind(command, parameters);
    }
}
