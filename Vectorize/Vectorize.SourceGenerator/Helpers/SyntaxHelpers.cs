using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Vectorize.Helpers;

public static class SyntaxHelpers
{
	public static SyntaxKind GetSyntaxKind(object? value)
	{
		return value switch
		{
			int => SyntaxKind.NumericLiteralExpression,
			float => SyntaxKind.NumericLiteralExpression,
			double => SyntaxKind.NumericLiteralExpression,
			long => SyntaxKind.NumericLiteralExpression,
			decimal => SyntaxKind.NumericLiteralExpression,
			string => SyntaxKind.StringLiteralExpression,
			char => SyntaxKind.CharacterLiteralExpression,
			true => SyntaxKind.TrueLiteralExpression,
			false => SyntaxKind.FalseLiteralExpression,
			null => SyntaxKind.NullLiteralExpression,
			_ => throw new ArgumentOutOfRangeException()
		};
	}

	public static object? GetVariableValue(Compilation compilation, SyntaxNode? expression, Dictionary<string, object?> variables)
	{
		if (!TryGetVariableValue(compilation, expression, variables, out var value))
		{
			value = null;
		}
		
		return value;
	}

	public static bool TryGetVariableValue(Compilation compilation, SyntaxNode? expression, Dictionary<string, object?> variables, out object? value)
	{
		if (TryGetSemanticModel(compilation, expression, out var semanticModel) && semanticModel.GetConstantValue(expression) is { HasValue: true, Value: var temp })
		{
			value = temp;
			return true;
		}
		
		switch (expression)
		{
			case LiteralExpressionSyntax literal:
				switch (literal.Kind())
				{
					case SyntaxKind.StringLiteralExpression:
					case SyntaxKind.CharacterLiteralExpression:
						value = literal.Token.Value;
						return true;
					case SyntaxKind.TrueLiteralExpression:
						value = true;
						return true;
					case SyntaxKind.FalseLiteralExpression:
						value = false;
						return true;
					case SyntaxKind.NullLiteralExpression:
						value = null;
						return true;
					default:
						value = literal.Token.Value;
						return true;
				}
			case IdentifierNameSyntax identifier:
				return variables.TryGetValue(identifier.Identifier.Text, out value);
			case MemberAccessExpressionSyntax simple:
				return TryGetVariableValue(compilation, simple.Expression, variables, out value);
			default:
				value = null;
				return true;
		}
	}

	public static string? GetVariableName(SyntaxNode? expression)
	{
		return expression switch
		{
			IdentifierNameSyntax identifier => identifier.Identifier.Text,
			_ => null
		};
	}
	
	public static LiteralExpressionSyntax CreateLiteral<T>(T? value)
	{
		return value switch
		{
			int i => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(i)),
			float f => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(f)),
			double d => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(d)),
			long l => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(l)),
			decimal dec => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(dec)),
			string s => SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(s)),
			char c => SyntaxFactory.LiteralExpression(SyntaxKind.CharacterLiteralExpression, SyntaxFactory.Literal(c)),
			bool b => SyntaxFactory.LiteralExpression(b ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression),
			null => SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression),
			_ => throw new ArgumentOutOfRangeException()
		};
	}

	public static object? GetConstantValue(Compilation compilation, ExpressionSyntax expression, CancellationToken token)
	{
		if (TryGetSemanticModel(compilation, expression, out var semanticModel) && semanticModel.GetConstantValue(expression, token) is { HasValue: true, Value: var value })
		{
			return value;
		}

		return expression switch
		{
			LiteralExpressionSyntax literal => literal.Token.Value,
			CollectionExpressionSyntax collection => ImmutableArray.CreateRange(collection.Elements
				.OfType<ExpressionElementSyntax>()
				.Select(x => GetConstantValue(compilation, x.Expression, token))),
			_ => null,
		};
	}

	public static bool TryGetConstantValue(Compilation compilation, ExpressionSyntax expression, CancellationToken token, out object? value)
	{
		if (TryGetSemanticModel(compilation, expression, out var semanticModel) && semanticModel.GetConstantValue(expression, token) is { HasValue: true, Value: var temp })
		{
			value = temp;
			return true;
		}
		
		switch (expression)
		{
			case LiteralExpressionSyntax literal:
				value = literal.Token.Value;
				return true;
			case CollectionExpressionSyntax collection:
				value = ImmutableArray.CreateRange(collection.Elements
					.OfType<ExpressionElementSyntax>()
					.Select(x => GetConstantValue(compilation, x.Expression, token)));
				return true;
			default:
				value = null;
				return false;
		}
	}

	public static bool IsConstantValue(SemanticModel semanticModel, ExpressionSyntax expression, CancellationToken token)
	{
		if (semanticModel.GetConstantValue(expression, token) is { HasValue: true })
		{
			return true;
		}

		return expression switch
		{
			LiteralExpressionSyntax => true,
			CollectionExpressionSyntax => true,
			_ => false
		};
	}
	
	public static bool IsConstExprAttribute(AttributeData? attribute)
	{
		return attribute?.AttributeClass is { Name: "ConstExprAttribute", ContainingNamespace.Name: "ConstantExpression" };
	}

	public static bool IsNumericType(ITypeSymbol type)
	{
		return type.SpecialType is SpecialType.System_Byte 
			or SpecialType.System_SByte 
			or SpecialType.System_Int16
			or SpecialType.System_UInt16 
			or SpecialType.System_Int32 
			or SpecialType.System_UInt32 
			or SpecialType.System_Int64 
			or SpecialType.System_UInt64 
			or SpecialType.System_Single 
			or SpecialType.System_Double 
			or SpecialType.System_Decimal;
	}

	public static bool IsImmutableArrayOfNumbers(ITypeSymbol type)	
	{
		if (type is INamedTypeSymbol { Name: "ImmutableArray", TypeArguments.Length: 1 } namedType && namedType.ContainingNamespace.ToString() == "System.Collections.Immutable")
		{
			return IsNumericType(namedType.TypeArguments[0]);
		}
		
		return false;
	}

	public static object? Add(object? left, object? right)
	{
		return left switch
		{
			int i when right is int ri => i + ri,
			int i when right is float rf => i + rf,
			int i when right is double rd => i + rd,
			int i when right is long rl => i + rl,
			int i when right is decimal rdec => i + rdec,
			float f when right is int ri => f + ri,
			float f when right is float rf => f + rf,
			float f when right is double rd => f + rd,
			float f when right is long rl => f + rl,
			float f when right is decimal rdec => f + (float) rdec,
			double d when right is int ri => d + ri,
			double d when right is float rf => d + rf,
			double d when right is double rd => d + rd,
			double d when right is long rl => d + rl,
			double d when right is decimal rdec => d + (double) rdec,
			long l when right is int ri => l + ri,
			long l when right is float rf => l + rf,
			long l when right is double rd => l + rd,
			long l when right is long rl => l + rl,
			long l when right is decimal rdec => l + rdec,
			decimal dec when right is int ri => dec + ri,
			decimal dec when right is float rf => dec + (decimal) rf,
			decimal dec when right is double rd => dec + (decimal) rd,
			decimal dec when right is long rl => dec + rl,
			decimal dec when right is decimal rdec => dec + rdec,
			_ => null
		};
	}

	public static object? Subtract(object? left, object? right)
	{
		return left switch
		{
			int i when right is int ri => i - ri,
			int i when right is float rf => i - rf,
			int i when right is double rd => i - rd,
			int i when right is long rl => i - rl,
			int i when right is decimal rdec => i - rdec,
			float f when right is int ri => f - ri,
			float f when right is float rf => f - rf,
			float f when right is double rd => f - rd,
			float f when right is long rl => f - rl,
			float f when right is decimal rdec => f - (float) rdec,
			double d when right is int ri => d - ri,
			double d when right is float rf => d - rf,
			double d when right is double rd => d - rd,
			double d when right is long rl => d - rl,
			double d when right is decimal rdec => d - (double) rdec,
			long l when right is int ri => l - ri,
			long l when right is float rf => l - rf,
			long l when right is double rd => l - rd,
			long l when right is long rl => l - rl,
			long l when right is decimal rdec => l - rdec,
			decimal dec when right is int ri => dec - ri,
			decimal dec when right is float rf => dec - (decimal) rf,
			decimal dec when right is double rd => dec - (decimal) rd,
			decimal dec when right is long rl => dec - rl,
			decimal dec when right is decimal rdec => dec - rdec,
			_ => null
		};
	}

	public static object? BitwiseNot(object? value)
	{
		return value switch
		{
			int i => ~i,
			long l => ~l,
			_ => null
		};
	}

	public static object? LogicalNot(object? value)
	{
		return value switch
		{
			bool b => !b,
			_ => value,
		};
	}

	public static object? ExecuteMethod(IMethodSymbol methodSymbol, object? instance, params object?[] arguments)
	{
		// Verkrijg de MethodInfo van de IMethodSymbol
		var methodInfo = GetMethodInfo(methodSymbol, arguments);

		if (methodInfo == null)
		{
			throw new InvalidOperationException("MethodInfo could not be retrieved.");
		}

		// Roep de methode aan
		return methodInfo.Invoke(instance, arguments);

		MethodInfo? GetMethodInfo(IMethodSymbol methodSymbol, object?[] arguments)
		{
			// Verkrijg de type van de methode
			var type = Type.GetType(methodSymbol.ContainingType.ToDisplayString());

			return type?.GetMethod(methodSymbol.Name, arguments.Where(w => w is not null).Select(s => s.GetType()).Where(w => w is not null).ToArray());
		}
	}

	public static object? GetPropertyValue(IPropertySymbol propertySymbol, object? instance)
	{
		if (propertySymbol == null)
		{
			throw new InvalidOperationException("Property symbol could not be retrieved.");
		}

		// Gebruik reflectie om de waarde van de eigenschap op te halen
		var propertyName = propertySymbol.Name;
		
		var propertyInfo = propertySymbol.IsStatic
			? propertySymbol.ContainingType.ToDisplayString().GetType().GetProperty(propertyName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
			: instance.GetType().GetProperty(propertyName);

		if (propertyInfo == null)
		{
			throw new InvalidOperationException("Property info could not be retrieved.");
		}

		return propertyInfo.GetValue(propertySymbol.IsStatic ? null : instance);
	}

	public static object? GetFieldValue(IFieldSymbol fieldSymbol, object? instance)
	{
		if (fieldSymbol == null)
		{
			throw new InvalidOperationException("Property symbol could not be retrieved.");
		}

		// Gebruik reflectie om de waarde van de eigenschap op te halen
		var propertyName = fieldSymbol.Name;

		var propertyInfo = fieldSymbol.IsStatic
			? fieldSymbol.ContainingType.ToDisplayString().GetType().GetField(propertyName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
			: instance.GetType().GetField(propertyName);

		if (propertyInfo == null)
		{
			throw new InvalidOperationException("Property info could not be retrieved.");
		}

		return propertyInfo.GetValue(fieldSymbol.IsStatic ? null : instance);
	}
	
	public static bool TryGetSemanticModel(Compilation compilation, SyntaxNode? node, out SemanticModel semanticModel)
	{
		var tree = node?.SyntaxTree;
		
		if (compilation.SyntaxTrees.Contains(tree))
		{
			semanticModel = compilation.GetSemanticModel(tree);
			return true;
		}

		semanticModel = null!;
		return false;
	}
	
	public static SemanticModel GetSemanticModel(Compilation compilation, SyntaxNode node)
	{
		return TryGetSemanticModel(compilation, node, out var semanticModel) 
			? semanticModel 
			: throw new InvalidOperationException("SemanticModel could not be retrieved.");
	}
}