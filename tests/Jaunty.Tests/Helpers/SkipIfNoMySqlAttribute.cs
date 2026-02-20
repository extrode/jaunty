using System;
using Xunit;

namespace Jaunty.Tests.Helpers;

/// <summary>
/// Marks a test as a Fact that should be skipped when MySQL is not configured.
/// Tests will be skipped if the JAUNTY_TEST_MYSQL environment variable is not set
/// and no ConnectionStrings:MySql is found in appsettings.json.
/// </summary>
public sealed class SkipIfNoMySqlFactAttribute : FactAttribute
{
    public SkipIfNoMySqlFactAttribute()
    {
        if (!TestConfiguration.HasMySql)
        {
            Skip = "MySQL not configured. Set JAUNTY_TEST_MYSQL environment variable or add appsettings.json with ConnectionStrings:MySql.";
        }
    }
}

/// <summary>
/// Marks a test as a Theory that should be skipped when MySQL is not configured.
/// </summary>
public sealed class SkipIfNoMySqlTheoryAttribute : TheoryAttribute
{
    public SkipIfNoMySqlTheoryAttribute()
    {
        if (!TestConfiguration.HasMySql)
        {
            Skip = "MySQL not configured. Set JAUNTY_TEST_MYSQL environment variable or add appsettings.json with ConnectionStrings:MySql.";
        }
    }
}
