using System;
using System.Collections.Generic;
using ConstExpr.SourceGenerator.Enums;

namespace ConstExpr.SourceGenerator.Models;

/// <summary>
///   Represents the outcome of a vectorization eligibility analysis performed by
///   <see cref="ConstExpr.SourceGenerator.Visitors.VectorizationEligibilityVisitor" />.
/// </summary>
public sealed class VectorizationResult
{
	/// <summary>
	///   Gets a value indicating whether the analyzed code is eligible for
	///   auto-vectorization.
	/// </summary>
	public bool IsVectorizable { get; }

	/// <summary>
	///   Gets the suggested SIMD vector width to use when rewriting the code.
	///   <see cref="VectorTypes.None" /> when <see cref="IsVectorizable" /> is
	///   <see langword="false" />.
	/// </summary>
	public VectorTypes SuggestedVectorType { get; }

	/// <summary>
	///   Gets the list of reasons explaining why the code is (not) vectorizable.
	///   When <see cref="IsVectorizable" /> is <see langword="true" /> this list is
	///   typically empty.  When it is <see langword="false" /> it contains
	///   human-readable diagnostics pointing to specific lines or constructs that
	///   prevented vectorization.
	/// </summary>
	public IReadOnlyList<string> Reasons { get; }

	/// <summary>
	///   Initializes a new instance of <see cref="VectorizationResult" />.
	/// </summary>
	public VectorizationResult(bool isVectorizable, VectorTypes suggestedVectorType, IReadOnlyList<string> reasons)
	{
		IsVectorizable = isVectorizable;
		SuggestedVectorType = suggestedVectorType;
		Reasons = reasons;
	}

	/// <inheritdoc />
	public override string ToString()
	{
		if (IsVectorizable)
		{
			return $"Vectorizable ({SuggestedVectorType})";
		}

		return $"Not vectorizable: {String.Join("; ", Reasons)}";
	}
}