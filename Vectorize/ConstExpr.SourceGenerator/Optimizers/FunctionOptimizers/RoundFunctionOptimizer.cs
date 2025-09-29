using ConstExpr.Core.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class RoundFunctionOptimizer : BaseFunctionOptimizer
{
	public override bool TryOptimize(IMethodSymbol method, FloatingPointEvaluationMode floatingPointMode, IList<ExpressionSyntax> parameters, out SyntaxNode? result)
	{
		result = null;

		if (method.Name != "Round")
		{
			return false;
		}

		var containing = method.ContainingType?.ToString();

		if (containing is not "System.Math" and not "System.MathF")
		{
			return false;
		}

		var type = method.Parameters[0].Type;

		// We only simplify default arguments:
		// - digits == 0 can be dropped
		// - mode == MidpointRounding.ToEven can be dropped
		// Prefer not to reorder or duplicate the primary argument to keep evaluation semantics intact.
		if (parameters.Count is 2 or 3)
		{
			var arg0 = parameters[0];
			var args = new List<ExpressionSyntax>(parameters);

			bool IsZero(ExpressionSyntax e)
				=> e is LiteralExpressionSyntax { Token.Value: 0 }
					or LiteralExpressionSyntax { Token.Value: 0u }
					or LiteralExpressionSyntax { Token.Value: 0L }
					or LiteralExpressionSyntax { Token.Value: 0ul };

			bool IsToEven(ExpressionSyntax e)
			{
				// Accept fully qualified or identifier forms of MidpointRounding.ToEven
				return e is MemberAccessExpressionSyntax { Name.Identifier.Text: "ToEven" };
			}

			// For 3-arg: Round(x, digits, mode)
			if (args.Count == 3)
			{
				var digits = args[1];
				var mode = args[2];

				if (IsZero(digits) && IsToEven(mode))
				{
					result = CreateInvocation(type, "Round", arg0);
					return true;
				}
				if (IsToEven(mode))
				{
					result = CreateInvocation(type, "Round", arg0, digits);
					return true;
				}
				if (IsZero(digits))
				{
					result = CreateInvocation(type, "Round", arg0, mode);
					return true;
				}
			}
			// For 2-arg overloads: Round(x, digits) or Round(x, mode)
			else
			{
				var second = args[1];

				if (IsZero(second) || IsToEven(second))
				{
					result = CreateInvocation(type, "Round", arg0);
					return true;
				}
			}
		}

		result = CreateInvocation(type, "Round", parameters[0]);
		return true;
	}
}
