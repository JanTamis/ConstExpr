using ConstExpr.Core.Enumerators;
using System;

namespace ConstExpr.Core.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class ConstEvalAttribute : Attribute
{
	/// <summary>
	/// Gets or sets the floating‑point evaluation mode used during constant expression processing.
	/// </summary>
	/// <value>
	/// Defaults to <see cref="FloatingPointEvaluationMode.Strict"/>.
	/// </value>
	public FloatingPointEvaluationMode FloatingPointMode { get; set; } = FloatingPointEvaluationMode.Strict;

	/// <summary>
	/// Gets or sets the maximum number of loop iterations to unroll during constant expression evaluation.
	/// </summary>
	/// <value>
	/// Defaults to 32. Set to 0 to disable loop unrolling.
	/// </value>
	public uint MaxUnrollIterations { get; set; } = 32;
}
