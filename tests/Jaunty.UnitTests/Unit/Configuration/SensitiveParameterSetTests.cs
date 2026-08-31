using Jaunty.Configuration;

using Xunit;

namespace Jaunty.Tests.Unit.Configuration;

/// <summary>
/// AUD-R35-139 and AUD-R35-140. <see cref="LoggingConfiguration.SensitiveParameterNames"/> handed out
/// the live, non-thread-safe <see cref="HashSet{T}"/> that <c>IsSensitiveParameter</c> enumerated,
/// re-splitting every configured name into words on every call. A configuration is normally a DI
/// singleton, so an <c>Add</c> from startup racing a logged command threw out of the <c>foreach</c>.
/// </summary>
public class SensitiveParameterSetTests
{
    [Fact]
    public void TheSeededNamesStillMask_OnWordBoundaries()
    {
        var configuration = new LoggingConfiguration();

        Assert.True(configuration.IsSensitiveParameter("@NewPassword"));
        Assert.True(configuration.IsSensitiveParameter("access_token"));
        Assert.True(configuration.IsSensitiveParameter("apiKey"));
        Assert.False(configuration.IsSensitiveParameter("TokenizerVersion"));
        Assert.False(configuration.IsSensitiveParameter("OrderId"));
    }

    [Fact]
    public void ANameAddedThroughTheSet_TakesEffectImmediately()
    {
        var configuration = new LoggingConfiguration();

        Assert.False(configuration.IsSensitiveParameter("EmployeeSsnValue"));

        configuration.SensitiveParameterNames.Add("Ssn");

        Assert.True(configuration.IsSensitiveParameter("EmployeeSsnValue"));
    }

    [Fact]
    public void ANameRemovedThroughTheSet_StopsMasking()
    {
        var configuration = new LoggingConfiguration();

        Assert.True(configuration.SensitiveParameterNames.Remove("Token"));
        Assert.False(configuration.IsSensitiveParameter("AccessToken"));
        Assert.True(configuration.IsSensitiveParameter("@Password"));
    }

    [Fact]
    public void ClearingTheSet_MasksNothing()
    {
        var configuration = new LoggingConfiguration();

        configuration.SensitiveParameterNames.Clear();

        Assert.Empty(configuration.SensitiveParameterNames);
        Assert.False(configuration.IsSensitiveParameter("Password"));
    }

    [Fact]
    public void TheSetStillBehavesLikeASet()
    {
        var configuration = new LoggingConfiguration();
        ISet<string> names = configuration.SensitiveParameterNames;

        Assert.Equal(7, names.Count);
        Assert.True(names.Contains("password"));
        Assert.False(names.Add("PASSWORD"));

        names.UnionWith(["Pin", "Otp"]);
        Assert.True(names.IsSupersetOf(["Pin", "Otp", "Token"]));
        Assert.True(names.Overlaps(["Otp", "OrderId"]));

        var copy = new string[names.Count];
        names.CopyTo(copy, 0);
        Assert.Contains("Pin", copy);
    }

    /// <summary>
    /// The failure the finding names: enumerating the exposed set while another thread mutates it
    /// used to throw <see cref="InvalidOperationException"/> out of whichever side lost the race.
    /// Enumeration now walks a snapshot, so neither side sees the other mid-write.
    /// </summary>
    [Fact]
    public void MutatingWhileLoggingDoesNotThrow()
    {
        var configuration = new LoggingConfiguration();
        using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));

        Task writer = Task.Run(() =>
        {
            int i = 0;
            while (!stop.IsCancellationRequested)
            {
                configuration.SensitiveParameterNames.Add("Name" + i++);
                if (i > 200)
                {
                    configuration.SensitiveParameterNames.Clear();
                    i = 0;
                }
            }
        });

        Task reader = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                configuration.IsSensitiveParameter("@CustomerPasswordHash");
                foreach (string name in configuration.SensitiveParameterNames)
                    _ = name.Length;
            }
        });

        Task.WaitAll(writer, reader);

        Assert.Equal(TaskStatus.RanToCompletion, writer.Status);
        Assert.Equal(TaskStatus.RanToCompletion, reader.Status);
    }
}
