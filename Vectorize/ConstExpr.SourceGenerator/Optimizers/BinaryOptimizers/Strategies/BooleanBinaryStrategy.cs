using System;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;

public class BooleanBinaryStrategy : SpecialTypeBinaryStrategy
{
	public override SyntaxNode? Optimize(BinaryOptimizeContext context)
	{
		throw new NotImplementedException();
	}
	
	public override bool IsValidSpecialType(SpecialType specialType)
	{
		return specialType is SpecialType.System_Boolean;
	}
}
