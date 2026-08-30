using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.RegexOptimizers;

/// <summary>
///   Base class for optimizers that target <c>System.Text.RegularExpressions.Regex</c> methods.
///   Subclasses are discovered via reflection (same pattern as Math/Linq/Simd optimizers).
/// </summary>
public abstract class BaseRegexFunctionOptimizer(string name, Func<int, bool> isValidParameterCount) : BaseFunctionOptimizer
{
	public string Name { get; } = name;
	public Func<int, bool> IsValidParameterCount { get; } = isValidParameterCount;

	public override bool TryOptimize(FunctionOptimizerContext context, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (!IsRegexMethod(context.Method))
		{
			result = null;
			return false;
		}

		return TryOptimizeRegex(context, out result);
	}

	/// <summary>
	///   The shape shared by every static <c>Regex</c> overload except <c>Replace</c>:
	///   <c>(input, pattern[, options[, timeout]])</c>. Hoists the <see cref="Regex" /> when the pattern
	///   and options are compile-time constants; the timeout passes straight into the constructor.
	/// </summary>
	protected virtual bool TryOptimizeRegex(FunctionOptimizerContext context, [NotNullWhen(true)] out SyntaxNode? result)
	{
		result = null;

		context.Usings.Add("System.Text.RegularExpressions");

		// Pattern must be a compile-time constant.
		if (!TryGetLiteralValue(context.VisitedParameters[1], context, out _))
		{
			return false;
		}

		// Options (param[2] for >=3-arg overloads) must also be constant.
		if (context.VisitedParameters.Count >= 3 && !TryGetLiteralValue(context.VisitedParameters[2], context, out _))
		{
			return false;
		}

		// Timeout (param[3] for 4-arg overloads) passes through - goes straight into the Regex constructor.

		result = GetRegexInvocation(context);
		return true;
	}

	/// <summary>
	///   Hoists a <see cref="Regex" /> built from the constant pattern (plus any constant options and
	///   timeout) into a <c>private static readonly</c> field and returns the equivalent instance-method
	///   call on that field.
	/// </summary>
	/// <param name="ctorArguments">
	///   The arguments forwarded to the <c>new Regex(...)</c> constructor. Defaults to every parameter
	///   after the input — correct for every overload except <c>Replace</c>, where the replacement sits
	///   between the pattern and the options.
	/// </param>
	/// <param name="callArguments">
	///   The arguments forwarded to the instance method. Defaults to the input alone.
	/// </param>
	protected InvocationExpressionSyntax GetRegexInvocation(FunctionOptimizerContext context, IReadOnlyList<ExpressionSyntax>? ctorArguments = null, IReadOnlyList<ExpressionSyntax>? callArguments = null)
	{
		ctorArguments ??= context.VisitedParameters.Skip(1).ToList();
		callArguments ??= [ context.VisitedParameters[0] ];

		// Build a deterministic field name from the constant constructor arguments.
		var patternKey = String.Concat(
			ctorArguments.Select(s => TryGetLiteralValue(s, context, out var lit) && lit is string str ? str : s.ToFullString())
		);
		var variableName = $"Regex_{patternKey.GetDeterministicHashString()}";

		var field = FieldDeclaration(VariableDeclaration(IdentifierName(nameof(Regex)))
				.WithVariables(
					SingletonSeparatedList(
						VariableDeclarator(Identifier(variableName))
							.WithInitializer(EqualsValueClause(
								ObjectCreationExpression(IdentifierName(nameof(Regex)))
									.WithArgumentList(ArgumentList(SeparatedList(ctorArguments.Select(Argument)))))
							))
				))
			.WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.StaticKeyword), Token(SyntaxKind.ReadOnlyKeyword)));

		context.AdditionalSyntax.Add(field, true);
		context.Usings.Add("System.Text.RegularExpressions");

		return InvocationExpression(MemberAccessExpression(IdentifierName(variableName), IdentifierName(context.Method.Name)))
			.WithArgumentList(ArgumentList(SeparatedList(callArguments.Select(Argument))));
	}

	/// <summary>
	///   Gates the "lower this regex to a plain string operation" rewrites. Succeeds only when the
	///   pattern is a non-empty constant <em>and</em> nothing about the call can change what that pattern
	///   means.
	///   <para>
	///     Every option except <c>None</c> is rejected, and each for a reason verified against the real
	///     engine: <c>Multiline</c> makes <c>^Test</c> match <c>"x\nTest"</c> (so it is no longer
	///     <c>StartsWith</c>), <c>IgnoreCase</c> makes a literal match case-blind <em>and</em>
	///     culture-sensitively so (under <c>tr-TR</c> the engine does not match <c>I</c> against
	///     <c>i</c> while <c>OrdinalIgnoreCase</c> does, so that is not a safe substitute either),
	///     <c>ECMAScript</c> narrows <c>\w</c> to ASCII, <c>IgnorePatternWhitespace</c> changes what the
	///     pattern string parses to at all, and <c>RightToLeft</c>/<c>Singleline</c> change the anchor
	///     and <c>.</c> analysis.
	///   </para>
	/// </summary>
	/// <param name="optionsIndex">Index of the <c>options</c> parameter; the timeout sits right after it.</param>
	protected bool TryGetLowerablePattern(FunctionOptimizerContext context, int patternIndex, int optionsIndex, [NotNullWhen(true)] out string? pattern)
	{
		pattern = null;

		if (!TryGetLiteralValue(context.VisitedParameters[patternIndex], context, out var patternValue)
		    || patternValue is not string text
		    || text.Length == 0)
		{
			return false;
		}

		if (context.VisitedParameters.Count > optionsIndex)
		{
			if (!TryGetLiteralValue(context.VisitedParameters[optionsIndex], context, out var optionsValue)
			    || optionsValue is not RegexOptions options
			    || options != RegexOptions.None)
			{
				return false;
			}
		}

		// A timeout argument means the call can raise RegexMatchTimeoutException. A plain string
		// operation never can, so leave those calls to the engine.
		if (context.VisitedParameters.Count > optionsIndex + 1)
		{
			return false;
		}

		pattern = text;
		return true;
	}

	private bool IsRegexMethod(IMethodSymbol method)
	{
		return method.Name == Name
		       && method.IsStatic
		       && method.ContainingType.ToString() == "System.Text.RegularExpressions.Regex"
		       && IsValidParameterCount(method.Parameters.Length);
	}
}