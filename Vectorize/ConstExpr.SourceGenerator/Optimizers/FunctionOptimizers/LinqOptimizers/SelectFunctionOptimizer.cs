using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

public class SelectFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Select), 1)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context)
		    || !TryGetLambda(context.VisitedParameters[0], out var lambda)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		if (TryExecutePredicates(context, source, out result))
		{
			return true;
		}

		if (IsIdentityLambda(lambda))
		{
			result = context.Visit(source) ?? source;
			return true;
		}

		// Optimize .Select(x => (T)x) to .Cast<T>()
		if (IsCastLambda(lambda, out var castType))
		{
			result = CreateCastMethodCall(context.Visit(source) ?? source, castType);
			return true;
		}

		if (IsLinqMethodChain(source, nameof(Enumerable.Select), out var innerInvocation)
		    && TryGetLambda(innerInvocation.ArgumentList.Arguments[0].Expression, out var innerLambda)
		    && TryGetLinqSource(innerInvocation, out var innerSource))
		{
			// Combine the two lambdas: source.Select(inner).Select(outer) => source.Select(combined)
			var combinedLambda = CombineLambdas(lambda, context.Visit(innerLambda) as LambdaExpressionSyntax ?? innerLambda);

			// Create a new Select call with the combined lambda
			result = UpdateInvocation(context, context.Visit(innerSource) ?? innerSource, combinedLambda);
			return true;
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
		var genericName = SyntaxFactory.GenericName(
			SyntaxFactory.Identifier(nameof(Enumerable.Cast)),
			SyntaxFactory.TypeArgumentList(
				SyntaxFactory.SingletonSeparatedList(targetType)));

		return CreateInvocation(source, genericName);
	}

	private LambdaExpressionSyntax CombineLambdas(LambdaExpressionSyntax outer, LambdaExpressionSyntax inner)
	{
		// Get parameter names from both lambdas
		var innerParam = GetLambdaParameter(inner);
		var outerParam = GetLambdaParameter(outer);

		// Get the body expressions
		var innerBody = GetLambdaBody(inner);
		var outerBody = GetLambdaBody(outer);

		// Replace the outer lambda's parameter with the inner lambda's body
		var combinedBody = ReplaceIdentifier(outerBody, outerParam, innerBody);

		// Create a new lambda with the inner parameter and the combined body
		return SyntaxFactory.SimpleLambdaExpression(
			SyntaxFactory.Parameter(SyntaxFactory.Identifier(innerParam)),
			combinedBody
		);
	}
}