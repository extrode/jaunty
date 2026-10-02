using System.Data;

using Extrode.Jaunty.Internals.Parameters;

namespace Extrode.Jaunty;

internal static class CommandExtensions
{
    internal static void BindParameters(this IDbCommand command, object? parameters)
    {
        if (parameters is null)
            return;

        ParameterBinder.Bind(command, parameters);
    }
}
