using ConstExpr.Core.Enumerators;
using System;

namespace ConstExpr.Core.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class ConstExprAttribute : Attribute
{
	// public GenerationLevel Level { get; set; } = GenerationLevel.Balanced;
}

