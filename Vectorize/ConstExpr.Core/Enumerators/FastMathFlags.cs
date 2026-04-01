using System;

namespace ConstExpr.Core.Enumerators;

/// <summary>
/// Specifies the floating-point evaluation mode for constant expression processing.
/// Individual flags correspond to GCC/Clang fast-math sub-options and can be combined freely.
/// </summary>
/// <remarks>
/// Use <see cref="FastMath"/> to enable all optimisations at once (equivalent to C++ <c>-ffast-math</c>),
/// or compose individual flags for fine-grained control over which IEEE-754 rules may be relaxed.
/// </remarks>
[Flags]
public enum FastMathFlags
{
	/// <summary>
	/// Enforce strict IEEE 754 semantics (default). No fast-math transformations are applied.
	/// </summary>
	Strict = 0,

	/// <summary>
	/// Allow re-association of floating-point operands (<c>-fassociative-math</c>).
	/// Enables constant folding of floating-point binary expressions where the result may
	/// differ from strictly left-to-right IEEE 754 evaluation.
	/// </summary>
	AssociativeMath = 1 << 0,

	/// <summary>
	/// Assume that no NaN values occur at runtime (<c>-fno-honor-nans</c>).
	/// Enables math-function approximations and comparisons that produce incorrect
	/// results when given NaN inputs.
	/// </summary>
	NoNaN = 1 << 1,

	/// <summary>
	/// Assume that no infinite values occur at runtime (<c>-fno-honor-infinities</c>).
	/// Combined with <see cref="NoNaN"/>, permits finite-only math optimisations such as
	/// replacing <c>x * 0.0</c> with <c>0.0</c>.
	/// </summary>
	NoInfinity = 1 << 2,

	/// <summary>
	/// Ignore the sign of zero; treat <c>-0.0</c> as equal to <c>0.0</c> (<c>-fno-signed-zeros</c>).
	/// Allows simplifications such as <c>x + 0.0 → x</c> and <c>0.0 - x → -x</c>.
	/// </summary>
	NoSignedZero = 1 << 3,

	/// <summary>
	/// Allow replacing division by a constant with multiplication by its reciprocal (<c>-freciprocal-math</c>).
	/// For example, <c>x / 3.0</c> may be rewritten as <c>x * 0.333…</c>.
	/// Results may differ from IEEE 754 due to rounding of the reciprocal approximation.
	/// </summary>
	ReciprocalMath = 1 << 4,

	/// <summary>
	/// Assume the floating-point rounding mode is always round-to-nearest (<c>-fno-rounding-math</c>).
	/// Permits constant folding that would otherwise be incorrect under non-default rounding modes.
	/// </summary>
	RoundToNearest = 1 << 5,

	/// <summary>
	/// Assume that floating-point operations never raise hardware traps (<c>-fno-trapping-math</c>).
	/// Allows the generator to reorder or eliminate floating-point operations that could otherwise
	/// trigger a hardware floating-point exception.
	/// </summary>
	NoTrappingMath = 1 << 6,

	/// <summary>
	/// Permit fused-multiply-add (FMA) contraction (<c>-ffp-contract=fast</c>).
	/// Allows <c>a * b + c</c> to be emitted as a single <c>FMA</c> instruction, which may
	/// change the rounding behaviour compared to two separate operations.
	/// </summary>
	FusedMultiplyAdd = 1 << 7,

	/// <summary>
	/// Enable all fast-math optimisations — equivalent to C++ <c>-ffast-math</c>.
	/// Combines <see cref="AssociativeMath"/>, <see cref="NoNaN"/>, <see cref="NoInfinity"/>,
	/// <see cref="NoSignedZero"/>, <see cref="ReciprocalMath"/>, <see cref="RoundToNearest"/>,
	/// <see cref="NoTrappingMath"/>, and <see cref="FusedMultiplyAdd"/>.
	/// </summary>
	FastMath = AssociativeMath | NoNaN | NoInfinity | NoSignedZero
	           | ReciprocalMath | RoundToNearest | NoTrappingMath | FusedMultiplyAdd,
}