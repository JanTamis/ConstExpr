using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.LinqUnrollers;

public class OfTypeLinqUnroller : BaseLinqUnroller
{
	public override void UnrollLoopBody(UnrolledLinqMethod method, List<StatementSyntax> statements, ref ExpressionSyntax elementName)
	{
		if (method.MethodSymbol.TypeArguments.Length != 1)
		{
			return;
		}

		var targetType = method.MethodSymbol.TypeArguments[0].AsTypeSyntax();
		var newName = $"item_{elementName.GetDeterministicHashString()}";

		// if (item is not TargetType typedItem) continue;
		statements.Add(IfStatement(
			IsPatternExpression(elementName,
				UnaryPattern(Token(SyntaxKind.NotKeyword), DeclarationPattern(targetType, SingleVariableDesignation(Identifier(newName))))),
			ContinueStatement()));
		
		elementName = IdentifierName(newName);
	}
}