using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers
{
	/// <summary>
	/// Optimizes usages of <c>string.Concat</c>. This optimizer:
	/// - Combines adjacent string literal arguments into a single literal (for example, <c>Concat("a", "b", x)</c> -> <c>Concat("ab", x)</c>).
	/// - If the call reduces to a single expression, returns that expression directly.
	/// - Rebuilds the invocation targeting the resolved string type/helper when changes are made.
	/// </summary>
	/// <param name="instance">Optional syntax node instance provided by the optimizer infrastructure; may be null.</param>
	public class ConcatFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "Concat")
	{
		public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, Func<SyntaxNode, ExpressionSyntax?> visit, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
		{
			result = null;

			if (!IsValidMethod(method, out var stringType) || !method.IsStatic)
			{
				return false;
			}

			// If no arguments -> empty string
			if (parameters.Count == 0)
			{
				result = SyntaxHelpers.CreateLiteral(String.Empty);
				return true;
			}

			// Combine adjacent string literals: Concat("a", "b", x, "c") -> Concat("ab", x, "c")
			var newParams = new List<ExpressionSyntax>();
			var literalBuffer = new StringBuilder();

			foreach (var p in parameters)
			{
				switch (p)
				{
					case LiteralExpressionSyntax les when les.IsKind(SyntaxKind.StringLiteralExpression):
						// Use Token.ValueText to get unescaped text
						literalBuffer.Append(les.Token.ValueText);
						break;
					default:
						// Non-literal: flush buffer and add parameter as-is
						FlushBuffer();
						newParams.Add(p);
						break;
				}
			}

			FlushBuffer();

			// If after combining we have a single expression, return it directly
			if (newParams.Count == 1)
			{
				result = newParams[0];
				return true;
			}

			// If nothing changed, don't claim optimization
			if (newParams.Count == parameters.Count)
			{
				// Compare sequence elements by reference/simple kind - cheap heuristic
				var changed = false;

				for (var i = 0; i < parameters.Count; i++)
				{
					if (!parameters[i].IsEquivalentTo(newParams[i]))
					{
						changed = true;
						break;
					}
				}

				if (!changed)
				{
					return false;
				}
			}

			// Rebuild invocation targeting the string helper/type
			result = CreateInvocation(stringType, Name, newParams);
			return true;

			void FlushBuffer()
			{
				if (literalBuffer.Length == 0)
				{
					return;
				}

				var lit = SyntaxHelpers.CreateLiteral(literalBuffer.ToString());

				newParams.Add(lit!);
				literalBuffer.Clear();
			}
		}
	}
}
