using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using static ConstExpr.SourceGenerator.Helpers.SyntaxHelpers;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
/// Miscellaneous visitor methods for the ConstExprPartialRewriter.
/// Handles object creation, list visiting, and other utility methods.
/// </summary>
public partial class ConstExprPartialRewriter
{
	public override SyntaxNode? VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
	{
		if (!semanticModel.TryGetSymbol(node.Type, out ITypeSymbol? type))
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
					chars.Add(ch);
					continue;
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

			if (TryGetLiteral(constructedObject, out var literalExpression))
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
				break;

			var visited = Visit(node);

			switch (visited)
			{
				case null:
					continue;
				case BlockSyntax block:
					foreach (var st in block.Statements)
					{
						if (st is TNode t)
						{
							result.Add(t);

							if (st is ReturnStatementSyntax)
							{
								shouldStop = true;
								break;
							}
						}
					}
					break;
				case TNode t:
					result.Add(t);

					if (visited is ReturnStatementSyntax)
					{
						shouldStop = true;
					}

					break;
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
			if (shouldStop) break;

			var visited = Visit(node);

			switch (visited)
			{
				case null:
					continue;
				case BlockSyntax block:
					foreach (var st in block.Statements)
					{
						if (st is TNode t)
						{
							result.Add(t);

							if (st is ReturnStatementSyntax)
							{
								shouldStop = true;
								break;
							}
						}
					}
					break;
				case TNode t:
					result.Add(t);

					if (visited is ReturnStatementSyntax)
					{
						shouldStop = true;
					}

					break;
			}
		}

		return SeparatedList(result);
	}
}

