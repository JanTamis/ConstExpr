using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;

/// <summary>
///   Optimizes string.IsNullOrEmpty(literal) to true/false.
/// </summary>
public class IsNullOrEmptyFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "IsNullOrEmpty", true, n => n is 1)
{
	protected override bool TryOptimizeString(FunctionOptimizerContext context, ITypeSymbol stringType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		result = null;

		if (!CanBeNull(context, context.VisitedParameters[0]))
		{
			result = EqualsExpression(MemberAccessExpression(context.VisitedParameters[0], IdentifierName("Length")), CreateLiteral(0));
			return true;
		}

		return false;
	}
}