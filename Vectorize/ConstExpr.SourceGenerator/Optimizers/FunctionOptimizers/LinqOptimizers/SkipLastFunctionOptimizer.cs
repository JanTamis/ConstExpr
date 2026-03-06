using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.SkipLast context.Method.
/// Optimizes patterns such as:
/// - collection.SkipLast(0) => collection (skip nothing)
/// - collection.SkipLast(n).SkipLast(m) => collection.SkipLast(n + m)
/// </summary>
public class SkipLastFunctionOptimizer() : BaseLinqFunctionOptimizer("SkipLast", 1)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}
		
		var isNewSource = TryGetOptimizedChainExpression(source, OrderingOperations, out source);

		// Optimize SkipLast(0) => source (skip nothing)
		if (context.VisitedParameters[0] is LiteralExpressionSyntax { Token.Value: <= 0 })
		{
			result = source;
			return true;
		}
		
		if (IsLinqMethodChain(source, out var methodName, out var invocation)
		    && TryGetLinqSource(invocation, out var invocationSource))
		{
			switch (methodName)
			{
				case "SkipLast" when invocation.ArgumentList.Arguments.Count == 1:
				{
					var argument = invocation.ArgumentList.Arguments[0].Expression;
					var intType = context.Model.Compilation.GetSpecialType(SpecialType.System_Int32);

					var newArgument = context.OptimizeBinaryExpression(SyntaxFactory.BinaryExpression(SyntaxKind.AddExpression, argument, context.VisitedParameters[0]), intType, intType, intType);
					
					result = CreateInvocation(invocationSource, Name, newArgument as ExpressionSyntax);
					return true;
				}
			}
		}

		if (isNewSource)
		{
			result = CreateInvocation(source, Name, context.VisitedParameters[0]);
			return true;
		}

		result = null;
		return false;
	}
}

