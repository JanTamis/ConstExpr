using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public class DistinctByLinqUnroller : BaseLinqUnroller
{
	private const string SetName = "distinctBySet";
	private const string KeyName = "distinctByKey";
	private const string SeenTrue = "distinctBySeenTrue";
	private const string SeenFalse = "distinctBySeenFalse";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		// The key type is the last type argument (the selector's return type).
		var keyType = method.MethodSymbol.TypeArguments[^1];

		switch (keyType.SpecialType)
		{
			case SpecialType.System_Boolean:
				statements.Add(CreateLocalDeclaration(SeenTrue, LiteralExpression(SyntaxKind.FalseLiteralExpression)));
				statements.Add(CreateLocalDeclaration(SeenFalse, LiteralExpression(SyntaxKind.FalseLiteralExpression)));
				break;

			case SpecialType.System_Byte:
			case SpecialType.System_SByte:
				// Span<bool> distinctBySet = stackalloc bool[256];
				statements.Add(CreateStackAllocSpan(SetName, PredefinedType(Token(SyntaxKind.BoolKeyword)), 256));
				break;

			case SpecialType.System_Int16:
			case SpecialType.System_UInt16:
			case SpecialType.System_Char:
				// Span<ulong> distinctBySet = stackalloc ulong[1024]; (8 KB bitset for 65 536 values)
				statements.Add(CreateStackAllocSpan(SetName, PredefinedType(Token(SyntaxKind.ULongKeyword)), 1024));
				break;

			default:
			{
				var typeName = method.Model.Compilation.GetMinimalString(keyType);
				var capacityArg = GetCollectionSizeExpression(method.CollectionType);
				var args = capacityArg is not null
					? ArgumentList(SingletonSeparatedList(Argument(capacityArg)))
					: ArgumentList();

				statements.Add(CreateLocalDeclaration(SetName,
					ObjectCreationExpression(IdentifierName($"HashSet<{typeName}>"))
						.WithArgumentList(args)));
				break;
			}
		}
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		if (!TryGetLambda(method.Parameters[0], out var lambda))
			return;

		var keyExpr = ReplaceLambda(method.Visit(lambda) as LambdaExpressionSyntax ?? lambda, IdentifierName(elementName.ToString()));
		if (keyExpr is null)
			return;

		var keyType = method.MethodSymbol.TypeArguments[^1];

		switch (keyType.SpecialType)
		{
			case SpecialType.System_Boolean:
				// var distinctByKey = <keyExpr>;  — cache to avoid double evaluation
				statements.Add(CreateLocalDeclaration(KeyName, keyExpr));
				AddBoolDistinctBody(statements, IdentifierName(KeyName), SeenTrue, SeenFalse);
				break;

			case SpecialType.System_Byte:
				statements.Add(CreateLocalDeclaration(KeyName, keyExpr));
				AddSpanIndexDistinctBody(statements, IdentifierName(KeyName), SetName, castToByte: false);
				break;

			case SpecialType.System_SByte:
				statements.Add(CreateLocalDeclaration(KeyName, keyExpr));
				AddSpanIndexDistinctBody(statements, IdentifierName(KeyName), SetName, castToByte: true);
				break;

			case SpecialType.System_UInt16:
			case SpecialType.System_Char:
				statements.Add(CreateLocalDeclaration(KeyName, keyExpr));
				AddBitSetDistinctBody(statements, IdentifierName(KeyName), SetName, castToUShort: false);
				break;

			case SpecialType.System_Int16:
				statements.Add(CreateLocalDeclaration(KeyName, keyExpr));
				AddBitSetDistinctBody(statements, IdentifierName(KeyName), SetName, castToUShort: true);
				break;

			default:
				// HashSet.Add fallback — keyExpr evaluated only once
				statements.Add(IfStatement(
					PrefixUnaryExpression(SyntaxKind.LogicalNotExpression,
						CreateMethodInvocation(IdentifierName(SetName), "Add", keyExpr)),
					ContinueStatement()));
				break;
		}
	}
}