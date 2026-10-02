namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Compiler-required marker for <see langword="init"/> accessors, which the netstandard2.0
    /// reference assemblies predate. Declaring it here lets this project use records and
    /// <c>init</c>-only members; the type is <see langword="internal"/>, so it cannot collide with
    /// a consumer's own copy.
    /// </summary>
    internal static class IsExternalInit
    {
    }
}
