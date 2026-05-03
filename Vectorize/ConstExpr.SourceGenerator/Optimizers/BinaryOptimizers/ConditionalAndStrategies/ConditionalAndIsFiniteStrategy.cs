using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.ConditionalAndStrategies;

public class ConditionalAndIsFiniteStrategy() : SymmetricStrategy<BooleanBinaryStrategy, PrefixUnaryExpressionSyntax, PrefixUnaryExpressionSyntax>(SyntaxKind.LogicalNotExpression, SyntaxKind.LogicalNotExpression)
{
	public override bool TryOptimizeSymmetric(BinaryOptimizeContext<PrefixUnaryExpressionSyntax, PrefixUnaryExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (!IsInfinityMethod(context, context.Left.Syntax, out var leftArgument, out var leftContainingType)
		    || !IsNaNMethod(context, context.Right.Syntax, out var rightArgument, out var rightContainingType)
		    || leftArgument.GetDeterministicHash() != rightArgument.GetDeterministicHash()
		    || !SymbolEqualityComparer.Default.Equals(leftContainingType, rightContainingType))
		{
			optimized = null;
			return false;
		}

		optimized = InvocationExpression(
			MemberAccessExpression(IdentifierName(leftContainingType.Name), IdentifierName("IsFinite")),
			ArgumentList(SingletonSeparatedList(leftArgument)));

		return true;
	}

	private bool IsInfinityMethod(BinaryOptimizeContext<PrefixUnaryExpressionSyntax, PrefixUnaryExpressionSyntax> context, PrefixUnaryExpressionSyntax expr, out ArgumentSyntax argument, out ITypeSymbol containingType)
	{
		if (expr.Operand is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "IsInfinity" }, ArgumentList.Arguments.Count: 1 } invocation
		    && context.Model.TryGetMethodSymbol(invocation, context.SymbolStore, out var methodSymbol)
		    && methodSymbol.Name == "IsInfinity"
		    && methodSymbol.ContainingType.IsFloatingNumeric())
		{
			argument = invocation.ArgumentList.Arguments[0];
			containingType = methodSymbol.ContainingType;
			return true;
		}

		argument = null!;
		containingType = null!;
		return false;
	}

	private bool IsNaNMethod(BinaryOptimizeContext<PrefixUnaryExpressionSyntax, PrefixUnaryExpressionSyntax> context, PrefixUnaryExpressionSyntax expr, out ArgumentSyntax argument, out ITypeSymbol containingType)
	{
		if (expr.Operand is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "IsNaN" }, ArgumentList.Arguments.Count: 1 } invocation
		    && context.Model.TryGetMethodSymbol(invocation, context.SymbolStore, out var methodSymbol)
		    && methodSymbol.Name == "IsNaN"
		    && methodSymbol.ContainingType.IsFloatingNumeric())
		{
			argument = invocation.ArgumentList.Arguments[0];
			containingType = methodSymbol.ContainingType;
			return true;
		}

		argument = null!;
		containingType = null!;
		return false;
	}
}