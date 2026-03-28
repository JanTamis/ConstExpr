using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Extensions;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers
{
	public abstract class BaseStringFunctionOptimizer(SyntaxNode? instance, string name, bool isStatic, params HashSet<int> parameterCounts) : BaseFunctionOptimizer
	{
		public string Name { get; } = name;

		public SyntaxNode? Instance { get; } = instance;

		public override bool TryOptimize(FunctionOptimizerContext context, [NotNullWhen(true)] out SyntaxNode? result)
		{
			if (!IsValidMethod(context.Method, out var stringType))
			{
				result = null;
				return false;
			}

			return TryOptimizeString(context, stringType, out result);
		}

		protected abstract bool TryOptimizeString(FunctionOptimizerContext context, ITypeSymbol stringType, [NotNullWhen(true)] out SyntaxNode? result);

		protected bool IsValidMethod(IMethodSymbol method, out INamedTypeSymbol type)
		{
			type = method.ContainingType;

			return method.Name == Name 
			       && type.SpecialType == SpecialType.System_String 
			       && method.IsStatic == isStatic
			       && parameterCounts.Contains(method.Parameters.Length);
		}

		protected bool TryGetStringInstance(out string? result)
		{
			if (Instance is LiteralExpressionSyntax les 
			    && les.IsKind(SyntaxKind.StringLiteralExpression))
			{
				result = les.Token.Value as string;
				return true;
			}

			result = null;
			return false;
		}
	}
}
