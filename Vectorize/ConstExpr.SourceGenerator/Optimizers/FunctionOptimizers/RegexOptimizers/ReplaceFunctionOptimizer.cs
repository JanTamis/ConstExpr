using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.RegexOptimizers;

/// <summary>
///   Optimizes static <c>Regex.Replace</c> overloads by caching a compiled <see cref="Regex" />
///   instance as a private static readonly field when the <c>pattern</c> (and optional
///   <c>options</c>) argument is a compile-time constant.
///   <list type="bullet">
///     <item>
///       <c>Regex.Replace(input, pattern, replacement)</c>
///     </item>
///     <item>
///       <c>Regex.Replace(input, pattern, replacement, options)</c>
///     </item>
///     <item>
///       <c>Regex.Replace(input, pattern, evaluator)</c>
///     </item>
///     <item>
///       <c>Regex.Replace(input, pattern, evaluator, options)</c>
///     </item>
///   </list>
///   The <c>input</c> and <c>replacement</c>/<c>evaluator</c> arguments may be runtime values.
///   Unlike every other static <c>Regex</c> overload the replacement sits *between* the pattern and
///   the options, so the constructor arguments cannot simply be "everything after the input".
/// </summary>
public class ReplaceFunctionOptimizer() : BaseRegexFunctionOptimizer("Replace", n => n is 3 or 4 or 5)
{
	protected override bool TryOptimizeRegex(FunctionOptimizerContext context, [NotNullWhen(true)] out SyntaxNode? result)
	{
		result = null;

		context.Usings.Add("System.Text.RegularExpressions");

		// Pattern (param[1]) must be a compile-time constant.
		if (!TryGetLiteralValue(context.VisitedParameters[1], context, out _))
		{
			return false;
		}

		// For 4+ argument overloads the options (param[3]) must also be constant.
		if (context.VisitedParameters.Count >= 4 && !TryGetLiteralValue(context.VisitedParameters[3], context, out _))
		{
			return false;
		}

		// Timeout (param[4] for 5-arg overloads) passes through - goes straight into the Regex constructor.

		// Collect the constructor arguments for the cached Regex: pattern + optional options + optional timeout.
		var ctorArgs = context.VisitedParameters.Count switch
		{
			3 => new List<ExpressionSyntax> { context.VisitedParameters[1] },
			4 => new List<ExpressionSyntax> { context.VisitedParameters[1], context.VisitedParameters[3] },
			_ => new List<ExpressionSyntax> { context.VisitedParameters[1], context.VisitedParameters[3], context.VisitedParameters[4] }
		};

		// Instance Replace takes (input, replacement) - drop the pattern and options arguments.
		result = GetRegexInvocation(context, ctorArgs, [ context.VisitedParameters[0], context.VisitedParameters[2] ]);

		return true;
	}
}