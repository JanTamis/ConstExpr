using System.Text.RegularExpressions;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.RegexOptimizers;

/// <summary>
///   Optimizes <c>Regex.IsMatch(input, pattern)</c> and <c>Regex.IsMatch(input, pattern, options)</c>.
///   <para>
///     When the constant pattern is really just a literal string - optionally anchored with <c>^</c> -
///     the call is lowered to the equivalent ordinal <c>string.StartsWith</c>/<c>string.Contains</c>,
///     which drops the regex engine <em>and</em> the hoisted <c>Regex</c> field entirely. The emitted
///     call then feeds the existing StartsWith/Contains string optimizers for a further round.
///   </para>
///   <para>
///     Otherwise it falls back to the shared behaviour: cache a compiled <see cref="Regex" /> instance
///     as a private static readonly field and call the instance method on it.
///   </para>
/// </summary>
public class IsMatchFunctionOptimizer() : BaseRegexFunctionOptimizer("IsMatch", n => n is 2 or 3 or 4);