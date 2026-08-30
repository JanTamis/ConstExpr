using System.Text.RegularExpressions;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.RegexOptimizers;

/// <summary>
///   Optimizes <c>Regex.Count(input, pattern)</c> and <c>Regex.Count(input, pattern, options)</c>
///   by caching a compiled <see cref="Regex" /> instance as a private static readonly field and
///   replacing the static call with the equivalent instance method call.
/// </summary>
public class CountFunctionOptimizer() : BaseRegexFunctionOptimizer("Count", n => n is 2 or 3 or 4);