using Jaunty.Scaffolding.CodeGeneration;

namespace Jaunty.Scaffolding.Tests.CodeGeneration;

/// <summary>
/// AUD-R35-264. <c>IrregularPlurals</c> was matched only as a whole word, while the three closed
/// classes are matched as a trailing PascalCase segment (OrderStatuses to OrderStatus, BookShelves
/// to BookShelf). Two mechanisms in the same method therefore disagreed on whether a plural counts
/// in compound position: OrderIndices fell through to the generic <c>-s</c> rule as "OrderIndice",
/// and AuditCriteria and CustomerChildren matched no rule at all.
/// </summary>
public class NamingHelperCompoundIrregularTests
{
    [Theory]
    [InlineData("OrderIndices", "OrderIndex")]
    [InlineData("AuditCriteria", "AuditCriterion")]
    [InlineData("CustomerChildren", "CustomerChild")]
    [InlineData("StaffPeople", "StaffPerson")]
    [InlineData("SparePartsMen", "SparePartsMan")]
    [InlineData("LabMice", "LabMouse")]
    [InlineData("BloodAnalyses", "BloodAnalysis")]
    [InlineData("SparseMatrices", "SparseMatrix")]
    public void AnIrregularPluralIsRecognisedInCompoundPosition(string plural, string expected) =>
        Assert.Equal(expected, NamingHelper.Singularize(plural));

    /// <summary>
    /// The whole-word answers are unchanged; the compound pass only ever sees what the dictionary
    /// lookup did not already answer.
    /// </summary>
    [Theory]
    [InlineData("People", "Person")]
    [InlineData("Children", "Child")]
    [InlineData("Men", "Man")]
    [InlineData("Women", "Woman")]
    [InlineData("Indices", "Index")]
    public void ABareIrregularIsUnchanged(string plural, string expected) =>
        Assert.Equal(expected, NamingHelper.Singularize(plural));

    /// <summary>
    /// The segment boundary must be a capital, so an ordinary word that merely ends in one of these
    /// letter sequences is left to the shape rules - the same guard <c>TrySingularizeBySuffix</c>
    /// already applied for "Olives".
    /// </summary>
    [Theory]
    [InlineData("Women", "Woman")]
    [InlineData("Specimen", "Specimen")]
    [InlineData("Metadata", "Metadata")]
    [InlineData("Regimen", "Regimen")]
    public void ALowercaseBoundaryDoesNotMatch(string word, string expected) =>
        Assert.Equal(expected, NamingHelper.Singularize(word));
}
