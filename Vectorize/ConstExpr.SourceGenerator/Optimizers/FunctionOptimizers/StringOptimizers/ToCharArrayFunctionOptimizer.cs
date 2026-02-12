using System;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ConstExpr.SourceGenerator.Models;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;

/// <summary>
/// Optimizes ToCharArray calls on string literals:
/// - "hello".ToCharArray() → ['h', 'e', 'l', 'l', 'o']
/// </summary>
public class ToCharArrayFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "ToCharArray")
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMethod(context.Method, out _) || context.Method.IsStatic || context.VisitedParameters.Count != 0)
		{
			return false;
		}

		if (!TryGetStringInstance(out var str) || str is null)
		{
			return false;
		}

		// Create collection expression with char literals
		var elements = str
			.Select(c => SyntaxHelpers.CreateLiteral(c))
			.Where(e => e is not null)
			.Select(e => ExpressionElement(e!))
			.Cast<CollectionElementSyntax>()
			.ToArray();

		result = CollectionExpression(SeparatedList(elements));
		return true;
	}
}

