using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public abstract class BaseLinqUnroller
{
	public virtual void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements) { }

	public virtual void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName) { }

	public virtual void UnrollUnderLoop(UnrolledLinqMethod method, List<StatementSyntax> statements) { }

	public virtual void CreateLoop(UnrolledLinqMethod method, ITypeSymbol collectionType, IList<StatementSyntax> statements, string collectionName, IList<StatementSyntax> resultStatements)
	{
		resultStatements.Add(ForEachStatement(IdentifierName("var"), "item", IdentifierName(collectionName), Block(statements)));
	}

	public virtual ExpressionSyntax GetCollectionElement(UnrolledLinqMethod method, string collectionName)
	{
		return IdentifierName("item");
	}

	protected static ExpressionSyntax InvertSyntax(ExpressionSyntax node)
	{
		switch (node)
		{
			// invert binary expressions with logical operators
			case BinaryExpressionSyntax binary:
			{
				return binary.Kind() switch
				{
					SyntaxKind.LogicalAndExpression => BinaryExpression(SyntaxKind.LogicalOrExpression, InvertSyntax(binary.Left), InvertSyntax(binary.Right)),
					SyntaxKind.LogicalOrExpression => BinaryExpression(SyntaxKind.LogicalAndExpression, InvertSyntax(binary.Left), InvertSyntax(binary.Right)),
					SyntaxKind.EqualsExpression => BinaryExpression(SyntaxKind.NotEqualsExpression, binary.Left, binary.Right),
					SyntaxKind.NotEqualsExpression => BinaryExpression(SyntaxKind.EqualsExpression, binary.Left, binary.Right),
					SyntaxKind.GreaterThanExpression => BinaryExpression(SyntaxKind.LessThanOrEqualExpression, binary.Left, binary.Right),
					SyntaxKind.GreaterThanOrEqualExpression => BinaryExpression(SyntaxKind.LessThanExpression, binary.Left, binary.Right),
					SyntaxKind.LessThanExpression => BinaryExpression(SyntaxKind.GreaterThanOrEqualExpression, binary.Left, binary.Right),
					SyntaxKind.LessThanOrEqualExpression => BinaryExpression(SyntaxKind.GreaterThanExpression, binary.Left, binary.Right),
					SyntaxKind.IsExpression => IsPatternExpression(binary.Left, UnaryPattern(Token(SyntaxKind.NotKeyword), TypePattern((TypeSyntax) binary.Right))),
					_ => PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, node)
				};
			}
			case LiteralExpressionSyntax literal:
			{
				return literal.Kind() switch
				{
					SyntaxKind.FalseLiteralExpression => LiteralExpression(SyntaxKind.TrueLiteralExpression),
					SyntaxKind.TrueLiteralExpression => LiteralExpression(SyntaxKind.FalseLiteralExpression),
					_ => PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, node)
				};
			}
		}

		return PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, node);
	}

	protected static ExpressionSyntax? ReplaceLambda(LambdaExpressionSyntax lambda, ExpressionSyntax replacement)
	{
		var lambdaParam = GetLambdaParameter(lambda);

		var body = lambda switch
		{
			SimpleLambdaExpressionSyntax simpleLambda => simpleLambda.Body,
			ParenthesizedLambdaExpressionSyntax parenthesizedLambda => parenthesizedLambda.Body,
			_ => throw new InvalidOperationException("Unsupported lambda expression type")
		};

		return ReplaceIdentifier(body, lambdaParam, replacement) as ExpressionSyntax;
	}

	protected static bool TryGetLambda(ExpressionSyntax? parameter, [NotNullWhen(true)] out LambdaExpressionSyntax? lambda)
	{
		if (parameter is LambdaExpressionSyntax lambdaExpression)
		{
			lambda = lambdaExpression;
			return true;
		}

		lambda = null;
		return false;
	}

	private static string GetLambdaParameter(LambdaExpressionSyntax lambda)
	{
		return lambda switch
		{
			SimpleLambdaExpressionSyntax simpleLambda => simpleLambda.Parameter.Identifier.Text,
			ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: > 0 } parenthesizedLambda
				=> parenthesizedLambda.ParameterList.Parameters[0].Identifier.Text,
			_ => throw new InvalidOperationException("Unsupported lambda expression type")
		};
	}

	private static SyntaxNode ReplaceIdentifier(CSharpSyntaxNode body, string oldIdentifier, ExpressionSyntax replacement)
	{
		var wrappedReplacement = replacement is BinaryExpressionSyntax or ConditionalExpressionSyntax
			? ParenthesizedExpression(replacement)
			: replacement;

		return new IdentifierReplacer(oldIdentifier, wrappedReplacement).Visit(body);
	}

	/// <summary>
	/// Checks if the invocation is made on an array type.
	/// </summary>
	protected static bool IsInvokedOnArray(ITypeSymbol type)
	{
		return type is IArrayTypeSymbol;
	}

	/// <summary>
	/// Checks if the invocation is made on a List&lt;T&gt; type.
	/// </summary>
	protected static bool IsInvokedOnCollection(ITypeSymbol type)
	{
		return type.AllInterfaces.Any(a => a.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IList_T);
	}

	protected ForStatementSyntax CreateForLoop(string collectionName, string indexName, string lengthName, BlockSyntax body, ExpressionSyntax initialElement)
	{
		return ForStatement(body)
			.WithDeclaration(VariableDeclaration(IdentifierName("var"))
				.WithVariables(SingletonSeparatedList(VariableDeclarator(indexName).WithInitializer(EqualsValueClause(initialElement))))
			)
			.WithCondition(BinaryExpression(SyntaxKind.LessThanExpression, CreateCastSyntax<uint>(IdentifierName(indexName)), CreateCastSyntax<uint>(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName(collectionName), IdentifierName(lengthName)))))
			.WithIncrementors(SingletonSeparatedList<ExpressionSyntax>(PostfixUnaryExpression(SyntaxKind.PostIncrementExpression, IdentifierName(indexName))));
	}

	protected ThrowStatementSyntax CreateThrowExpression<TException>(string message = "") where TException : Exception
	{
		return ThrowStatement(
			ObjectCreationExpression(
					IdentifierName(typeof(TException).Name))
				.WithArgumentList(
					ArgumentList(
						SingletonSeparatedList(
							Argument(
								LiteralExpression(
									SyntaxKind.StringLiteralExpression,
									Literal(message)))))));
	}
	
	protected LocalDeclarationStatementSyntax CreateLocalDeclaration(string variableName, TypeSyntax type, ExpressionSyntax initializer)
	{
		return LocalDeclarationStatement(
			VariableDeclaration(type)
				.WithVariables(
					SingletonSeparatedList(
						VariableDeclarator(variableName)
							.WithInitializer(EqualsValueClause(initializer)))));
	}

	protected LocalDeclarationStatementSyntax CreateLocalDeclaration(string variableName, ExpressionSyntax initializer)
	{
		return LocalDeclarationStatement(
			VariableDeclaration(IdentifierName("var"))
				.WithVariables(
					SingletonSeparatedList(
						VariableDeclarator(variableName)
							.WithInitializer(EqualsValueClause(initializer)))));
	}
	
	/// <summary>
	/// Generates a <c>Span&lt;T&gt;</c> local backed by <c>stackalloc</c>.
	/// Example: <c>Span&lt;bool&gt; name = stackalloc bool[size];</c>
	/// </summary>
	protected static LocalDeclarationStatementSyntax CreateStackAllocSpan(string name, TypeSyntax elementType, int size)
	{
		return LocalDeclarationStatement(
			VariableDeclaration(
					GenericName("Span")
						.WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(elementType))))
				.WithVariables(SingletonSeparatedList(
					VariableDeclarator(name)
						.WithInitializer(EqualsValueClause(
							StackAllocArrayCreationExpression(
								ArrayType(elementType)
									.WithRankSpecifiers(SingletonList(
										ArrayRankSpecifier(SingletonSeparatedList(
											CreateLiteral(size)))))))))));
	}

	/// <summary>
	/// Returns a size expression for the source collection: <c>collection.Length</c> for arrays,
	/// <c>collection.Count</c> for <c>IList&lt;T&gt;</c>-implementing collections, or <c>null</c>
	/// for plain <c>IEnumerable&lt;T&gt;</c> where the size is unknown at compile time.
	/// </summary>
	protected static ExpressionSyntax? GetCollectionSizeExpression(ITypeSymbol collectionType, string collectionParamName = "collection")
	{
		if (IsInvokedOnArray(collectionType))
		{
			return MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
				IdentifierName(collectionParamName), IdentifierName("Length"));
		}

		if (IsInvokedOnCollection(collectionType))
		{
			return MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
				IdentifierName(collectionParamName), IdentifierName("Count"));
		}

		return null;
	}

	/// <summary>
	/// Emits a two-flag distinct check for <c>bool</c> elements.
	/// <code>
	/// if (element) { if (seenTrue) continue; seenTrue = true; }
	/// else         { if (seenFalse) continue; seenFalse = true; }
	/// </code>
	/// </summary>
	protected static void AddBoolDistinctBody(List<StatementSyntax> statements, ExpressionSyntax element, string seenTrueName, string seenFalseName)
	{
		statements.Add(IfStatement(element,
			Block(
				IfStatement(IdentifierName(seenTrueName), ContinueStatement()),
				CreateAssignment(seenTrueName, CreateLiteral(true))),
			ElseClause(Block(
				IfStatement(IdentifierName(seenFalseName), ContinueStatement()),
				CreateAssignment(seenFalseName, CreateLiteral(true))))));
	}

	/// <summary>
	/// Emits a direct-index distinct check against a <c>Span&lt;bool&gt;</c> for 8-bit types.
	/// <code>
	/// if (span[index]) continue;
	/// span[index] = true;
	/// </code>
	/// </summary>
	protected static void AddSpanIndexDistinctBody(List<StatementSyntax> statements, ExpressionSyntax element, string spanName, bool castToByte)
	{
		ExpressionSyntax IndexExpression()
		{
			var index = castToByte
				? CastExpression(PredefinedType(Token(SyntaxKind.ByteKeyword)), element)
				: element;
			return ElementAccessExpression(IdentifierName(spanName))
				.WithArgumentList(BracketedArgumentList(SingletonSeparatedList(Argument(index))));
		}

		statements.Add(IfStatement(IndexExpression(), ContinueStatement()));
		statements.Add(ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
			IndexExpression(), CreateLiteral(true))));
	}

	/// <summary>
	/// Emits a bitset distinct check against a <c>Span&lt;ulong&gt;</c> for 16-bit types.
	/// <code>
	/// if ((span[index &gt;&gt; 6] &amp; (1UL &lt;&lt; (index &amp; 63))) != 0UL) continue;
	/// span[index &gt;&gt; 6] |= 1UL &lt;&lt; (index &amp; 63);
	/// </code>
	/// </summary>
	protected static void AddBitSetDistinctBody(List<StatementSyntax> statements, ExpressionSyntax element, string spanName, bool castToUShort)
	{
		statements.Add(IfStatement(
			BinaryExpression(SyntaxKind.NotEqualsExpression,
				ParenthesizedExpression(BinaryExpression(SyntaxKind.BitwiseAndExpression,
					BucketAccess(), BitMask())),
				CreateLiteral(0UL)),
			ContinueStatement()));

		statements.Add(ExpressionStatement(
			AssignmentExpression(SyntaxKind.OrAssignmentExpression,
				BucketAccess(), BitMask())));
		
		return;

		ExpressionSyntax IndexExpr() => castToUShort
			? CreateCastSyntax<ushort>(element)
			: element;

		ExpressionSyntax BucketAccess() => ElementAccessExpression(IdentifierName(spanName))
			.WithArgumentList(BracketedArgumentList(SingletonSeparatedList(Argument(
				BinaryExpression(SyntaxKind.RightShiftExpression,
					IndexExpr(),
					 CreateLiteral(6))))));

		ExpressionSyntax BitMask() => BinaryExpression(SyntaxKind.LeftShiftExpression,
			CreateLiteral(1UL),
			ParenthesizedExpression(BinaryExpression(SyntaxKind.BitwiseAndExpression,
				IndexExpr(),
				CreateLiteral(63))));
	}

	protected static ExpressionStatementSyntax CreateAssignment(string variableName, ExpressionSyntax value)
	{
		return ExpressionStatement(
			AssignmentExpression(
				SyntaxKind.SimpleAssignmentExpression,
				IdentifierName(variableName),
				value));
	}
	
	protected static InvocationExpressionSyntax CreateMethodInvocation(ExpressionSyntax target, string methodName, params ExpressionSyntax[] arguments)
	{
		return InvocationExpression(
			MemberAccessExpression(
				SyntaxKind.SimpleMemberAccessExpression,
				target,
				IdentifierName(methodName)))
			.WithArgumentList(
				ArgumentList(
					SeparatedList(arguments.Select(Argument))));
	}
}

file sealed class IdentifierReplacer(string identifier, ExpressionSyntax replacement) : CSharpSyntaxRewriter
{
	public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
	{
		return node.Identifier.Text == identifier ? replacement : base.VisitIdentifierName(node);
	}
}