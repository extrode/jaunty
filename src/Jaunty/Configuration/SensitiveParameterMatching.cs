namespace Jaunty.Configuration;

/// <summary>
/// How a parameter name is compared against <c>LoggingConfiguration.SensitiveParameterNames</c>
/// in the optional Extrode.Jaunty.Extensions.Logging package.
/// </summary>
public enum SensitiveParameterMatching
{
    /// <summary>
    /// A parameter is sensitive when a configured name appears in it as a whole word - ignoring case,
    /// any provider prefix (<c>@</c>, <c>:</c>, <c>?</c>, <c>$</c>), and treating <c>_</c>, <c>-</c>,
    /// <c>.</c> and camel-case humps as word boundaries. This is the default.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AUD-R26 (batch 4, medium/security). Matching used to be exact equality, so the seeded name
    /// <c>Password</c> masked a parameter called exactly <c>Password</c> and nothing else - not
    /// <c>NewPassword</c>, <c>PasswordHash</c> or <c>UserPassword</c>; <c>Token</c> did not mask
    /// <c>AccessToken</c> or <c>RefreshToken</c>; <c>ApiKey</c> did not mask <c>Api_Key</c>. Since
    /// <c>LogParameters</c> defaults to <see langword="true"/>, credentials under those very ordinary
    /// spellings were written to the log in clear at Information level, out of the box.
    /// </para>
    /// <para>
    /// Whole-word rather than plain substring, because substring over-masks in a way that makes the
    /// log useless and teaches people to switch masking off: "Token" occurs inside
    /// <c>TokenizerVersion</c>, "Secret" inside <c>SecretariatId</c>. Word boundaries keep every real
    /// hit and drop that class of false positive. A multi-word configured name matches as a
    /// consecutive run, so <c>ApiKey</c> matches <c>api_key</c> and <c>apiKeyValue</c> but not a
    /// parameter that merely happens to contain both words apart.
    /// </para>
    /// </remarks>
    WholeWord = 0,

    /// <summary>
    /// A parameter is sensitive only when its name equals a configured name exactly, ignoring case,
    /// any provider prefix and separator style. This was the behaviour before AUD-R26.
    /// </summary>
    /// <remarks>
    /// Available for callers who chose exact matching deliberately. Prefer <see cref="WholeWord"/>:
    /// under-masking a credential is a far worse failure than over-masking a column.
    /// </remarks>
    Exact = 1,
}
