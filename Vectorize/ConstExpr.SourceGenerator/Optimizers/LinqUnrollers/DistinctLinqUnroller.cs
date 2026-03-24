using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public class DistinctLinqUnroller : BaseLinqUnroller
{
	private const string SetName = "distinctSet";
	private const string SeenTrue = "seenTrue";
	private const string SeenFalse = "seenFalse";

	public override void UnrollAboveLoop(UnrolledLinqMethod method, List<StatementSyntax> statements)
	{
		var elementType = method.MethodSymbol.TypeArguments[0];

		switch (elementType.SpecialType)
		{
			case SpecialType.System_Boolean:
				statements.Add(CreateLocalDeclaration(SeenTrue, CreateLiteral(false)!));
				statements.Add(CreateLocalDeclaration(SeenFalse, CreateLiteral(false)!));
				break;

			case SpecialType.System_Byte:
			case SpecialType.System_SByte:
				// Span<bool> distinctSet = stackalloc bool[256];
				statements.Add(CreateStackAllocSpan<bool>(SetName, 256));
				break;

			case SpecialType.System_Int16:
			case SpecialType.System_UInt16:
			case SpecialType.System_Char:
				// Span<ulong> distinctSet = stackalloc ulong[1024]; (8 KB bitset for 65 536 values)
				statements.Add(CreateStackAllocSpan<ulong>(SetName, 1024));
				break;

			default:
			{
				var typeName = method.Model.Compilation.GetMinimalString(elementType);
				var capacityArg = GetCollectionSizeExpression(method.CollectionType);

				statements.Add(CreateLocalDeclaration(SetName,
					ObjectCreationExpression(IdentifierName($"HashSet<{typeName}>"), capacityArg)));
				break;
			}
		}
	}

	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		var element = IdentifierName(elementName.ToString());

		switch (method.MethodSymbol.TypeArguments[0].SpecialType)
		{
			case SpecialType.System_Boolean:
				AddBoolDistinctBody(statements, element, SeenTrue, SeenFalse);
				break;

			case SpecialType.System_Byte:
				AddSpanIndexDistinctBody(statements, element, SetName, castToByte: false);
				break;

			case SpecialType.System_SByte:
				AddSpanIndexDistinctBody(statements, element, SetName, castToByte: true);
				break;

			case SpecialType.System_UInt16:
			case SpecialType.System_Char:
				AddBitSetDistinctBody(statements, element, SetName, castToUShort: false);
				break;

			case SpecialType.System_Int16:
				AddBitSetDistinctBody(statements, element, SetName, castToUShort: true);
				break;

			default:
				statements.Add(IfStatement(
					LogicalNotExpression(
						CreateMethodInvocation(IdentifierName(SetName), "Add", element)),
					ContinueStatement()));
				break;
		}
	}
}