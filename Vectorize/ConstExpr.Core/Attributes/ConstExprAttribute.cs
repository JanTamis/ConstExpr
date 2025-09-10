using ConstExpr.Core.Enumerators;
using System;

namespace ConstExpr.Core.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Assembly, Inherited = false)]
public sealed class ConstExprAttribute : Attribute
{
	public GenerationLevel Level { get; set; } = GenerationLevel.Balanced;
}

