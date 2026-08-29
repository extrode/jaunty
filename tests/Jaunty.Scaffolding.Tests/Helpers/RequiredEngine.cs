using Xunit;

namespace Jaunty.Scaffolding.Tests.Helpers;

/// <summary>
/// Turns "this engine is not here" into a skip, or into a failure when CI has declared the engine
/// required.
/// </summary>
/// <remarks>
/// The schema-reader suites have always probed for reachability - <c>OpenOrSkip</c> opens a
/// connection and calls <see cref="Assert.Skip(string)"/> when it throws - which is the behaviour
/// <c>Jaunty.Tests</c> adopted on 2026-08-30. It leaves one gap the other project has too: a
/// service container that fails to start in CI skips silently and reports a green leg that tested
/// nothing. Setting the matching variable closes it.
/// <para>
/// The mirror of <c>Jaunty.Tests.Helpers.Dialects.DialectReachability</c>, duplicated rather than
/// shared because the two test projects reference no common assembly. The variable names are the
/// contract between them and must stay identical; <c>DialectGateTests</c> pins the parsing rules
/// on the other side.
/// </para>
/// </remarks>
internal static class RequiredEngine
{
    public const string SqlServer = "JAUNTY_REQUIRE_SQLSERVER";

    public const string PostgreSql = "JAUNTY_REQUIRE_POSTGRESQL";

    public const string MySql = "JAUNTY_REQUIRE_MYSQL";

    /// <summary>
    /// Skips the calling test, unless <paramref name="variable"/> says the engine had to be there.
    /// </summary>
    /// <remarks>
    /// Throwing is safe here in a way it is not in a data attribute: this runs inside the test
    /// body, so it fails one test rather than collapsing every row of the method.
    /// </remarks>
    public static void SkipOrFail(string variable, string reason)
    {
        if (IsRequired(variable))
        {
            throw new InvalidOperationException(
                $"{reason}{Environment.NewLine}" +
                $"This is a failure rather than a skip because {variable} is set. " +
                $"Unset it to let these tests skip when the engine is absent.");
        }

        Assert.Skip(reason);
    }

    /// <summary>
    /// Whether <paramref name="variable"/> asks for this engine to be present. "0", "false" and
    /// "no" read as not-required so a CI job can turn one engine off without deleting the line.
    /// </summary>
    public static bool IsRequired(string variable)
    {
        string? value = Environment.GetEnvironmentVariable(variable);

        if (string.IsNullOrWhiteSpace(value))
            return false;

        string trimmed = value!.Trim();

        return !trimmed.Equals("0", StringComparison.OrdinalIgnoreCase)
            && !trimmed.Equals("false", StringComparison.OrdinalIgnoreCase)
            && !trimmed.Equals("no", StringComparison.OrdinalIgnoreCase);
    }
}
