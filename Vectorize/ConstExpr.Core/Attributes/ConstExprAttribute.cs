﻿using ConstExpr.Core.Enumerators;
using System;

namespace ConstExpr.Core.Attributes;

/// <summary>
/// Marks a method, constructor, or type as eligible for constant expression evaluation
/// performed by the ConstExpr tooling or analyzers.
/// </summary>
/// <remarks>
/// Applying this attribute signals that the annotated member (or all members inside an
/// annotated type) should be considered for compile-time evaluation when possible.
/// Use <see cref="FloatingPointMode"/> to control floating‑point semantics (strict vs fast).
/// </remarks>
/// <seealso cref="FloatingPointEvaluationMode"/>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class ConstExprAttribute : Attribute
{
	/// <summary>
	/// Gets or sets the floating‑point evaluation mode used during constant expression processing.
	/// </summary>
	/// <value>
	/// Defaults to <see cref="FloatingPointEvaluationMode.Strict"/>.
	/// </value>
	public FloatingPointEvaluationMode FloatingPointMode { get; set; } = FloatingPointEvaluationMode.Strict;
}

