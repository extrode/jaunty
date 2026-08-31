using Jaunty.Scaffolding.CodeGeneration;

namespace Jaunty.Scaffolding.Tests.CodeGeneration;

/// <summary>
/// AUD-R35-035. The <c>-ies -&gt; -y</c> rule ran unconditionally and ahead of every closed-class
/// check, so it truncated the class of nouns whose singular already ends in <c>-ie</c> and whose
/// plural merely adds <c>-s</c>: Movies to "Movy", Cookies to "Cooky", Series to "Sery". Same shape
/// as the defects R24 fixed for <c>-ves</c> and AUD-R26 for <c>-ses</c>/<c>-ches</c>/<c>-zes</c>,
/// and resolved the same way - enumerate the closed class, let the rest fall through. Singularize
/// is on by default for every scaffolded table.
/// </summary>
public class NamingHelperIePluralTests
{
    [Theory]
    [InlineData("Movies", "Movie")]
    [InlineData("Cookies", "Cookie")]
    [InlineData("Calories", "Calorie")]
    [InlineData("Zombies", "Zombie")]
    [InlineData("Rookies", "Rookie")]
    [InlineData("Brownies", "Brownie")]
    [InlineData("Genies", "Genie")]
    [InlineData("Ties", "Tie")]
    [InlineData("Pies", "Pie")]
    [InlineData("Lies", "Lie")]
    [InlineData("Dies", "Die")]
    public void AnIeSingularKeepsItsE(string plural, string expected) =>
        Assert.Equal(expected, NamingHelper.Singularize(plural));

    /// <summary>
    /// Their own plurals, which no shape rule can recognise.
    /// </summary>
    [Theory]
    [InlineData("Series")]
    [InlineData("Species")]
    public void AnUnchangingPluralIsLeftAlone(string word) =>
        Assert.Equal(word, NamingHelper.Singularize(word));

    /// <summary>
    /// The large class the rule exists for, which must be untouched by the exception table.
    /// </summary>
    [Theory]
    [InlineData("Categories", "Category")]
    [InlineData("Companies", "Company")]
    [InlineData("Entities", "Entity")]
    [InlineData("Cities", "City")]
    [InlineData("Countries", "Country")]
    [InlineData("Properties", "Property")]
    public void AYStemStillLosesItsIes(string plural, string expected) =>
        Assert.Equal(expected, NamingHelper.Singularize(plural));

    /// <summary>
    /// The class-name path is what a scaffolded table actually goes through, and singularization is
    /// on by default there.
    /// </summary>
    [Fact]
    public void AMoviesTableScaffoldsAMovieClass() =>
        Assert.Equal("Movie", NamingHelper.ToClassName("movies", singularize: true, null, null));

    /// <summary>
    /// The closed class is matched at a PascalCase segment boundary, so a compound name works and a
    /// word that merely ends in the same letters does not get rewritten.
    /// </summary>
    [Theory]
    [InlineData("UserCookies", "UserCookie")]
    [InlineData("FeatureMovies", "FeatureMovie")]
    public void ACompoundNameSingularizesItsLastSegment(string plural, string expected) =>
        Assert.Equal(expected, NamingHelper.Singularize(plural));
}
