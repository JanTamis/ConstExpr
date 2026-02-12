using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.SequenceEqual context.Method.
/// Optimizes patterns such as:
/// - collection.SequenceEqual(collection) => true (same reference)
/// - Enumerable.Empty&lt;T&gt;().SequenceEqual(Enumerable.Empty&lt;T&gt;()) => true
/// </summary>
public class SequenceEqualFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.SequenceEqual), 1)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context.Model, context.Method)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		var secondSource = context.VisitedParameters[0];
		
		source = context.Visit(source) ?? source;

		// Optimize collection.SequenceEqual(collection) => true (same reference)
		// Optimize Enumerable.Empty<T>().SequenceEqual(Enumerable.Empty<T>()) => true
		if (AreSyntacticallyEquivalent(source, secondSource)
		    || IsEmptyEnumerable(source) && IsEmptyEnumerable(secondSource))
		{
			result = LiteralExpression(SyntaxKind.TrueLiteralExpression);
			return true;
		}
		
		result = null;
		return false;
	}
}

