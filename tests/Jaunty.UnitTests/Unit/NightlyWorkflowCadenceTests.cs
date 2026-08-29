using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using Xunit;

namespace Jaunty.Tests.Unit;

/// <summary>
/// The mutation tier is gated to the weekly cron by a literal string comparison against
/// <c>github.event.schedule</c>. That coupling fails in the quiet direction: edit the cron and
/// leave the condition, and the job stops running entirely - no error, no skipped-job badge on the
/// weekday runs it was already absent from, just a report that never appears again. Nothing else
/// in the repo reads these two strings together.
/// </summary>
public class NightlyWorkflowCadenceTests
{
    // 05:00, not 03:00: jauntyq's nightly fires at '0 3 * * *' and the two were colliding.
    private const string WeekdayCron = "0 5 * * 1-6";
    private const string WeeklyCron = "0 5 * * 0";

    [Fact]
    public void TheNightlyWorkflowDeclaresBothSchedules()
    {
        string workflow = ReadNightlyWorkflow();

        Assert.Contains("cron: '" + WeekdayCron + "'", workflow, StringComparison.Ordinal);
        Assert.Contains("cron: '" + WeeklyCron + "'", workflow, StringComparison.Ordinal);
    }

    /// <summary>
    /// Two schedule entries at the same minute fire as two independent runs rather than one, so a
    /// weekday cron that still included Sunday would run the full suite twice that day.
    /// </summary>
    [Fact]
    public void TheWeekdayScheduleExcludesTheDayTheWeeklyScheduleCovers()
    {
        List<string> crons = DeclaredCrons();

        Assert.Equal(new[] { WeekdayCron, WeeklyCron }, crons);
    }

    [Fact]
    public void TheMutationJobIsGatedToTheWeeklyScheduleAndManualRuns()
    {
        string condition = MutationJobCondition();

        Assert.Contains("github.event.schedule == '" + WeeklyCron + "'", condition, StringComparison.Ordinal);
        Assert.Contains("github.event_name == 'workflow_dispatch'", condition, StringComparison.Ordinal);
    }

    /// <summary>
    /// The gate is meant to hold the mutation tier back and nothing else. A schedule condition that
    /// spread to <c>full-suite</c> or <c>fuzz</c> would quietly halve what the nightly covers.
    /// </summary>
    [Fact]
    public void NoOtherJobCarriesAScheduleCondition()
    {
        List<string> gated = new();

        foreach (Match match in Regex.Matches(
                     ReadNightlyWorkflow(),
                     @"(?m)^  (?<job>[a-z0-9-]+):$\n(?:^    (?!if:).*\n)*^    if: (?<condition>.+)$"))
        {
            if (match.Groups["condition"].Value.IndexOf("github.event", StringComparison.Ordinal) >= 0)
                gated.Add(match.Groups["job"].Value);
        }

        Assert.Equal(new[] { "mutation" }, gated);
    }

    private static List<string> DeclaredCrons()
    {
        List<string> crons = new();

        foreach (Match match in Regex.Matches(ReadNightlyWorkflow(), @"cron:\s*'([^']+)'"))
            crons.Add(match.Groups[1].Value);

        return crons;
    }

    private static string MutationJobCondition()
    {
        Match job = Regex.Match(ReadNightlyWorkflow(), @"(?m)^  mutation:$\n(?:^    .*\n)+");
        Assert.True(job.Success, "No 'mutation' job was found in nightly.yml.");

        Match condition = Regex.Match(job.Value, @"(?m)^    if: (.+)$");
        Assert.True(condition.Success,
            "The mutation job carries no 'if:', so it runs on every nightly schedule again. It is " +
            "2,364 mutants and not a gate; it belongs on the weekly cron only.");

        return condition.Groups[1].Value;
    }

    private static string ReadNightlyWorkflow()
    {
        string path = Path.Combine(LocateRepositoryRoot().FullName, ".github", "workflows", "nightly.yml");

        Assert.True(File.Exists(path), "'" + path + "' is missing; this suite cannot pass vacuously.");

        // The file is CRLF. In multiline mode '$' matches before '\n' and not before '\r', so every
        // line-anchored pattern below silently matches nothing unless the endings are normalised.
        return File.ReadAllText(path).Replace("\r\n", "\n");
    }

    private static DirectoryInfo LocateRepositoryRoot()
    {
        DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Jaunty.slnx")))
                return dir;

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate Jaunty.slnx walking up from '" + AppContext.BaseDirectory + "'.");
    }
}
