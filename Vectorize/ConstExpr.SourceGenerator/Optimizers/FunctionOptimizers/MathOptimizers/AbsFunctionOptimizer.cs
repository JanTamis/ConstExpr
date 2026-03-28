using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class AbsFunctionOptimizer() : BaseMathFunctionOptimizer("Abs", 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var arg = context.VisitedParameters[0];

		// 1) Unsigned integer: Abs(x) -> x
		if (paramType.IsUnsignedInteger())
		{
			result = arg;
			return true;
		}

		// 2) Idempotence: Abs(Abs(x)) -> Abs(x)
		if (arg is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Abs" } } innerInv)
		{
			result = innerInv;
			return true;
		}

		// 3) Unary minus: Abs(-x) -> Abs(x)
		if (arg is PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int)SyntaxKind.MinusToken } prefix)
		{
			result = CreateInvocation(paramType, Name, prefix.Operand);
			return true;
		}

		// Default: keep as Abs call (target numeric helper type)
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}
}