using System.ComponentModel;

namespace Extrode.Jaunty.Tests.Unit.Write;

public class CsvImportKillOnCancelTests
{
    [Theory]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(Win32Exception))]
    [InlineData(typeof(NotSupportedException))]
    public void KillIgnoringExit_SwallowsTheExitRaceExceptions(Type thrown)
        => CsvImportExtensions.KillIgnoringExit(() => throw (Exception)Activator.CreateInstance(thrown)!);

    [Fact]
    public void KillIgnoringExit_LetsOtherExceptionsThrough()
        => Assert.Throws<UnauthorizedAccessException>(
            () => CsvImportExtensions.KillIgnoringExit(() => throw new UnauthorizedAccessException()));
}
