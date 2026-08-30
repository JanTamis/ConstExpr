using System.Text.RegularExpressions;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.RegexOptimizers;

/// <summary>
///   Optimizes <c>Regex.EnumerateMatches(input, pattern)</c>,
///   <c>Regex.EnumerateMatches(input, pattern, options)</c>, and
///   <c>Regex.EnumerateMatches(input, pattern, options, timeout)</c>
///   by caching a compiled <see cref="Regex" /> instance as a private static readonly field and
///   replacing the static call with the equivalent instance method call.
///   Returns a <c>ValueMatchEnumerator</c> (zero-alloc struct enumerator).
/// </summary>
public class EnumerateMatchesFunctionOptimizer() : BaseRegexFunctionOptimizer("EnumerateMatches", n => n is 2 or 3 or 4);