using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.MultiplyStrategies;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Generic;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public class BinaryMultiplyOptimizer : BaseBinaryOptimizer
{
	public override BinaryOperatorKind Kind => BinaryOperatorKind.Multiply;

	public override IEnumerable<IBinaryStrategy> GetStrategies()
	{
		yield return new MultiplyByZeroStrategy();
		yield return new MultiplyIdentityElementStrategy();
		yield return new MultiplyByNegativeOneStrategy();
		yield return new MultiplyConstantFoldingStrategy();
		yield return new MultiplyByTwoToShiftRightStrategy();
		yield return new MultiplyByTwoToShiftLeftStrategy();
		yield return new MultiplyByTwoToAdditionRightStrategy();
		yield return new MultiplyByTwoToAdditionLeftStrategy();
		// yield return new MultiplyStrengthReductionRightStrategy();
		// yield return new MultiplyStrengthReductionLeftStrategy();
		yield return new MultiplyByPowerOfTwoRightStrategy();
		yield return new MultiplyByPowerOfTwoLeftStrategy();
		yield return new MultiplyDoubleNegationStrategy();
		yield return new MultiplyLeftNegationStrategy();
		yield return new MultiplyRightNegationStrategy();
	}
}