using System;
using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Refactorers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
/// Miscellaneous visitor methods for the ConstExprPartialRewriter.
/// Handles object creation, list visiting, and other utility methods.
/// </summary>
public partial class ConstExprPartialRewriter
{
	public override SyntaxNode? VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
	{
		if (!semanticModel.TryGetSymbol(node.Type, symbolStore, out ITypeSymbol? type))
		{
			return base.VisitObjectCreationExpression(node);
		}

		usings.Add(type.ContainingNamespace.ToDisplayString());

		// Skip Random type
		if (type.EqualsType(semanticModel.Compilation.GetTypeByMetadataName("System.Random")))
		{
			return base.VisitObjectCreationExpression(node);
		}

		// Special-case: fold string object creations to literals
		if (type.SpecialType == SpecialType.System_String)
		{
			var result = TryFoldStringCreation(node);

			if (result is not null)
			{
				return result;
			}
		}

		// Try to create the object and convert it to a literal
		return TryCreateObjectLiteral(node, type) ?? base.VisitObjectCreationExpression(node);
	}

	/// <summary>
	/// Converts anonymous type creation expressions to value tuple expressions in generated code.
	/// This is safe when the anonymous type is used as an intermediate value.
	/// Skipped when the return type of the enclosing method is <c>dynamic</c>, because value tuple
	/// element names are compiler metadata only — <c>((dynamic)result).Name</c> would fail at runtime
	/// on a <c>ValueTuple</c> since it has no actual <c>Name</c> property.
	/// </summary>
	public override SyntaxNode VisitAnonymousObjectCreationExpression(AnonymousObjectCreationExpressionSyntax node)
	{
		// Visit nested expressions first so constant folding is applied to initializer values
		var visitedInitializers = node.Initializers
			.Select(init => (init.NameEquals, Expression: Visit(init.Expression) as ExpressionSyntax ?? init.Expression))
			.ToList();

		var updatedNode = node.WithInitializers(
			SeparatedList(visitedInitializers.Select(init =>
				AnonymousObjectMemberDeclarator(init.NameEquals, init.Expression))));

		// Skip the conversion when the enclosing method returns dynamic — runtime property access via
		// dynamic dispatch on a ValueTuple would fail because named tuple elements are not real properties.
		if (IsInsideDynamicReturnMethod())
		{
			return updatedNode;
		}

		return ConvertAnonymousTypeToTupleRefactoring.TryConvertAnonymousTypeToTuple(updatedNode, out var tuple)
			? tuple
			: updatedNode;

		bool IsInsideDynamicReturnMethod()
		{
			// Walk up the syntax tree to find the enclosing method-like declaration
			SyntaxNode? current = node.Parent;

			while (current is not null)
			{
				if (current is MethodDeclarationSyntax method)
				{
					return method.ReturnType is IdentifierNameSyntax { Identifier.Text: "dynamic" }
					       || semanticModel.GetTypeInfo(method.ReturnType).Type?.TypeKind == TypeKind.Dynamic;
				}

				if (current is LocalFunctionStatementSyntax localFunc)
				{
					return localFunc.ReturnType is IdentifierNameSyntax { Identifier.Text: "dynamic" }
					       || semanticModel.GetTypeInfo(localFunc.ReturnType).Type?.TypeKind == TypeKind.Dynamic;
				}

				current = current.Parent;
			}

			return false;
		}
	}

	/// <summary>
	/// Tries to fold string object creation to a literal.
	/// </summary>
	private SyntaxNode? TryFoldStringCreation(ObjectCreationExpressionSyntax node)
	{
		var args = node.ArgumentList?.Arguments
			.Select(a => Visit(a.Expression))
			.OfType<ExpressionSyntax>()
			.ToList() ?? [];

		if (args.Count != 1)
		{
			return null;
		}

		var arg = args[0];

		return arg switch
		{
			// new string("text") => "text"
			LiteralExpressionSyntax les when les.IsKind(SyntaxKind.StringLiteralExpression) => les,
			// new string([]) or new string(['a','b',...]) => "..."
			CollectionExpressionSyntax collection => TryFoldCharCollectionToString(collection),
			_ => null
		};

	}

	/// <summary>
	/// Tries to fold a char collection to a string literal.
	/// </summary>
	private SyntaxNode? TryFoldCharCollectionToString(CollectionExpressionSyntax collection)
	{
		var elements = collection.Elements.OfType<ExpressionElementSyntax>().ToList();

		if (elements.Count == 0)
		{
			return LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(string.Empty));
		}

		var chars = new List<char>(elements.Count);

		foreach (var el in elements)
		{
			var e = Visit(el.Expression) as ExpressionSyntax ?? el.Expression;

			switch (e)
			{
				case LiteralExpressionSyntax cle when cle.IsKind(SyntaxKind.CharacterLiteralExpression) && cle.Token.Value is char ch:
				{
					chars.Add(ch);
					continue;
				}
				case LiteralExpressionSyntax sle when sle.IsKind(SyntaxKind.StringLiteralExpression):
				{
					var text = sle.Token.ValueText;

					if (text.Length == 1)
					{
						chars.Add(text[0]);
						continue;
					}
					break;
				}
			}

			return null;
		}

		return LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(new string(chars.ToArray())));
	}

	/// <summary>
	/// Tries to create an object and convert it to a literal.
	/// </summary>
	private SyntaxNode? TryCreateObjectLiteral(ObjectCreationExpressionSyntax node, ITypeSymbol type)
	{
		var runtimeType = loader.GetType(type);

		if (runtimeType == null)
		{
			return null;
		}

		try
		{
			var arguments = node.ArgumentList?.Arguments
				.Select(arg => Visit(arg.Expression))
				.OfType<ExpressionSyntax>()
				.ToList() ?? [];

			var argumentValues = arguments
				.WhereSelect<ExpressionSyntax, object?>(TryGetLiteralValue)
				.ToList();

			if (arguments.Count != argumentValues.Count)
			{
				return null;
			}

			var constructors = runtimeType.GetConstructors();
			var matchingConstructor = constructors.FirstOrDefault(c => c.GetParameters().Length == arguments.Count);

			if (matchingConstructor == null)
			{
				return null;
			}

			var constructedObject = matchingConstructor.Invoke(argumentValues.ToArray());

			if (TryCreateLiteral(constructedObject, out var literalExpression))
			{
				return literalExpression;
			}
		}
		catch (Exception ex)
		{
			exceptionHandler(node, ex);
		}

		return null;
	}

	public override SyntaxList<TNode> VisitList<TNode>(SyntaxList<TNode> list)
	{
		var result = new List<TNode>();
		var shouldStop = false;

		foreach (var node in list)
		{
			if (shouldStop)
      {
        break;
      }

      var visited = Visit(node);

			switch (visited)
			{
				case null:
				{
					continue;
				}
				case BlockSyntax block:
				{
					foreach (var st in block.Statements)
					{
						if (st is TNode t)
						{
							if (st is ContinueStatementSyntax)
							{
								return List(result);
							}
							
							result.Add(t);

							if (st is ReturnStatementSyntax or BreakStatementSyntax)
							{
								shouldStop = true;
								break;
							}
						}
					}
					break;
				}
				case TNode t:
				{
					if (t is ContinueStatementSyntax)
					{
						return List(result);
					}

					result.Add(t);

					if (visited is ReturnStatementSyntax or BreakStatementSyntax)
					{
						shouldStop = true;
					}

					break;
				}
			}
		}

		return List(result);
	}

	public override SeparatedSyntaxList<TNode> VisitList<TNode>(SeparatedSyntaxList<TNode> list)
	{
		var result = new List<TNode>();
		var shouldStop = false;

		foreach (var node in list)
		{
			if (shouldStop)
			{
				break;
			}

			var visited = Visit(node);

			switch (visited)
			{
				case null:
				{
					continue;
				}
				case BlockSyntax block:
				{
					foreach (var st in block.Statements)
					{
						if (st is TNode t)
						{
							if (st is ContinueStatementSyntax)
							{
								return SeparatedList(result);
							}
							
							result.Add(t);

							if (st is ReturnStatementSyntax or BreakStatementSyntax)
							{
								shouldStop = true;
								break;
							}
						}
					}
					break;
				}
				case TNode t:
				{
					if (t is ContinueStatementSyntax)
					{
						return SeparatedList(result);
					}

					result.Add(t);

					if (visited is ReturnStatementSyntax or BreakStatementSyntax)
					{
						shouldStop = true;
					}

					break;
				}
			}
		}

		return SeparatedList(result);
	}
}

