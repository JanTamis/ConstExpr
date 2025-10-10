using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public abstract class BaseFunctionOptimizer(string name, params int[] parameterCounts)
{
	public string Name { get; } = name;
	public int[] ParameterCounts { get; } = parameterCounts;

	public abstract bool TryOptimize(IMethodSymbol method, FloatingPointEvaluationMode floatingPointMode, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result);

	protected InvocationExpressionSyntax CreateInvocation(ITypeSymbol type, string name, params IEnumerable<ExpressionSyntax> parameters)
	{
		return SyntaxFactory.InvocationExpression(
				SyntaxFactory.MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					SyntaxFactory.ParseTypeName(type.Name),
					SyntaxFactory.IdentifierName(name)))
			.WithArgumentList(
				SyntaxFactory.ArgumentList(
					SyntaxFactory.SeparatedList(
						parameters.Select(SyntaxFactory.Argument))));
	}

	protected InvocationExpressionSyntax CreateInvocation(string name, params IEnumerable<ExpressionSyntax> parameters)
	{
		return SyntaxFactory.InvocationExpression(
						SyntaxFactory.IdentifierName(name))
						.WithArgumentList(
							SyntaxFactory.ArgumentList(
								SyntaxFactory.SeparatedList(
									parameters.Select(SyntaxFactory.Argument))));
	}

	protected static bool IsPure(SyntaxNode node)
	{
		return node switch
		{
			IdentifierNameSyntax => true,
			LiteralExpressionSyntax => true,
			ParenthesizedExpressionSyntax par => IsPure(par.Expression),
			PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int)SyntaxKind.MinusToken } u => IsPure(u.Operand),
			BinaryExpressionSyntax b => IsPure(b.Left) && IsPure(b.Right),
			_ => false
		};
	}

	protected bool HasMethod(ITypeSymbol type, string name, int parameterCount)
	{
		return type.GetMembers(name)
			.OfType<IMethodSymbol>()
			.Any(m => m.Parameters.Length == parameterCount
								&& m.DeclaredAccessibility == Accessibility.Public
								&& SymbolEqualityComparer.Default.Equals(type, m.ContainingType));
	}

	protected static MethodDeclarationSyntax ParseMethodFromString(string methodString)
	{
		var wrappedCode = $$"""
			public class TempClass
			{
				{{methodString}}
			}
			""";

		var syntaxTree = CSharpSyntaxTree.ParseText(wrappedCode);

		return syntaxTree.GetRoot()
			.DescendantNodes()
			.OfType<MethodDeclarationSyntax>()
			.First();
	}

	protected bool IsMathType(ITypeSymbol? type)
	{
		return type?.ToString() is "System.Math" or "System.MathF";
	}

	protected bool IsValidMethod(IMethodSymbol method, [NotNullWhen(true)] out ITypeSymbol type)
	{
		type = method.Parameters.Length > 0 ? method.Parameters[0].Type : null!;

		return method.Name == Name
			&& type.IsNumericType()
			&& ParameterCounts.Contains(method.Parameters.Length)
			&& IsMathType(method.ContainingType) || method.ContainingType.EqualsType(type);
	}

	protected bool IsApproximately(double a, double b)
	{
		return Math.Abs(a - b) <= Double.Epsilon;
	}
}