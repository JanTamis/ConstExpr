using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Zip context.Method.
/// Optimizes patterns such as:
/// - collection.Zip(Enumerable.Empty&lt;T&gt;()) => Enumerable.Empty&lt;ValueTuple&lt;...&gt;&gt;()
/// - Enumerable.Empty&lt;T&gt;().Zip(collection) => Enumerable.Empty&lt;ValueTuple&lt;...&gt;&gt;()
/// </summary>
public class ZipFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Zip), 1, 2)
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

		var secondSource = context.VisitedParameters[0];
		
		// If either source is empty, result is empty
		if (IsEmptyEnumerable(source) 
		    || IsEmptyEnumerable(secondSource))
		{
			// Get the return type element from the context.Method
			if (context.Method.ReturnType is INamedTypeSymbol { TypeArguments.Length: > 0 } returnType)
			{
				result = CreateEmptyEnumerableCall(returnType.TypeArguments[0]);
				return true;
			}
		}

		// Optimize collection.Zip(collection) => collection.Select(x => (x, x)) (zip of a collection with itself is just pairs of the same element)
		if (AreSyntacticallyEquivalent(source, secondSource)
		    && TryGetEnumerableMethod(context, nameof(Enumerable.Select), 1, out var selectMethod)
		    && TryGetElementType(context, out var elementType))
		{
			var identfier = Identifier("x");
			var parameter = Parameter(identfier);
			
			var invocation = CreateInvocation(source, nameof(Enumerable.Select), SimpleLambdaExpression(parameter, null, TupleExpression(SeparatedList([ Argument(IdentifierName(identfier)), Argument(IdentifierName(identfier)) ]))));

			selectMethod = selectMethod.Construct(elementType, context.Model.Compilation.CreateValueTuple(elementType, elementType));
			
			context = context.WithInvocationAndMethod(invocation, selectMethod);

			result = TryOptimizeByOptimizer<SelectFunctionOptimizer>(context, invocation);
			return true;
		}

		result = null;
		return false;
	}
}

