using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using static ConstExpr.SourceGenerator.Helpers.SyntaxHelpers;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using SourceGen.Utilities.Extensions;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
/// Expression visitor methods for the ConstExprPartialRewriter.
/// Handles literal, binary, unary, cast, parenthesized, conditional, and tuple expressions.
/// </summary>
public partial class ConstExprPartialRewriter
{
	public override SyntaxNode? VisitLiteralExpression(LiteralExpressionSyntax node)
	{
		if (node.Token.Value is null)
		{
			return node;
		}

		if (TryGetLiteral(node.Token.Value, out var expression))
		{
			// Don't implicitly convert char literals to int - they should remain as char
			// to preserve their representation in pattern matching contexts
			if (semanticModel.GetOperation(node) is { Parent: IConversionOperation conversion }
			    && (node.Token.Value is not char || conversion.Type?.SpecialType != SpecialType.System_Int32))
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
			// Don't implicitly convert char values to int - they should remain as char
			// to preserve their representation in pattern matching contexts
			if (hasLeftValue && operation.LeftOperand is IConversionOperation leftConversion
			                 && !(leftValue is char && leftConversion.Type?.SpecialType == SpecialType.System_Int32))
			{
				leftValue = ExecuteConversion(leftConversion, leftValue);
			}

			if (hasRightValue && operation.RightOperand is IConversionOperation rightConversion
			                  && (rightValue is not char || rightConversion.Type?.SpecialType != SpecialType.System_Int32))
			{
				rightValue = ExecuteConversion(rightConversion, rightValue);
			}

			if (hasLeftValue && hasRightValue)
			{
				if (operation.OperatorMethod is not null
				    && loader.TryExecuteMethod(operation.OperatorMethod, null, new VariableItemDictionary(variables), [ leftValue, rightValue ], out var result))
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
					if (node.Parent is not BinaryExpressionSyntax && optimized is IsPatternExpressionSyntax pattern
					                                              && TryOptmizePattern(pattern, out var result))
					{
						return result;
					}

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

		if (TryGetConstantValue(semanticModel.Compilation, loader, exprToEvaluate, new VariableItemDictionary(variables), token, out var value)
		    && GetTypeFromRightSide(node.Right) is { } typeInfo
		    && IsTypeMatchForBinaryIs(typeInfo, value) is { } result)
		{
			return CreateLiteral(result);
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

			return typeInfo.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
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

		// Simplify double logical negation: !!x => x (when result is boolean)
		if (node.OperatorToken.IsKind(SyntaxKind.ExclamationToken)
		    && operand is PrefixUnaryExpressionSyntax { OperatorToken: var innerLogicalOp } innerLogicalUnary
		    && innerLogicalOp.IsKind(SyntaxKind.ExclamationToken))
		{
			return innerLogicalUnary.Operand;
		}

		// Simplify double negatives: -(-x) becomes x
		if (node.OperatorToken.IsKind(SyntaxKind.MinusToken)
		    && operand is PrefixUnaryExpressionSyntax { OperatorToken: var innerOp } innerUnary
		    && innerOp.IsKind(SyntaxKind.MinusToken))
		{
			return innerUnary.Operand;
		}

		// Simplify bitwise double negation: ~(~x) => x
		if (node.OperatorToken.IsKind(SyntaxKind.TildeToken)
		    && operand is PrefixUnaryExpressionSyntax { OperatorToken: var innerBitwiseOp } innerBitwiseUnary
		    && innerBitwiseOp.IsKind(SyntaxKind.TildeToken))
		{
			return innerBitwiseUnary.Operand;
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

		// Handle bitwise complement of numeric literals
		if (node.OperatorToken.IsKind(SyntaxKind.TildeToken) && TryGetLiteralValue(operand, out var bitwiseValue))
		{
			var complemented = BitwiseComplement(bitwiseValue);

			if (complemented != null && TryGetLiteral(complemented, out var lit))
			{
				return lit;
			}
		}

		if (semanticModel.GetOperation(node) is IUnaryOperation { ConstantValue.HasValue: true } operation
		    && (operation.Parent is IConversionOperation conversionOperation
			    && TryGetLiteral(conversionOperation.ConstantValue.Value, out var lit1) || TryGetLiteral(operation.ConstantValue.Value, out lit1)))
		{
			return lit1;
		}

		return node.WithOperand(operand as ExpressionSyntax ?? node.Operand);
	}

	/// <summary>
	/// Computes the bitwise complement of a value.
	/// </summary>
	private static object? BitwiseComplement(object? value)
	{
		try
		{
			return value switch
			{
				int i => ~i,
				long l => ~l,
				uint ui => ~ui,
				ulong ul => ~ul,
				short s => ~s,
				ushort us => ~us,
				byte b => ~b,
				sbyte sb => ~sb,
				_ => null
			};
		}
		catch
		{
			return null;
		}
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
		if (TryGetOperation(semanticModel, node, out IIncrementOrDecrementOperation? op)
		    && loader.TryExecuteMethod(op.OperatorMethod, null, new VariableItemDictionary(variables), [ current ], out var res))
		{
			updated = res;
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
		if ((node.OperatorToken.IsKind(SyntaxKind.PlusPlusToken) || node.OperatorToken.IsKind(SyntaxKind.MinusMinusToken))
		    && node.Operand is IdentifierNameSyntax id
		    && variables.TryGetValue(id.Identifier.Text, out var variable))
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

		return base.VisitPostfixUnaryExpression(node);
	}

	public override SyntaxNode? VisitParenthesizedExpression(ParenthesizedExpressionSyntax node)
	{
		var visitedExpression = Visit(node.Expression) as ExpressionSyntax ?? node.Expression;

		// Try to remove parentheses if possible
		if (node.CanRemoveParentheses(semanticModel, token)
		    || visitedExpression is ParenthesizedExpressionSyntax
			    or IdentifierNameSyntax
			    or LiteralExpressionSyntax
			    or InvocationExpressionSyntax
			    or ObjectCreationExpressionSyntax
			    or IsPatternExpressionSyntax
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
				    && loader.TryExecuteMethod(operation.OperatorMethod, null, new VariableItemDictionary(variables), [ value ], out var opResult)
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

	public override SyntaxNode? VisitConditionalAccessExpression(ConditionalAccessExpressionSyntax node)
	{
		var expression = Visit(node.Expression);

		// x?.Member where x is known non-null → x.Member
		if (TryGetLiteralValue(expression, out var value) && value is not null)
		{
			// Convert ?. to regular member access
			if (node.WhenNotNull is MemberBindingExpressionSyntax memberBinding)
			{
				var memberAccess = MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					expression as ExpressionSyntax ?? node.Expression,
					memberBinding.Name);

				return Visit(memberAccess);
			}

			if (node.WhenNotNull is ElementBindingExpressionSyntax elementBinding)
			{
				var elementAccess = ElementAccessExpression(
					expression as ExpressionSyntax ?? node.Expression,
					elementBinding.ArgumentList);

				return Visit(elementAccess);
			}

			// For ?.Method() we need to handle the member binding inside
			if (node.WhenNotNull is InvocationExpressionSyntax { Expression: MemberBindingExpressionSyntax methodBinding } invocation)
			{
				var memberAccess = MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					expression as ExpressionSyntax ?? node.Expression,
					methodBinding.Name);

				var newInvocation = InvocationExpression(memberAccess, invocation.ArgumentList);

				return Visit(newInvocation);
			}
		}

		// x?.Member where x is known null → null
		if (expression is LiteralExpressionSyntax { RawKind: (int) SyntaxKind.NullLiteralExpression })
		{
			return LiteralExpression(SyntaxKind.NullLiteralExpression);
		}

		var whenNotNull = Visit(node.WhenNotNull);

		return node
			.WithExpression(expression as ExpressionSyntax ?? node.Expression)
			.WithWhenNotNull(whenNotNull as ExpressionSyntax ?? node.WhenNotNull);
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
			var combinedText = String.Concat(result
				.OfType<InterpolatedStringTextSyntax>()
				.Select(s => s.TextToken.ValueText));

			return LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(combinedText));
		}

		if (result is [ InterpolationSyntax { FormatClause: null, AlignmentClause: null } singleInterp ])
		{
			return InvocationExpression(
					MemberAccessExpression(
						SyntaxKind.SimpleMemberAccessExpression,
						singleInterp.Expression,
						IdentifierName("ToString")))
				.WithArgumentList(ArgumentList());
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
				str = formattable.ToString(format, CultureInfo.InvariantCulture);
			}

			return InterpolatedStringText(
				Token(interp.GetLeadingTrivia(), SyntaxKind.InterpolatedStringTextToken, str, str, interp.GetTrailingTrivia()));
		}

		return interp.WithExpression(visited as ExpressionSyntax ?? interp.Expression);
	}

	private bool TryOptmizePattern(IsPatternExpressionSyntax pattern, out ExpressionSyntax? result)
	{
		result = null;

		// Only optimize binary patterns with OR
		if (pattern.Pattern is not BinaryPatternSyntax binaryPattern
		    || !TryGetTypeSymbol(pattern.Expression, out var type)
		    || !semanticModel.Compilation.TryGetUnsignedType(type, out var unsignedType))
		{
			return false;
		}

		var constants = new List<object?>();
		var isUnsigedType = IsEqualSymbol(type, unsignedType);

		// Extract all constant values from the OR pattern
		if (!TryExtractOrPatternConstants(binaryPattern, constants))
		{
			return false;
		}

		// Need at least 2 values to make optimization worthwhile
		if (constants.Count < 2)
		{
			return false;
		}

		constants = constants
			.Distinct()
			.OrderBy(o => o)
			.ToList();

		var results = new List<ExpressionSyntax>();
		var originalMinValue = constants[0];
		
		foreach (var cluster in constants!.GetClusterPatterns())
		{
			var minValue = cluster.Start;
			var maxValue = cluster.End;

			if (!TryGetLiteral(maxValue.Subtract(minValue).ToSpecialType(unsignedType.SpecialType), out var unsigneddiff))
			{
				return false;
			}

			var expression = isUnsigedType
				? pattern.Expression
				: CastExpression(
					ParseTypeName(semanticModel.Compilation.GetMinimalString(unsignedType)),
					pattern.Expression);

			if (!constants[0].IsNumericZero()
			    && TryGetLiteral(constants[0], out var minLit))
			{
				expression = isUnsigedType
					? BinaryExpression(SyntaxKind.SubtractExpression, pattern.Expression, minLit)
					: CastExpression(
						ParseTypeName(semanticModel.Compilation.GetMinimalString(unsignedType)),
						ParenthesizedExpression(BinaryExpression(SyntaxKind.SubtractExpression, pattern.Expression, minLit)));
			}

			// add range check 
			results.Add(BinaryExpression(SyntaxKind.LessThanOrEqualExpression, expression, unsigneddiff));

			if (!TryGetLiteral(cluster.Start, out var startExpression)
			    || !TryGetLiteral(cluster.End, out var endExpression)
			    || !TryGetLiteral(cluster.Step, out var stepExpression)
			    || !TryGetLiteral(cluster.Diff, out var diffExpression))
			{
				throw new InvalidOperationException("Failed to get literals for cluster optimization.");
			}

			cluster.StartExpression = startExpression;
			cluster.EndExpression = endExpression;
			cluster.StepExpression = stepExpression;
			cluster.DiffExpression = diffExpression;
			cluster.Expression = pattern.Expression;

			var zeroExpression = GetDefault(type);
			var oneExpression = CreateLiteral(GetOneLiteral(type));

			switch (cluster.Type)
			{
				case ObjectExtensions.ClusterType.Consecutive:
				{
					continue;
				}
				case ObjectExtensions.ClusterType.Step:
				{
					results.Add(GetStepSetExpression(cluster, zeroExpression));
					break;
				}
				case ObjectExtensions.ClusterType.PowerOfTwo:
				{
					results.Add(GetPowerOfTwoExpression(cluster, oneExpression!, zeroExpression));
					break;
				}
				case ObjectExtensions.ClusterType.Odd or ObjectExtensions.ClusterType.Even:
				{
					var memberAccess = MemberAccessExpression(
						SyntaxKind.SimpleMemberAccessExpression,
						ParseTypeName(type.Name),
						IdentifierName(cluster.Type == ObjectExtensions.ClusterType.Even ? "IsEvenInteger" : "IsOddInteger"));

					// Generate optimized expression: int.IsEvenInteger(x) or int.IsOddInteger(x)
					results.Add(InvocationExpression(memberAccess, ArgumentList([ Argument(pattern.Expression) ])));
					break;
				}
				case ObjectExtensions.ClusterType.Bitmask:
				{
					var bitType = (int) cluster.Step! switch
					{
						<= 32 => semanticModel.Compilation.GetSpecialType(SpecialType.System_UInt32),
						<= 64 => semanticModel.Compilation.GetSpecialType(SpecialType.System_UInt64),
						_ => unsignedType,
					};

					// Calculate the bitmask using relative positions from minValue
					var bitmask = 0.ToSpecialType(bitType.SpecialType);

					foreach (var value in cluster.Values)
					{
						// Calculate relative position: value - minValue
						var relativePos = value.Subtract(originalMinValue).ToSpecialType(bitType.SpecialType);
						bitmask = bitmask.Or(GetOneLiteral(bitType).LeftShift(relativePos));
					}

					// bitmask = bitmask.ToSpecialType(unsignedType.SpecialType);

					results.Add(GetBitmaskExpression(cluster, bitmask, oneExpression!, type));
					break;
				}
				default:
				{
					return false;
				}
			}
		}

		result = results[0];

		for (var i = 1; i < results.Count; i++)
		{
			result = BinaryExpression(i == 1 ? SyntaxKind.LogicalAndExpression : SyntaxKind.LogicalOrExpression, result, results[i]);
		}

		return true;

		object? GetOneLiteral(ITypeSymbol typeSymbol)
		{
			if (typeSymbol.SpecialType == SpecialType.System_Char)
			{
				return 1;
			}

			return 1.ToSpecialType(typeSymbol.SpecialType);
		}
	}

	/// <summary>
	/// Extracts all constant values from an OR pattern.
	/// </summary>
	private bool TryExtractOrPatternConstants(BinaryPatternSyntax pattern, List<object?> constants)
	{
		// Check if this is an OR pattern
		if (!pattern.OperatorToken.IsKind(SyntaxKind.OrKeyword))
		{
			return false;
		}

		// Recursively extract constants from left side
		if (!TryExtractPatternConstants(pattern.Left, constants))
		{
			return false;
		}

		// Recursively extract constants from right side
		if (!TryExtractPatternConstants(pattern.Right, constants))
		{
			return false;
		}

		return true;
	}

	/// <summary>
	/// Extracts constant values from a pattern (handles OR chains and individual constants).
	/// </summary>
	private bool TryExtractPatternConstants(PatternSyntax pattern, List<object?> constants)
	{
		return pattern switch
		{
			// Handle nested OR patterns
			BinaryPatternSyntax { OperatorToken: var opToken } binaryPattern when opToken.IsKind(SyntaxKind.OrKeyword) =>
				TryExtractOrPatternConstants(binaryPattern, constants),

			// Handle constant patterns
			ConstantPatternSyntax constPattern when
				TryGetConstantValue(semanticModel.Compilation, loader, constPattern.Expression, new VariableItemDictionary(variables), token, out var value) =>
				AddAndReturnTrue(constants, value),

			// Handle parenthesized patterns
			ParenthesizedPatternSyntax parenthesized =>
				TryExtractPatternConstants(parenthesized.Pattern, constants),

			// Unsupported pattern type for this optimization
			_ => false
		};

		bool AddAndReturnTrue<T>(List<T> list, T value)
		{
			list.Add(value);
			return true;
		}
	}

	// ({expression}) % {step} == 0
	private ExpressionSyntax GetStepSetExpression(ObjectExtensions.Cluster cluster, ExpressionSyntax zeroExpression)
	{
		return BinaryExpression(
			SyntaxKind.EqualsExpression,
			BinaryExpression(
				SyntaxKind.ModuloExpression,
				cluster.Expression,
				cluster.StepExpression),
			zeroExpression);
	}

	// ({expression} & ({expression} - 1)) == 0
	private ExpressionSyntax GetPowerOfTwoExpression(ObjectExtensions.Cluster cluster, ExpressionSyntax OneExpression, ExpressionSyntax zeroExpression)
	{
		return BinaryExpression(
			SyntaxKind.EqualsExpression,
			BinaryExpression(
				SyntaxKind.BitwiseAndExpression,
				cluster.Expression,
				ParenthesizedExpression(
					BinaryExpression(
						SyntaxKind.SubtractExpression,
						cluster.Expression,
						OneExpression))),
			zeroExpression);
	}

	private ExpressionSyntax GetBitmaskExpression(ObjectExtensions.Cluster cluster, object? bitmaskValue, ExpressionSyntax oneLiteral, ITypeSymbol maskType)
	{
		// Always create the subtraction expression for the shift: (target - minValue)
		var shiftTargetExpr = cluster.Expression;

		if (!cluster.Start.IsNumericZero())
		{
			shiftTargetExpr = BinaryExpression(
				SyntaxKind.SubtractExpression,
				cluster.Expression,
				cluster.StartExpression);
		}

		var maskLiteral = LiteralExpression(SyntaxKind.NumericLiteralExpression, bitmaskValue switch
		{
			int i => Literal($"0x{i:X}", i),
			uint ui => Literal($"0x{ui:X}u", ui),
			long l => Literal($"0x{l:X}L", l),
			ulong ul => Literal($"0x{ul:X}UL", ul),
			short s => Literal($"0x{s:X}", s),
			ushort us => Literal($"0x{us:X}", us),
			byte b => Literal($"0x{b:X}", b),
			sbyte sb => Literal($"0x{sb:X}", sb),
			char c => Literal($"0x{(ushort) c:X}", (ushort) c),
			_ => Literal(0)
		});

		// bitmask check: ({mask} >> {shiftExpression} & 1) != 0;
		var shiftRight = BinaryExpression(
			SyntaxKind.RightShiftExpression,
			maskLiteral,
			shiftTargetExpr);

		var andMask = BinaryExpression(
			SyntaxKind.BitwiseAndExpression,
			shiftRight,
			oneLiteral);

		return BinaryExpression(
			SyntaxKind.NotEqualsExpression,
			ParenthesizedExpression(andMask),
			GetDefault(maskType));
	}

	private ExpressionSyntax GetDefault(ITypeSymbol typeSymbol)
	{
		if (typeSymbol.SpecialType == SpecialType.System_Char)
		{
			return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0));
		}

		return typeSymbol.GetDefaultValue();
	}
}