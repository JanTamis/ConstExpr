using System.Text.RegularExpressions;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.RegexOptimizers;

/// <summary>
///   Optimizes <c>Regex.Match(input, pattern)</c> and <c>Regex.Match(input, pattern, options)</c>
///   by caching a compiled <see cref="Regex" /> instance as a private static readonly field and
///   replacing the static call with the equivalent instance method call.
/// </summary>
public class MatchFunctionOptimizer() : BaseRegexFunctionOptimizer("Match", n => n is 2 or 3 or 4);