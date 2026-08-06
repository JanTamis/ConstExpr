using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;

/// <summary>
///   Optimizes string.IsNullOrWhiteSpace(literal) to true/false, and folds the call on a provably
///   non-null argument into a span-based all-whitespace test that skips the redundant null check.
/// </summary>
public class IsNullOrWhiteSpaceFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "IsNullOrWhiteSpace", true, n => n is 1)
{
	protected override bool TryOptimizeString(FunctionOptimizerContext context, ITypeSymbol stringType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		result = null;

		var parameter = context.VisitedParameters[0];

		if (CanBeNull(context, parameter))
		{
			return false;
		}

		// MemoryExtensions.IsWhiteSpace(ReadOnlySpan<char>) is .NET 8+; leave the call as written when
		// the target compilation doesn't have it.
		if (!context.Model.Compilation.GetTypeByMetadataName("System.MemoryExtensions").HasMethod("IsWhiteSpace"))
		{
			return false;
		}

		context.Usings.Add("System");

		// s.AsSpan().IsWhiteSpace() matches string.IsNullOrWhiteSpace(s) for every input, minus the null
		// test: AsSpan maps null to an empty span, and an empty span is vacuously all-whitespace.
		// s.Length == 0 would be wrong here: "   " has Length 3 but is all-whitespace.
		result = InvocationExpression(
			MemberAccessExpression(
				InvocationExpression(
					MemberAccessExpression(parameter, IdentifierName("AsSpan"))),
				IdentifierName("IsWhiteSpace")));

		return true;
	}
}