namespace ConstExpr.Core.Enumerators;

// <summary>
// Specifies the floating?point evaluation mode for constant expression processing.
// </summary>
public enum FloatingPointEvaluationMode
{
	/// <summary>
	/// Enforce strict IEEE 754 semantics (default).
	/// </summary>
	Strict = 0,

	/// <summary>
	/// Permit non?strict (fast math) optimizations that may deviate from full IEEE 754 guarantees.
	/// </summary>
	FastMath = 1
}