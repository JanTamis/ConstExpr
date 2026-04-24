using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

public class SelectFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Select), n => n is 1)
{
	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (!TryGetLambda(context.VisitedParameters[0], out var lambda))
		{
			result = null;
			return false;
		}

		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}

		if (IsIdentityLambda(lambda))
		{
			result = source;
			return true;
		}

		// Optimize .Select(x => (T)x) to .Cast<T>()
		if (IsCastLambda(lambda, out var castType))
		{
			var invocation = CreateCastMethodCall(source, castType);

			if (context.Model.TryGetTypeSymbol(castType, context.SymbolStore, out var castTypeSymbol))
			{
				result = TryOptimizeByOptimizer<CastFunctionOptimizer>(context, invocation, castTypeSymbol);
				return true;
			}

			result = invocation;
			return true;
		}

		if (IsLinqMethodChain(source, out var methodName, out var innerInvocation)
		    && TryGetLinqSource(innerInvocation, out var innerSource))
		{
			switch (methodName)
			{
				case nameof(Enumerable.Select) when innerInvocation.ArgumentList.Arguments.Count > 0
				                                    && TryGetLambda(innerInvocation.ArgumentList.Arguments[0].Expression, out var innerLambda):
				{
					// Combine the two lambdas: source.Select(inner).Select(outer) => source.Select(combined)
					var combinedLambda = CombineLambdas(lambda, innerLambda);

					// Create a new Select call with the combined lambda
					result = UpdateInvocation(context, innerSource, combinedLambda);
					return true;
				}
			}
		}

		result = null;
		return false;
	}

	private bool IsCastLambda(LambdaExpressionSyntax lambda, [NotNullWhen(true)] out TypeSyntax? castType)
	{
		castType = null;

		// Check for lambda in the form: x => x as T
		var body = lambda switch
		{
			SimpleLambdaExpressionSyntax { ExpressionBody: { } expr } => expr,
			ParenthesizedLambdaExpressionSyntax { ExpressionBody: { } expr } => expr,
			_ => null
		};

		if (body is not CastExpressionSyntax castExpression)
		{
			return false;
		}

		// Verify left side is the lambda parameter
		var paramName = GetLambdaParameter(lambda);

		if (castExpression.Expression is not IdentifierNameSyntax identifier || identifier.Identifier.Text != paramName)
		{
			return false;
		}

		// Extract the target type
		castType = castExpression.Type;
		return true;
	}

	private InvocationExpressionSyntax CreateCastMethodCall(ExpressionSyntax source, TypeSyntax targetType)
	{
		var genericName = GenericName(
			Identifier(nameof(Enumerable.Cast)),
			TypeArgumentList(
				SingletonSeparatedList(targetType)));

		return CreateInvocation(source, genericName);
	}
}