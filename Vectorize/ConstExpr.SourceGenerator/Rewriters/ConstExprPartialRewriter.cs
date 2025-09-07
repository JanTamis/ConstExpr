using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Visitors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using static ConstExpr.SourceGenerator.Helpers.SyntaxHelpers;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Rewriters;

public class ConstExprPartialRewriter(SemanticModel semanticModel, MetadataLoader loader, Action<SyntaxNode?, Exception> exceptionHandler, IDictionary<string, VariableItem> variables, CancellationToken token) : CSharpSyntaxRewriter
{
	public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
	{
		if (variables.TryGetValue(node.Identifier.Text, out var value)
		    && value.HasValue)
		{
			if (TryGetOperation(semanticModel, node, out IOperation? operation) && operation.Parent is IConversionOperation conversion)
			{
				if (conversion.OperatorMethod is not null)
				{
					// If there's a conversion method, use it and produce a literal syntax node
					return CreateLiteral(semanticModel.Compilation.ExecuteMethod(loader, conversion.OperatorMethod, null, new VariableItemDictionary(variables), value));
				}

				// Convert the runtime value to the requested special type, then create a literal syntax node
				return conversion.Type?.SpecialType switch
				{
					SpecialType.System_Boolean => CreateLiteral(Convert.ToBoolean(value.Value)),
					SpecialType.System_Byte => CreateLiteral(Convert.ToByte(value.Value)),
					SpecialType.System_Char => CreateLiteral(Convert.ToChar(value.Value)),
					SpecialType.System_DateTime => CreateLiteral(Convert.ToDateTime(value.Value)),
					SpecialType.System_Decimal => CreateLiteral(Convert.ToDecimal(value.Value)),
					SpecialType.System_Double => CreateLiteral(Convert.ToDouble(value.Value)),
					SpecialType.System_Int16 => CreateLiteral(Convert.ToInt16(value.Value)),
					SpecialType.System_Int32 => CreateLiteral(Convert.ToInt32(value.Value)),
					SpecialType.System_Int64 => CreateLiteral(Convert.ToInt64(value.Value)),
					SpecialType.System_SByte => CreateLiteral(Convert.ToSByte(value.Value)),
					SpecialType.System_Single => CreateLiteral(Convert.ToSingle(value.Value)),
					SpecialType.System_String => CreateLiteral(Convert.ToString(value.Value)),
					SpecialType.System_UInt16 => CreateLiteral(Convert.ToUInt16(value.Value)),
					SpecialType.System_UInt32 => CreateLiteral(Convert.ToUInt32(value.Value)),
					SpecialType.System_UInt64 => CreateLiteral(Convert.ToUInt64(value.Value)),
					_ => CreateLiteral(value.Value),
				};
			}
			
			if (TryGetLiteral(value.Value, out var expression))
			{
				return expression;
			}

			return value.Value as SyntaxNode;
		}

		return node;
	}

	public override SyntaxNode? VisitLiteralExpression(LiteralExpressionSyntax node)
	{
		if (TryGetOperation(semanticModel, node, out IOperation? operation) && operation.Parent is IConversionOperation conversion)
		{
			var value = node.Token.Value;
			
			if (conversion.OperatorMethod is not null)
			{
				// If there's a conversion method, use it and produce a literal syntax node
				return CreateLiteral(semanticModel.Compilation.ExecuteMethod(loader, conversion.OperatorMethod, null, new VariableItemDictionary(variables), value));
			}

			// Convert the runtime value to the requested special type, then create a literal syntax node
			return conversion.Type?.SpecialType switch
			{
				SpecialType.System_Boolean => CreateLiteral(Convert.ToBoolean(value)),
				SpecialType.System_Byte => CreateLiteral(Convert.ToByte(value)),
				SpecialType.System_Char => CreateLiteral(Convert.ToChar(value)),
				SpecialType.System_DateTime => CreateLiteral(Convert.ToDateTime(value)),
				SpecialType.System_Decimal => CreateLiteral(Convert.ToDecimal(value)),
				SpecialType.System_Double => CreateLiteral(Convert.ToDouble(value)),
				SpecialType.System_Int16 => CreateLiteral(Convert.ToInt16(value)),
				SpecialType.System_Int32 => CreateLiteral(Convert.ToInt32(value)),
				SpecialType.System_Int64 => CreateLiteral(Convert.ToInt64(value)),
				SpecialType.System_SByte => CreateLiteral(Convert.ToSByte(value)),
				SpecialType.System_Single => CreateLiteral(Convert.ToSingle(value)),
				SpecialType.System_String => CreateLiteral(Convert.ToString(value)),
				SpecialType.System_UInt16 => CreateLiteral(Convert.ToUInt16(value)),
				SpecialType.System_UInt32 => CreateLiteral(Convert.ToUInt32(value)),
				SpecialType.System_UInt64 => CreateLiteral(Convert.ToUInt64(value)),
				_ => node,
			};
		}

		if (TryGetLiteral(node.Token.Value, out var expression))
		{
			return expression;
		}
		
		return node;
	}

	public override SyntaxNode? VisitBlock(BlockSyntax node)
	{
		return node.WithStatements(VisitList(node.Statements));
	}

	public override SyntaxList<TNode> VisitList<TNode>(SyntaxList<TNode> list)
	{
		var items = list
			.Select(Visit)
			.Where(w => w is not null)
			.OfType<TNode>()
			.SelectMany(s => s is BlockSyntax blockSyntax ? blockSyntax.Statements.OfType<TNode>() : [s]);

		return List(items);
	}

	public override SyntaxNode? VisitBinaryExpression(BinaryExpressionSyntax node)
	{
		var left = Visit(node.Left);
		var right = Visit(node.Right);

		object? leftValue = null, rightValue = null;
		
		var hasLeftValue = left is LiteralExpressionSyntax { Token.Value: var lv } && (leftValue = lv) != null;
		var hasRightValue = right is LiteralExpressionSyntax { Token.Value: var rv } && (rightValue = rv) != null;
		
		if (hasLeftValue && hasRightValue)
		{
			return CreateLiteral(ObjectExtensions.ExecuteBinaryOperation(MapSyntaxKindToOperatorKind(node.Kind()), leftValue, rightValue));
		}
		
		// Try algebraic/logical simplifications when one side is a constant and operator is built-in.
		// We avoid transforms that would duplicate or skip evaluation of non-constant operands.
		
		if (left is ExpressionSyntax leftExpr
		    && right is ExpressionSyntax rightExpr
		    && TryGetOperation(semanticModel, node, out IBinaryOperation? operation))
		{
			var opMethod = operation.OperatorMethod; // null => built-in operator
		
			var isBuiltIn = opMethod is null;
			// var leftIsPureVarOrLiteral = operation.LeftOperand is not null && IsVarOrLiteralOperation(operation.LeftOperand);
			// var rightIsPureVarOrLiteral = operation.RightOperand is not null && IsVarOrLiteralOperation(operation.RightOperand);
		
			if (isBuiltIn)
			{
				// Numeric identities
				if (IsNumericType(operation.LeftOperand.Type)
				    && IsNumericType(operation.RightOperand.Type))
				{
					switch (operation.OperatorKind)
					{
						case BinaryOperatorKind.Add:
							if (hasRightValue && rightValue.IsNumericZero()) return leftExpr;
							if (hasLeftValue && leftValue.IsNumericZero()) return rightExpr;
							break;
						case BinaryOperatorKind.Subtract:
							if (hasRightValue && rightValue.IsNumericZero()) return leftExpr;
							if (hasLeftValue && leftValue.IsNumericZero()) return PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, Parens(rightExpr));
							break;
						case BinaryOperatorKind.Multiply:
							if (hasRightValue && rightValue.IsNumericOne()) return leftExpr;
							if (hasLeftValue && leftValue.IsNumericOne()) return rightExpr;
		
							// x * 0 => 0 and 0 * x => 0 (only for non-floating numeric types to avoid NaN/-0.0 semantics)
							var nonFloating = IsNonFloatingNumeric(operation.LeftOperand.Type) && IsNonFloatingNumeric(operation.RightOperand.Type);
		
							if (nonFloating && hasRightValue && rightValue.IsNumericZero())
							{
								return CreateLiteral(0.ToSpecialType(operation.Type.SpecialType));
							}
		
							if (nonFloating && hasLeftValue && leftValue.IsNumericZero())
							{
								return CreateLiteral(0.ToSpecialType(operation.Type.SpecialType));
							}
		
							// 2 * x => (x + x), x * 2 => (x + x) when x is safe to duplicate
							if (hasLeftValue && leftValue.IsNumericTwo() && IsSafeToDuplicate(operation.RightOperand))
							{
								var dup = BinaryExpression(SyntaxKind.AddExpression, rightExpr, rightExpr);
								return Parens(dup);
							}
		
							if (hasRightValue && rightValue.IsNumericTwo() && IsSafeToDuplicate(operation.LeftOperand))
							{
								var dup = BinaryExpression(SyntaxKind.AddExpression, leftExpr, leftExpr);
								return Parens(dup);
							}
							break;
						case BinaryOperatorKind.Divide:
							if (hasRightValue && rightValue.IsNumericOne()) return leftExpr;
							break;
						case BinaryOperatorKind.ExclusiveOr:
							// integral XOR 0 => x
							if (hasRightValue && rightValue.IsNumericZero()) return leftExpr;
							if (hasLeftValue && leftValue.IsNumericZero()) return rightExpr;
							break;
						case BinaryOperatorKind.Or:
							// x | 0 => x
							if (hasRightValue && rightValue.IsNumericZero()) return leftExpr;
							if (hasLeftValue && leftValue.IsNumericZero()) return rightExpr;
							break;
					}
				}
		
				// Boolean logical identities
				if (IsBoolType(operation.LeftOperand.Type)
				    && IsBoolType(operation.RightOperand.Type))
				{
					switch (operation.OperatorKind)
					{
						case BinaryOperatorKind.ConditionalAnd: // &&
							if (hasRightValue && rightValue is true) return leftExpr; // x && true => x
							if (hasLeftValue && leftValue is true) return rightExpr; // true && x => x
							if (hasLeftValue && leftValue is false) return CreateLiteral(false); // false && x => false
							break;
						case BinaryOperatorKind.ConditionalOr: // ||
							if (hasRightValue && rightValue is false) return leftExpr; // x || false => x
							if (hasLeftValue && leftValue is false) return rightExpr; // false || x => x
							if (hasLeftValue && leftValue is true) return CreateLiteral(true); // true || x => true
							break;
						case BinaryOperatorKind.And: // & (bool)
							if (hasRightValue && rightValue is true) return leftExpr; // x & true => x
							if (hasLeftValue && leftValue is true) return rightExpr; // true & x => x
							break; // avoid collapsing to false to preserve evaluation of the other side
						case BinaryOperatorKind.Or: // | (bool)
							if (hasRightValue && rightValue is false) return leftExpr; // x | false => x
							if (hasLeftValue && leftValue is false) return rightExpr; // false | x => x
							break; // avoid collapsing to true to preserve evaluation of the other side
						case BinaryOperatorKind.ExclusiveOr: // ^ (bool)
							if (hasRightValue && rightValue is false) return leftExpr; // x ^ false => x
							if (hasLeftValue && leftValue is false) return rightExpr; // false ^ x => x
							if (hasRightValue && rightValue is true) return PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, Parens(leftExpr)); // x ^ true => !x
							if (hasLeftValue && leftValue is true) return PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, Parens(rightExpr)); // true ^ x => !x
							break;
						case BinaryOperatorKind.Equals:
							if (hasRightValue && rightValue is bool rb)
							{
								return rb
									? leftExpr // x == true => x
									: PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, Parens(leftExpr)); // x == false => !x
							}
		
							if (hasLeftValue && leftValue is bool lb)
							{
								return lb
									? rightExpr // true == x => x
									: PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, Parens(rightExpr)); // false == x => !x
							}
							break;
						case BinaryOperatorKind.NotEquals:
							if (hasRightValue && rightValue is bool rbn)
							{
								return rbn
									? PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, Parens(leftExpr)) // x != true => !x
									: leftExpr; // x != false => x
							}
		
							if (hasLeftValue && leftValue is bool lbn)
							{
								return lbn
									? PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, Parens(rightExpr)) // true != x => !x
									: rightExpr; // false != x => x
							}
							break;
					}
				}
			}
		
			var result = node
				.WithLeft(leftExpr)
				.WithRight(rightExpr);
		
			return result;
		}

		return node;

		static BinaryOperatorKind MapSyntaxKindToOperatorKind(SyntaxKind kind)
		{
			return kind switch
			{
				SyntaxKind.AddExpression => BinaryOperatorKind.Add,
				SyntaxKind.SubtractExpression => BinaryOperatorKind.Subtract,
				SyntaxKind.MultiplyExpression => BinaryOperatorKind.Multiply,
				SyntaxKind.DivideExpression => BinaryOperatorKind.Divide,
				SyntaxKind.LeftShiftExpression => BinaryOperatorKind.LeftShift,
				SyntaxKind.RightShiftExpression => BinaryOperatorKind.RightShift,
				SyntaxKind.ModuloExpression => BinaryOperatorKind.Remainder,
				SyntaxKind.ExclusiveOrExpression => BinaryOperatorKind.ExclusiveOr,
				SyntaxKind.BitwiseAndExpression => BinaryOperatorKind.And,
				SyntaxKind.BitwiseOrExpression => BinaryOperatorKind.Or,
				SyntaxKind.LogicalAndExpression => BinaryOperatorKind.ConditionalAnd,
				SyntaxKind.LogicalOrExpression => BinaryOperatorKind.ConditionalOr,
				SyntaxKind.EqualsExpression => BinaryOperatorKind.Equals,
				SyntaxKind.NotEqualsExpression => BinaryOperatorKind.NotEquals,
				SyntaxKind.GreaterThanExpression => BinaryOperatorKind.GreaterThan,
				SyntaxKind.GreaterThanOrEqualExpression => BinaryOperatorKind.GreaterThanOrEqual,
				SyntaxKind.LessThanExpression => BinaryOperatorKind.LessThan,
				SyntaxKind.LessThanOrEqualExpression => BinaryOperatorKind.LessThanOrEqual,
				_ => BinaryOperatorKind.None,
			};
		}

		bool IsVarOrLiteralOperation(IOperation op)
		{
			switch (op)
			{
				case ILocalReferenceOperation:
				case IParameterReferenceOperation:
				case ILiteralOperation:
					return true;
				case IParenthesizedOperation p:
					return p.Operand is not null && IsVarOrLiteralOperation(p.Operand);
				case IConversionOperation c:
					return c.Operand is not null && IsVarOrLiteralOperation(c.Operand);
				default:
					return false;
			}
		}

		// Stricter check for duplication safety: only locals/parameters/literals (allow parentheses but NO conversions)
		bool IsSafeToDuplicate(IOperation op)
		{
			switch (op)
			{
				case ILocalReferenceOperation:
				case IParameterReferenceOperation:
				case ILiteralOperation:
					return true;
				case IParenthesizedOperation p:
					return p.Operand is not null && IsSafeToDuplicate(p.Operand);
				default:
					return false;
			}
		}

		ExpressionSyntax Parens(ExpressionSyntax e)
		{
			return e is ParenthesizedExpressionSyntax
				? e
				: ParenthesizedExpression(e);
		}

		bool IsNonFloatingNumeric(ITypeSymbol? t)
		{
			return t is not null && IsNumericType(t) && t.SpecialType != SpecialType.System_Single && t.SpecialType != SpecialType.System_Double;
		}

		bool IsBoolType(ITypeSymbol? t)
		{
			return t?.SpecialType == SpecialType.System_Boolean;
		}

		bool IsNumericType(ITypeSymbol? t)
		{
			return t is not null && t.SpecialType switch
			{
				SpecialType.System_Byte or SpecialType.System_SByte or SpecialType.System_Int16 or SpecialType.System_UInt16 or
					SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64 or
					SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal => true,
				_ => false
			};
		}
	}

	public override SyntaxNode? VisitVariableDeclarator(VariableDeclaratorSyntax node)
	{
		var value = Visit(node.Initializer?.Value);

		if (TryGetOperation(semanticModel, node, out IVariableDeclaratorOperation? operation))
		{
			var name = operation.Symbol.Name;

			if (!variables.TryGetValue(name, out var item))
			{
				item = new VariableItem(operation.Type ?? operation.Symbol.Type, true, value);
				variables.Add(name, item);
			}

			if (value is IdentifierNameSyntax nameSyntax)
			{
				item.Value = nameSyntax;
				item.IsInitialized = true;
			}
			else if (operation.Initializer is null && operation.Symbol is ILocalSymbol local)
			{
				item.Value = local.Type.GetDefaultValue();
				item.IsInitialized = false;
			}
			else if (TryGetConstantValue(semanticModel.Compilation, loader, value, new VariableItemDictionary(variables), token, out var result))
			{
				item.Value = result;
				item.IsInitialized = true;
			}
			else
			{
				item.HasValue = false;
				item.IsInitialized = true;
			}

			return node.WithInitializer(node.Initializer.WithValue(value as ExpressionSyntax));
		}

		return base.VisitVariableDeclarator(node);
	}

	public override SyntaxNode? VisitIfStatement(IfStatementSyntax node)
	{
		var condition = Visit(node.Condition);

		if (condition is LiteralExpressionSyntax { Token.Value: bool b })
		{
			if (b)
			{
				return Visit(node.Statement);
			}
			
			return node.Else is not null
				? Visit(node.Else.Statement)
				: null;
		}
		
		
		var statement = Visit(node.Statement);
		var @else = Visit(node.Else);
		
		return node
			.WithCondition(condition as ExpressionSyntax)
			.WithStatement(statement as StatementSyntax)
			.WithElse(@else as ElseClauseSyntax);
	}

	public override SyntaxNode? VisitAssignmentExpression(AssignmentExpressionSyntax node)
	{
		// Do not visit the left/target to avoid turning assignable expressions into constants.
		var visitedRight = Visit(node.Right);
		var rightExpr = visitedRight as ExpressionSyntax ?? node.Right;

		if (node.Left is IdentifierNameSyntax { Identifier.Text: var name} && variables.TryGetValue(name, out var variable))
		{
			if (!variable.IsInitialized)
			{
				if (rightExpr is IdentifierNameSyntax nameSyntax)
				{
					variable.Value = nameSyntax;
					variable.HasValue = true;
				}
				else if (TryGetConstantValue(semanticModel.Compilation, loader, rightExpr, new VariableItemDictionary(variables), token, out var value))
				{
					variable.Value = value;
					variable.HasValue = true;
				}
				else
				{
					variable.HasValue = false;
				}

				variable.IsInitialized = true;

				var result = LocalDeclarationStatement(VariableDeclaration(ParseTypeName("var"), SingletonSeparatedList(
					VariableDeclarator(Identifier(name))
						.WithInitializer(EqualsValueClause(rightExpr)))
				));

				return result;
			}

			if (TryGetConstantValue(semanticModel.Compilation, loader, rightExpr, new VariableItemDictionary(variables), token, out var tempValue))
			{
				variable.Value = tempValue;

				if (TryGetLiteral(tempValue, out var literal))
				{
					rightExpr = literal;
				}
			}
			else
			{
				variable.HasValue = false;
			}
		}

		return node.WithRight(rightExpr);
	}

	private StatementSyntax ToStatementSyntax(IEnumerable<SyntaxNode> nodes)
	{
		var items = nodes
			.SelectMany<SyntaxNode, SyntaxNode>(s => s is BlockSyntax block ? block.Statements : new[] { s })
			.OfType<StatementSyntax>()
			.ToList();

		if (items.Count == 1)
		{
			return items[0];
		}

		return Block(items);
	}
}