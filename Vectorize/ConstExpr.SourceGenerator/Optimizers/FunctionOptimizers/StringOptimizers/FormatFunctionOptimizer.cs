using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using ConstExpr.SourceGenerator.Models;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers
{
	/// <summary>
	/// Optimizes usages of <c>string.Format</c>. This optimizer:
	/// - Converts suitable <c>string.Format</c> calls with a constant format string into an interpolated string expression when it can safely be done (for example, <c>string.Format("Hello {0}", name)</c> -> <c>$"Hello {name}"</c>).
	/// - Ensures purity for repeated argument usages to avoid duplicating side-effects.
	/// - If conversion to an interpolated string is not possible or safe, the optimizer will not claim a change.
	/// </summary>
	/// <param name="instance">Optional syntax node instance provided by the optimizer infrastructure; may be null.</param>
	public class FormatFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "Format")
	{
		public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
		{
			result = null;

			if (!IsValidMethod(context.Method, out var stringType) || !context.Method.IsStatic)
			{
				return false;
			}

			// For simplicity, do not handle overloads that take an IFormatProvider
			if (context.Method.Parameters.Length > 0 && context.Method.Parameters[0].Type.Name == "IFormatProvider")
			{
				return false;
			}

			const int formatIndex = 0;

			if (context.VisitedParameters.Count <= formatIndex)
			{
				return false;
			}

			if (context.VisitedParameters[formatIndex] is not LiteralExpressionSyntax formatLiteral || !formatLiteral.IsKind(SyntaxKind.StringLiteralExpression))
			{
				return false;
			}

			var formatString = formatLiteral.Token.ValueText;

			// Collect following expressions; require purity to avoid side-effects
			var argExpressions = new List<ExpressionSyntax>();

			for (var i = 1; i < context.VisitedParameters.Count; i++)
			{
				argExpressions.Add(context.VisitedParameters[i]);
			}

			// First pass: scan the format string to count placeholder usages
			var usageCounts = new Dictionary<int, int>();
			var scanPos = 0;
			var scanFailed = false;

			while (scanPos < formatString.Length)
			{
				var ch = formatString[scanPos];

				if (ch == '{')
				{
					if (scanPos + 1 < formatString.Length && formatString[scanPos + 1] == '{')
					{
						scanPos += 2;
						continue;
					}

					scanPos++;
					var startIdx = scanPos;

					while (scanPos < formatString.Length && char.IsDigit(formatString[scanPos]))
					{
						scanPos++;
					}

					if (scanPos == startIdx)
					{
						scanFailed = true;
						break;
					}

					var idxText = formatString.Substring(startIdx, scanPos - startIdx);

					if (!Int32.TryParse(idxText, NumberStyles.None, CultureInfo.InvariantCulture, out var argIndex))
					{
						scanFailed = true;
						break;
					}

					// skip optional alignment
					if (scanPos < formatString.Length && formatString[scanPos] == ',')
					{
						scanPos++;

						while (scanPos < formatString.Length && formatString[scanPos] != ':' && formatString[scanPos] != '}')
						{
							scanPos++;
						}
					}

					// skip optional format
					if (scanPos < formatString.Length && formatString[scanPos] == ':')
					{
						scanPos++;

						while (scanPos < formatString.Length && formatString[scanPos] != '}')
						{
							scanPos++;
						}

						if (scanPos >= formatString.Length)
						{
							scanFailed = true;
							break;
						}
					}

					if (scanPos >= formatString.Length || formatString[scanPos] != '}')
					{
						scanFailed = true;
						break;
					}

					scanPos++;

					if (argIndex < 0)
					{
						scanFailed = true;
						break;
					}

					usageCounts[argIndex] = usageCounts.TryGetValue(argIndex, out var c) ? c + 1 : 1;
					continue;
				}

				if (ch == '}')
				{
					if (scanPos + 1 < formatString.Length && formatString[scanPos + 1] == '}')
					{
						scanPos += 2;
						continue;
					}

					scanFailed = true;
					break;
				}

				scanPos++;
			}

			if (scanFailed)
			{
				return false;
			}

			// Enforce purity only for args referenced more than once
			foreach (var kv in usageCounts)
			{
				if (kv.Key < 0 || kv.Key >= argExpressions.Count)
				{
					return false; // invalid index
				}

				if (kv.Value > 1)
				{
					var expr = argExpressions[kv.Key];

					if (!IsPure(expr))
					{
						return false;
					}
				}
			}

			// Build interpolated string content
			var contentBuilder = new StringBuilder();
			var pos = 0;

			while (pos < formatString.Length)
			{
				var ch = formatString[pos];

				if (ch == '{')
				{
					if (pos + 1 < formatString.Length && formatString[pos + 1] == '{')
					{
						// Escaped open brace
						contentBuilder.Append('{');
						pos += 2;

						continue;
					}

					// Placeholder
					pos++;
					var startIdx = pos;

					while (pos < formatString.Length && Char.IsDigit(formatString[pos]))
					{
						pos++;
					}

					if (pos == startIdx)
					{
						return false; // invalid placeholder
					}

					var idxText = formatString.Substring(startIdx, pos - startIdx);

					if (!Int32.TryParse(idxText, NumberStyles.None, CultureInfo.InvariantCulture, out var argIndex))
					{
						return false;
					}

					// optional alignment
					var alignmentPart = String.Empty;

					if (pos < formatString.Length && formatString[pos] == ',')
					{
						var alignStart = pos;

						pos++; // skip comma

						while (pos < formatString.Length && formatString[pos] != ':' && formatString[pos] != '}')
						{
							pos++;
						}

						alignmentPart = formatString.Substring(alignStart, pos - alignStart);
					}

					// optional format
					var formatPart = string.Empty;

					if (pos < formatString.Length && formatString[pos] == ':')
					{
						pos++; // skip ':'
						var fmtStart = pos;

						while (pos < formatString.Length && formatString[pos] != '}')
						{
							pos++;
						}

						if (pos >= formatString.Length)
						{
							return false;
						}

						formatPart = formatString.Substring(fmtStart, pos - fmtStart);
					}

					if (pos >= formatString.Length || formatString[pos] != '}')
					{
						return false;
					}

					pos++; // skip '}'

					if (argIndex < 0 || argIndex >= argExpressions.Count)
					{
						return false; // invalid index
					}

					var exprText = argExpressions[argIndex].ToFullString().Trim();

					contentBuilder.Append('{');
					contentBuilder.Append(exprText);

					if (!String.IsNullOrEmpty(alignmentPart))
					{
						contentBuilder.Append(alignmentPart);
					}

					if (!String.IsNullOrEmpty(formatPart))
					{
						contentBuilder.Append(':');
						// formatPart may contain '"' or '\\', we'll escape later when building the literal
						contentBuilder.Append(formatPart);
					}

					contentBuilder.Append('}');
					continue;
				}

				if (ch == '}')
				{
					if (pos + 1 < formatString.Length && formatString[pos + 1] == '}')
					{
						contentBuilder.Append('}');
						pos += 2;

						continue;
					}

					return false; // unmatched '}'
				}

				// regular char: escape backslash and double-quote for inclusion in a double-quoted string
				if (ch is '"' or '\\')
				{
					contentBuilder.Append('\\');
				}
				
				contentBuilder.Append(ch);

				pos++;
			}

			var content = contentBuilder.ToString();
			var exprTextFull = "\"" + content + "\"";

			try
			{
				var parsed = SyntaxFactory.ParseExpression(exprTextFull);

				if (parsed is InterpolatedStringExpressionSyntax)
				{
					result = parsed;
					return true;
				}

				return false;
			}
			catch
			{
				result = null;
				return false;
			}
		}
	}
}
