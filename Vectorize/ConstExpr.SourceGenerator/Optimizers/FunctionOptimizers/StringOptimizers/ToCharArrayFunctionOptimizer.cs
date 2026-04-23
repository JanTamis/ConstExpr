using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;

/// <summary>
/// Optimizes ToCharArray calls on string literals:
/// - "hello".ToCharArray() → ['h', 'e', 'l', 'l', 'o']
/// </summary>
public class ToCharArrayFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "ToCharArray", false, n => n is 0)
{
	protected override bool TryOptimizeString(FunctionOptimizerContext context, ITypeSymbol stringType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		result = null;
		
		if (!TryGetStringInstance(out var str) || str is null)
		{
			return false;
		}

		// Create collection expression with char literals
		var elements = str
			.Select(c => CreateLiteral(c))
			.Where(e => e is not null)
			.Select(e => ExpressionElement(e!))
			.Cast<CollectionElementSyntax>()
			.ToArray();

		result = CollectionExpression(SeparatedList(elements));
		return true;
	}
}

