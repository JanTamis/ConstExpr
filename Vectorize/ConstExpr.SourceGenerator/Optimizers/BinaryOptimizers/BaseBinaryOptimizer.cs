using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Linq;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;

public abstract class BaseBinaryOptimizer
{
	public abstract BinaryOperatorKind Kind { get; }

	public abstract IEnumerable<IBinaryStrategy> GetStrategies();
}