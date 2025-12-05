using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using SourceGen.Utilities.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using static ConstExpr.SourceGenerator.Helpers.SyntaxHelpers;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
/// Expression visitor methods for the ConstExprPartialRewriter.
/// Handles literal, binary, unary, cast, parenthesized, conditional, and tuple expressions.
/// </summary>
public partial class ConstExprPartialRewriter
{
	public override SyntaxNode? VisitLiteralExpression(LiteralExpressionSyntax node)
	{
		if (TryGetLiteral(node.Token.Value, out var expression))
		{
			if (semanticModel.GetOperation(node) is IOperation { Parent: IConversionOperation conversion })
			{
				TryGetLiteral(ExecuteConversion(conversion, node.Token.Value), out expression);
			}

			return expression;
		}

		return node;
	}

	public override SyntaxNode? VisitBinaryExpression(BinaryExpressionSyntax node)
	{
		// Handle "is" type expressions (e.g., obj is int)
		if (node.IsKind(SyntaxKind.IsExpression))
		{
			return VisitIsTypeExpression(node);
		}

		var left = Visit(node.Left);
		var right = Visit(node.Right);

		var hasLeftValue = TryGetLiteralValue(node.Left, out var leftValue) || TryGetLiteralValue(left, out leftValue);
		var hasRightValue = TryGetLiteralValue(node.Right, out var rightValue) || TryGetLiteralValue(right, out rightValue);

		if (TryGetOperation(semanticModel, node, out IBinaryOperation? operation))
		{
			if (hasLeftValue && operation.LeftOperand is IConversionOperation leftConversion)
			{
				leftValue = ExecuteConversion(leftConversion, leftValue);
			}

			if (hasRightValue && operation.RightOperand is IConversionOperation rightConversion)
			{
				rightValue = ExecuteConversion(rightConversion, rightValue);
			}

			if (hasLeftValue && hasRightValue)
			{
				if (operation.OperatorMethod is not null
				    && loader.TryExecuteMethod(operation.OperatorMethod, null, new VariableItemDictionary(variables), [leftValue, rightValue], out var result))
				{
					return CreateLiteral(result);
				}

				return CreateLiteral(ObjectExtensions.ExecuteBinaryOperation(node.Kind(), leftValue, rightValue));
			}

			// Try algebraic/logical simplifications when one side is a constant and operator is built-in
			if (left is ExpressionSyntax leftExpr && right is ExpressionSyntax rightExpr)
			{
				if (TryOptimizeBinaryExpression(operation, leftExpr, rightExpr, out var optimized))
				{
					return optimized;
				}

				return node.WithLeft(leftExpr).WithRight(rightExpr);
			}
		}

		return node
			.WithLeft(left as ExpressionSyntax ?? node.Left)
			.WithRight(right as ExpressionSyntax ?? node.Right);
	}

	/// <summary>
	/// Handles the "is" type expression (e.g., obj is int).
	/// </summary>
	private SyntaxNode? VisitIsTypeExpression(BinaryExpressionSyntax node)
	{
		var visitedLeft = Visit(node.Left);
		var exprToEvaluate = visitedLeft ?? node.Left;

		if (TryGetConstantValue(semanticModel.Compilation, loader, exprToEvaluate, new VariableItemDictionary(variables), token, out var value))
		{
			ITypeSymbol? typeInfo = GetTypeFromRightSide(node.Right);

			if (typeInfo is not null)
			{
				var result = IsTypeMatchForBinaryIs(typeInfo, value);

				if (result.HasValue)
				{
					return CreateLiteral(result.Value);
				}
			}
		}

		return node.WithLeft(visitedLeft as ExpressionSyntax ?? node.Left);
	}

	/// <summary>
	/// Gets the type symbol from the right side of an "is" expression.
	/// </summary>
	private ITypeSymbol? GetTypeFromRightSide(ExpressionSyntax right)
	{
		if (right is not TypeSyntax typeSyntax)
		{
			return null;
		}

		var typeInfo = semanticModel.GetTypeInfo(typeSyntax, token).Type
		               ?? semanticModel.GetSymbolInfo(typeSyntax, token).Symbol as ITypeSymbol;

		// If we can't get the type from semantic model, try to resolve from PredefinedTypeSyntax
		if (typeInfo is null && typeSyntax is PredefinedTypeSyntax predefined)
		{
			typeInfo = predefined.Keyword.Kind() switch
			{
				SyntaxKind.BoolKeyword => semanticModel.Compilation.GetSpecialType(SpecialType.System_Boolean),
				SyntaxKind.ByteKeyword => semanticModel.Compilation.GetSpecialType(SpecialType.System_Byte),
				SyntaxKind.SByteKeyword => semanticModel.Compilation.GetSpecialType(SpecialType.System_SByte),
				SyntaxKind.ShortKeyword => semanticModel.Compilation.GetSpecialType(SpecialType.System_Int16),
				SyntaxKind.UShortKeyword => semanticModel.Compilation.GetSpecialType(SpecialType.System_UInt16),
				SyntaxKind.IntKeyword => semanticModel.Compilation.GetSpecialType(SpecialType.System_Int32),
				SyntaxKind.UIntKeyword => semanticModel.Compilation.GetSpecialType(SpecialType.System_UInt32),
				SyntaxKind.LongKeyword => semanticModel.Compilation.GetSpecialType(SpecialType.System_Int64),
				SyntaxKind.ULongKeyword => semanticModel.Compilation.GetSpecialType(SpecialType.System_UInt64),
				SyntaxKind.FloatKeyword => semanticModel.Compilation.GetSpecialType(SpecialType.System_Single),
				SyntaxKind.DoubleKeyword => semanticModel.Compilation.GetSpecialType(SpecialType.System_Double),
				SyntaxKind.DecimalKeyword => semanticModel.Compilation.GetSpecialType(SpecialType.System_Decimal),
				SyntaxKind.StringKeyword => semanticModel.Compilation.GetSpecialType(SpecialType.System_String),
				SyntaxKind.CharKeyword => semanticModel.Compilation.GetSpecialType(SpecialType.System_Char),
				SyntaxKind.ObjectKeyword => semanticModel.Compilation.GetSpecialType(SpecialType.System_Object),
				_ => null
			};
		}

		return typeInfo;
	}

	/// <summary>
	/// Checks if a type matches the given value for binary "is" expressions.
	/// </summary>
	private bool? IsTypeMatchForBinaryIs(ITypeSymbol typeInfo, object? val)
	{
		if (val is null)
		{
			// null only matches nullable reference types or Nullable<T>
			if (typeInfo.IsReferenceType)
			{
				return typeInfo.NullableAnnotation == NullableAnnotation.Annotated;
			}

			if (typeInfo.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
			{
				return true;
			}
			return false;
		}

		var valueType = val.GetType();
		var specialType = typeInfo.SpecialType;

		if (specialType != SpecialType.None)
		{
			return specialType switch
			{
				SpecialType.System_Boolean => valueType == typeof(bool),
				SpecialType.System_Char => valueType == typeof(char),
				SpecialType.System_SByte => valueType == typeof(sbyte),
				SpecialType.System_Byte => valueType == typeof(byte),
				SpecialType.System_Int16 => valueType == typeof(short),
				SpecialType.System_UInt16 => valueType == typeof(ushort),
				SpecialType.System_Int32 => valueType == typeof(int),
				SpecialType.System_UInt32 => valueType == typeof(uint),
				SpecialType.System_Int64 => valueType == typeof(long),
				SpecialType.System_UInt64 => valueType == typeof(ulong),
				SpecialType.System_Single => valueType == typeof(float),
				SpecialType.System_Double => valueType == typeof(double),
				SpecialType.System_Decimal => valueType == typeof(decimal),
				SpecialType.System_String => valueType == typeof(string),
				SpecialType.System_Object => true,
				_ => null
			};
		}

		// Handle Nullable<T> pattern
		if (typeInfo.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
		{
			var underlyingType = (typeInfo as INamedTypeSymbol)?.TypeArguments.FirstOrDefault();

			if (underlyingType is not null)
			{
				return IsTypeMatchForBinaryIs(underlyingType, val);
			}
		}

		return IsTypeMatchByName(typeInfo, valueType);
	}

	/// <summary>
	/// Checks if the type matches by name, interface, or inheritance.
	/// </summary>
	private static bool? IsTypeMatchByName(ITypeSymbol typeInfo, Type valueType)
	{
		var typeDisplayString = typeInfo.ToDisplayString();
		var typeFullName = valueType.FullName;
		var typeName = valueType.Name;

		// Check for exact match or name match
		if (string.Equals(typeDisplayString, typeFullName, StringComparison.Ordinal) ||
		    string.Equals(typeInfo.Name, typeName, StringComparison.Ordinal))
		{
			return true;
		}

		// Check for interface implementation
		if (typeInfo.TypeKind == TypeKind.Interface)
		{
			return valueType.GetInterfaces().Any(i =>
				i.FullName == typeDisplayString || i.Name == typeInfo.Name);
		}

		// Check inheritance chain
		var baseType = valueType.BaseType;

		while (baseType != null)
		{
			if (baseType.FullName == typeFullName || baseType.Name == typeName)
			{
				return true;
			}
			baseType = baseType.BaseType;
		}

		return false;
	}

	/// <summary>
	/// Tries to optimize a binary expression using algebraic/logical simplifications.
	/// </summary>
	private bool TryOptimizeBinaryExpression(IBinaryOperation operation, ExpressionSyntax leftExpr, ExpressionSyntax rightExpr, out SyntaxNode? result)
	{
		result = null;

		var opMethod = operation.OperatorMethod;
		var isBuiltIn = opMethod is null;

		// Boolean optimizations are always safe to apply
		var isBooleanOp = operation.OperatorKind is BinaryOperatorKind.ConditionalAnd or BinaryOperatorKind.ConditionalOr;

		// Integer optimizations are also always safe (no floating-point concerns)
		var isIntegerOp = operation.Type?.IsInteger() ?? false;

		if (isBuiltIn
		    && operation.Type is not null
		    && (isBooleanOp || isIntegerOp || attribute.FloatingPointMode == FloatingPointEvaluationMode.FastMath)
		    && TryOptimizeNode(operation.OperatorKind, operation.Type, leftExpr, operation.LeftOperand.Type, rightExpr, operation.RightOperand.Type, out result))
		{
			return true;
		}

		return false;
	}

	public override SyntaxNode? VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
	{
		var operand = Visit(node.Operand);

		// Support ++i and --i
		if (node.OperatorToken.IsKind(SyntaxKind.PlusPlusToken) || node.OperatorToken.IsKind(SyntaxKind.MinusMinusToken))
		{
			return VisitPrefixIncrementDecrement(node, operand);
		}

		// Handle logical negation: !true => false
		if (node.OperatorToken.IsKind(SyntaxKind.ExclamationToken)
		    && TryGetLiteralValue(operand, out var value)
		    && value is bool logicalBool)
		{
			return CreateLiteral(!logicalBool);
		}

		// Simplify double negatives: -(-x) becomes x
		if (node.OperatorToken.IsKind(SyntaxKind.MinusToken)
		    && operand is PrefixUnaryExpressionSyntax { OperatorToken: var innerOp } innerUnary
		    && innerOp.IsKind(SyntaxKind.MinusToken))
		{
			return innerUnary.Operand;
		}

		// Handle negation of numeric literals
		if (node.OperatorToken.IsKind(SyntaxKind.MinusToken) && TryGetLiteralValue(operand, out var numValue))
		{
			var negated = NegateValue(numValue);

			if (negated != null && TryGetLiteral(negated, out var lit))
			{
				return lit;
			}
		}

		if (semanticModel.GetOperation(node) is IUnaryOperation { ConstantValue.HasValue: true } operation)
		{
			if (operation.Parent is IConversionOperation conversionOperation
			    && TryGetLiteral(conversionOperation.ConstantValue.Value, out var lit))
			{
				return lit;
			}

			if (TryGetLiteral(operation.ConstantValue.Value, out lit))
			{
				return lit;
			}
		}

		return node.WithOperand(operand as ExpressionSyntax ?? node.Operand);
	}

	/// <summary>
	/// Handles prefix increment (++i) and decrement (--i) expressions.
	/// </summary>
	private SyntaxNode? VisitPrefixIncrementDecrement(PrefixUnaryExpressionSyntax node, SyntaxNode? operand)
	{
		if (node.Operand is IdentifierNameSyntax id && variables.TryGetValue(id.Identifier.Text, out var variable))
		{
			if (variable.IsInitialized && TryGetLiteralValue(id, out var current))
			{
				var updated = ComputeIncrementDecrement(node, current, variable);

				variable.Value = updated;
				variable.HasValue = true;

				// Prefix returns the updated value
				return TryGetLiteral(updated, out var lit) ? lit : node.WithOperand(id);
			}

			variable.IsAltered = true;
		}

		return node.WithOperand(operand as ExpressionSyntax ?? node.Operand);
	}

	/// <summary>
	/// Computes the result of an increment or decrement operation.
	/// </summary>
	private object? ComputeIncrementDecrement(ExpressionSyntax node, object? current, VariableItem variable)
	{
		object? updated = null;

		// Prefer operator method if available (overloaded ++/--)
		if (TryGetOperation(semanticModel, node, out IIncrementOrDecrementOperation? op) && op is not null)
		{
			try
			{
				if (loader.TryExecuteMethod(op.OperatorMethod, null, new VariableItemDictionary(variables), [current], out var res))
				{
					updated = res;
				}
			}
			catch { }
		}

		if (updated is null)
		{
			var isIncrement = node switch
			{
				PrefixUnaryExpressionSyntax prefix => prefix.OperatorToken.IsKind(SyntaxKind.PlusPlusToken),
				PostfixUnaryExpressionSyntax postfix => postfix.OperatorToken.IsKind(SyntaxKind.PlusPlusToken),
				_ => true
			};

			var st = variable.Type.SpecialType;
			var one = 1.ToSpecialType(st) ?? 1;
			var kind = isIncrement ? SyntaxKind.AddExpression : SyntaxKind.SubtractExpression;

			if (st == SpecialType.System_Char)
			{
				var i = Convert.ToInt32(current);
				updated = isIncrement ? i + 1 : i - 1;
				updated = Convert.ToChar(updated);
			}
			else
			{
				updated = ObjectExtensions.ExecuteBinaryOperation(kind, current, one) ?? current;
			}
		}

		return updated;
	}

	/// <summary>
	/// Negates a numeric value.
	/// </summary>
	private static object? NegateValue(object? value)
	{
		try
		{
			return value switch
			{
				int i => -i,
				long l => -l,
				float f => -f,
				double d => -d,
				decimal dec => -dec,
				short s => -s,
				sbyte sb => -sb,
				_ => null
			};
		}
		catch
		{
			return null;
		}
	}

	public override SyntaxNode? VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node)
	{
		// Support i++ and i--
		if (node.OperatorToken.IsKind(SyntaxKind.PlusPlusToken) || node.OperatorToken.IsKind(SyntaxKind.MinusMinusToken))
		{
			if (node.Operand is IdentifierNameSyntax id && variables.TryGetValue(id.Identifier.Text, out var variable))
			{
				if (variable.IsInitialized && TryGetLiteralValue(id, out var current))
				{
					var updated = ComputeIncrementDecrement(node, current, variable);

					// Postfix returns the original value, but updates the variable
					variable.Value = updated;
					variable.HasValue = true;

					return TryGetLiteral(current, out var lit) ? lit : node.WithOperand(id);
				}

				variable.IsAltered = true;
			}
		}

		return base.VisitPostfixUnaryExpression(node);
	}

	public override SyntaxNode? VisitParenthesizedExpression(ParenthesizedExpressionSyntax node)
	{
		var visitedExpression = Visit(node.Expression) as ExpressionSyntax ?? node.Expression;

		// Try to remove parentheses if possible
		if (node.CanRemoveParentheses(semanticModel, token)
		    || visitedExpression is ParenthesizedExpressionSyntax or IdentifierNameSyntax or LiteralExpressionSyntax
		       or InvocationExpressionSyntax or ObjectCreationExpressionSyntax or IsPatternExpressionSyntax
		       or InterpolatedStringExpressionSyntax)
		{
			return visitedExpression;
		}

		return node.WithExpression(visitedExpression);
	}

	public override SyntaxNode? VisitCastExpression(CastExpressionSyntax node)
	{
		if (semanticModel.TryGetSymbol(node.Type, out ITypeSymbol? symbol))
		{
			var expression = Visit(node.Expression);

			if (TryGetLiteralValue(expression, out var value) || TryGetLiteralValue(node.Expression, out value))
			{
				var result = ConvertToSpecialType(symbol.SpecialType, value);

				if (result is not null && TryGetLiteral(result, out var literal))
				{
					return literal;
				}

				// Handle non-special types via operator method
				if (symbol.SpecialType == SpecialType.None
				    && TryGetOperation(semanticModel, node, out IConversionOperation? operation)
				    && loader.TryExecuteMethod(operation.OperatorMethod, null, new VariableItemDictionary(variables), [value], out var opResult)
				    && TryGetLiteral(opResult, out literal))
				{
					return literal;
				}
			}

			return node.WithExpression(expression as ExpressionSyntax ?? node.Expression);
		}

		return base.VisitCastExpression(node);
	}

	/// <summary>
	/// Converts a value to the specified special type.
	/// </summary>
	private static object? ConvertToSpecialType(SpecialType specialType, object? value)
	{
		try
		{
			return specialType switch
			{
				SpecialType.System_Boolean => Convert.ToBoolean(value),
				SpecialType.System_Byte => Convert.ToByte(value),
				SpecialType.System_Char => Convert.ToChar(value),
				SpecialType.System_DateTime => Convert.ToDateTime(value),
				SpecialType.System_Decimal => Convert.ToDecimal(value),
				SpecialType.System_Double => Convert.ToDouble(value),
				SpecialType.System_Int16 => Convert.ToInt16(value),
				SpecialType.System_Int32 => Convert.ToInt32(value),
				SpecialType.System_Int64 => Convert.ToInt64(value),
				SpecialType.System_SByte => Convert.ToSByte(value),
				SpecialType.System_Single => Convert.ToSingle(value),
				SpecialType.System_String => Convert.ToString(value),
				SpecialType.System_UInt16 => Convert.ToUInt16(value),
				SpecialType.System_UInt32 => Convert.ToUInt32(value),
				SpecialType.System_UInt64 => Convert.ToUInt64(value),
				_ => null
			};
		}
		catch
		{
			return null;
		}
	}

	public override SyntaxNode? VisitConditionalExpression(ConditionalExpressionSyntax node)
	{
		var condition = Visit(node.Condition);
		var whenTrue = Visit(node.WhenTrue);
		var whenFalse = Visit(node.WhenFalse);

		if (TryGetLiteralValue(condition, out var value) && value is bool b)
		{
			return b ? whenTrue : whenFalse;
		}

		// Try optimization with the original node
		if (semanticModel.GetTypeInfo(node).Type is { } type)
		{
			var optimizer = new Optimizers.ConditionalOptimizers.ConditionalExpressionOptimizer
			{
				Condition = node.Condition,
				WhenTrue = node.WhenTrue,
				WhenFalse = node.WhenFalse,
				Type = type
			};

			if (optimizer.TryOptimize(loader, variables, out var optimized))
			{
				return Visit(optimized);
			}
		}

		return node
			.WithCondition(condition as ExpressionSyntax ?? node.Condition)
			.WithWhenTrue(whenTrue as ExpressionSyntax ?? node.WhenTrue)
			.WithWhenFalse(whenFalse as ExpressionSyntax ?? node.WhenFalse);
	}

	public override SyntaxNode? VisitTupleExpression(TupleExpressionSyntax node)
	{
		var arguments = node.Arguments
			.Select(arg => Visit(arg.Expression))
			.ToList();

		var constantArguments = arguments
			.WhereSelect<SyntaxNode?, object?>(TryGetLiteralValue)
			.ToArray();

		// If all tuple elements are constant, create a tuple literal
		if (constantArguments.Length == arguments.Count && constantArguments.Length > 0)
		{
			var literalArguments = constantArguments
				.Select(arg => CreateLiteral(arg))
				.ToArray();

			if (literalArguments.All(lit => lit is not null))
			{
				return TupleExpression(
					SeparatedList(literalArguments.Select(lit => Argument(lit!))));
			}
		}

		return node.WithArguments(
			SeparatedList(arguments
				.Select((arg, i) => Argument(arg as ExpressionSyntax ?? node.Arguments[i].Expression))));
	}

	public override SyntaxNode? VisitInterpolatedStringExpression(InterpolatedStringExpressionSyntax node)
	{
		var contents = node.Contents;
		var result = new List<InterpolatedStringContentSyntax>(contents.Count);

		foreach (var content in contents)
		{
			switch (content)
			{
				case InterpolatedStringTextSyntax text:
					result.Add(text);
					break;
				case InterpolationSyntax interp:
					result.Add(ProcessInterpolation(interp));
					break;
			}
		}

		if (result.All(a => a is InterpolatedStringTextSyntax))
		{
			var combinedText = string.Concat(result.OfType<InterpolatedStringTextSyntax>().Select(s => s.TextToken.ValueText));
			return LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(combinedText));
		}

		return node.WithContents(List(result));
	}

	/// <summary>
	/// Processes a single interpolation in an interpolated string.
	/// </summary>
	private InterpolatedStringContentSyntax ProcessInterpolation(InterpolationSyntax interp)
	{
		var visited = Visit(interp.Expression);

		if (TryGetLiteralValue(visited, out var value))
		{
			var str = value?.ToString() ?? string.Empty;
			var format = interp.FormatClause?.FormatStringToken.ValueText;

			if (value is IFormattable formattable && format?.Length > 0)
			{
				str = formattable.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
			}

			return InterpolatedStringText(
				Token(interp.GetLeadingTrivia(), SyntaxKind.InterpolatedStringTextToken, str, str, interp.GetTrailingTrivia()));
		}

		return interp.WithExpression(visited as ExpressionSyntax ?? interp.Expression);
	}
}

