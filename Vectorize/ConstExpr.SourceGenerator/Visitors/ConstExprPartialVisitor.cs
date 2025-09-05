using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using SourceGen.Utilities.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;

namespace ConstExpr.SourceGenerator.Visitors;

public class ConstExprPartialVisitor(Compilation compilation, MetadataLoader loader, Action<IOperation?, Exception> exceptionHandler, CancellationToken token) : OperationVisitor<IDictionary<string, VariableItem>, SyntaxNode>
{
	public override SyntaxNode? DefaultVisit(IOperation operation, IDictionary<string, VariableItem> argument)
	{
		if (operation.ConstantValue is { HasValue: true, Value: var value } && SyntaxHelpers.TryGetLiteral(value, out var expression))
		{
			return expression;
		}

		// exceptionHandler(operation, new NotImplementedException($"Operation of type {operation.Kind} is not supported."));

		return operation.Syntax;
	}

	public override SyntaxNode? VisitExpressionStatement(IExpressionStatementOperation operation, IDictionary<string, VariableItem> argument)
	{
		return Visit(operation.Operation, argument);
	}

	public override SyntaxNode? VisitBlock(IBlockOperation operation, IDictionary<string, VariableItem> argument)
	{
		var statements = new List<StatementSyntax>();

		foreach (var child in operation.ChildOperations)
		{
			var visited = Visit(child, argument);
			var kind = child.Kind;

			if (visited is null)
			{
				continue;
			}

			switch (visited)
			{
				case BlockSyntax block:
					// Flatten nested blocks
					statements.AddRange(block.Statements);
					break;

				case StatementSyntax stmt:
					statements.Add(stmt);
					break;

				case ExpressionSyntax expr:
					// Ensure expressions become statements inside blocks
					statements.Add(SyntaxFactory.ExpressionStatement(expr));
					break;

				default:
					// Ignore anything that isn't a statement or expression
					break;
			}
		}

		return SyntaxFactory.Block(statements);
	}

	public override SyntaxNode? VisitParameterReference(IParameterReferenceOperation operation, IDictionary<string, VariableItem> argument)
	{
		if (argument.TryGetValue(operation.Parameter.Name, out var value) && value.HasValue && SyntaxHelpers.TryGetLiteral(value.Value, out var expression))
		{
			return expression;
		}

		return operation.Syntax;
	}

	public override SyntaxNode? VisitLocalReference(ILocalReferenceOperation operation, IDictionary<string, VariableItem> argument)
	{
		if (argument.TryGetValue(operation.Local.Name, out var value) 
			&& value.HasValue 
			&& SyntaxHelpers.TryGetLiteral(value.Value, out var expression))
		{
			return expression;
		}

		return operation.Syntax;
	}

	public override SyntaxNode? VisitBinaryOperator(IBinaryOperation operation, IDictionary<string, VariableItem> argument)
	{
		if (operation.Syntax is BinaryExpressionSyntax binary)
		{
			var left = Visit(operation.LeftOperand, argument);
			var right = Visit(operation.RightOperand, argument);

			var hasLeftValue = SyntaxHelpers.TryGetConstantValue(compilation, loader, left, new VariableItemDictionary(argument), token, out var leftValue);
			var hasRightValue = SyntaxHelpers.TryGetConstantValue(compilation, loader, right, new VariableItemDictionary(argument), token, out var rightValue);

			if (hasLeftValue && hasRightValue)
			{
				return SyntaxHelpers.CreateLiteral(ObjectExtensions.ExecuteBinaryOperation(operation.OperatorKind, leftValue, rightValue));
			}

			// Try algebraic/logical simplifications when one side is a constant and operator is built-in.
			// We avoid transforms that would duplicate or skip evaluation of non-constant operands.
			var leftExpr = (ExpressionSyntax?)left;
			var rightExpr = (ExpressionSyntax?)right;

			if (leftExpr is not null && rightExpr is not null)
			{
				var opMethod = (operation as IBinaryOperation).OperatorMethod; // null => built-in operator
				var resultType = operation.Type;

				var isBuiltIn = opMethod is null;
				bool IsBoolType(ITypeSymbol? t) => t?.SpecialType == SpecialType.System_Boolean;
				bool IsNumericType(ITypeSymbol? t) => t is not null && t.SpecialType switch
				{
					SpecialType.System_Byte or SpecialType.System_SByte or SpecialType.System_Int16 or SpecialType.System_UInt16 or
					SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64 or
					SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal => true,
					_ => false
				};

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

				var leftIsPureVarOrLiteral = operation.LeftOperand is not null && IsVarOrLiteralOperation(operation.LeftOperand);
				var rightIsPureVarOrLiteral = operation.RightOperand is not null && IsVarOrLiteralOperation(operation.RightOperand);

				ExpressionSyntax Parens(ExpressionSyntax e) => e is ParenthesizedExpressionSyntax ? e : SyntaxFactory.ParenthesizedExpression(e);

				if (isBuiltIn)
				{
					// Numeric identities
					if (IsNumericType(operation.LeftOperand.Type) 
						&& IsNumericType(operation.RightOperand.Type))
					{
						switch (operation.OperatorKind)
						{
							case BinaryOperatorKind.Add:
								if (hasRightValue && IsNumericZero(rightValue) && leftIsPureVarOrLiteral) return leftExpr;
								if (hasLeftValue && IsNumericZero(leftValue) && rightIsPureVarOrLiteral) return rightExpr;
								break;
							case BinaryOperatorKind.Subtract:
								if (hasRightValue && IsNumericZero(rightValue) && leftIsPureVarOrLiteral) return leftExpr;
								if (hasLeftValue && IsNumericZero(leftValue) && rightIsPureVarOrLiteral) return SyntaxFactory.PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, Parens(rightExpr));
								break;
							case BinaryOperatorKind.Multiply:
								if (hasRightValue && IsNumericOne(rightValue) && leftIsPureVarOrLiteral) return leftExpr;
								if (hasLeftValue && IsNumericOne(leftValue) && rightIsPureVarOrLiteral) return rightExpr;
								// x * 0 => 0 and 0 * x => 0 (only for non-floating numeric types to avoid NaN/-0.0 semantics)
								bool IsNonFloatingNumeric(ITypeSymbol? t) => t is not null && IsNumericType(t) && t.SpecialType != SpecialType.System_Single && t.SpecialType != SpecialType.System_Double;
								var nonFloating = IsNonFloatingNumeric(operation.LeftOperand.Type) && IsNonFloatingNumeric(operation.RightOperand.Type);
								if (nonFloating && hasRightValue && IsNumericZero(rightValue) && leftIsPureVarOrLiteral)
								{
									return SyntaxHelpers.CreateLiteral(0.ToSpecialType(operation.Type.SpecialType));
								}
								if (nonFloating && hasLeftValue && IsNumericZero(leftValue) && rightIsPureVarOrLiteral)
								{
									return SyntaxHelpers.CreateLiteral(0.ToSpecialType(operation.Type.SpecialType));
								}
								// 2 * x => (x + x), x * 2 => (x + x) when x is safe to duplicate
								if (hasLeftValue && IsNumericTwo(leftValue) && IsSafeToDuplicate(operation.RightOperand))
								{
									var dup = SyntaxFactory.BinaryExpression(SyntaxKind.AddExpression, rightExpr, rightExpr);
									return Parens(dup);
								}
								if (hasRightValue && IsNumericTwo(rightValue) && IsSafeToDuplicate(operation.LeftOperand))
								{
									var dup = SyntaxFactory.BinaryExpression(SyntaxKind.AddExpression, leftExpr, leftExpr);
									return Parens(dup);
								}
								break;
							case BinaryOperatorKind.Divide:
								if (hasRightValue && IsNumericOne(rightValue) && leftIsPureVarOrLiteral) return leftExpr;
								break;
							case BinaryOperatorKind.ExclusiveOr:
								// integral XOR 0 => x
								if (hasRightValue && IsNumericZero(rightValue) && leftIsPureVarOrLiteral) return leftExpr;
								if (hasLeftValue && IsNumericZero(leftValue) && rightIsPureVarOrLiteral) return rightExpr;
								break;
							case BinaryOperatorKind.Or:
								// x | 0 => x
								if (hasRightValue && IsNumericZero(rightValue) && leftIsPureVarOrLiteral) return leftExpr;
								if (hasLeftValue && IsNumericZero(leftValue) && rightIsPureVarOrLiteral) return rightExpr;
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
								if (hasRightValue && rightValue is true && leftIsPureVarOrLiteral) return leftExpr; // x && true => x
								if (hasLeftValue && leftValue is true && rightIsPureVarOrLiteral) return rightExpr;  // true && x => x
								if (hasLeftValue && leftValue is false && rightIsPureVarOrLiteral) return SyntaxHelpers.CreateLiteral(false); // false && x => false
								break;
							case BinaryOperatorKind.ConditionalOr: // ||
								if (hasRightValue && rightValue is false && leftIsPureVarOrLiteral) return leftExpr; // x || false => x
								if (hasLeftValue && leftValue is false && rightIsPureVarOrLiteral) return rightExpr;  // false || x => x
								if (hasLeftValue && leftValue is true && rightIsPureVarOrLiteral) return SyntaxHelpers.CreateLiteral(true); // true || x => true
								break;
							case BinaryOperatorKind.And: // & (bool)
								if (hasRightValue && rightValue is true && leftIsPureVarOrLiteral) return leftExpr; // x & true => x
								if (hasLeftValue && leftValue is true && rightIsPureVarOrLiteral) return rightExpr;  // true & x => x
								break; // avoid collapsing to false to preserve evaluation of the other side
							case BinaryOperatorKind.Or: // | (bool)
								if (hasRightValue && rightValue is false && leftIsPureVarOrLiteral) return leftExpr; // x | false => x
								if (hasLeftValue && leftValue is false && rightIsPureVarOrLiteral) return rightExpr;  // false | x => x
								break; // avoid collapsing to true to preserve evaluation of the other side
							case BinaryOperatorKind.ExclusiveOr: // ^ (bool)
								if (hasRightValue && rightValue is false && leftIsPureVarOrLiteral) return leftExpr; // x ^ false => x
								if (hasLeftValue && leftValue is false && rightIsPureVarOrLiteral) return rightExpr;  // false ^ x => x
								if (hasRightValue && rightValue is true && leftIsPureVarOrLiteral) return SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, Parens(leftExpr)); // x ^ true => !x
								if (hasLeftValue && leftValue is true && rightIsPureVarOrLiteral) return SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, Parens(rightExpr)); // true ^ x => !x
								break;
							case BinaryOperatorKind.Equals:
								if (hasRightValue && rightValue is bool rb && leftIsPureVarOrLiteral)
								{
									return rb
										? leftExpr // x == true => x
										: SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, Parens(leftExpr)); // x == false => !x
								}
								if (hasLeftValue && leftValue is bool lb && rightIsPureVarOrLiteral)
								{
									return lb
										? rightExpr // true == x => x
										: SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, Parens(rightExpr)); // false == x => !x
								}
								break;
							case BinaryOperatorKind.NotEquals:
								if (hasRightValue && rightValue is bool rbn && leftIsPureVarOrLiteral)
								{
									return rbn
										? SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, Parens(leftExpr)) // x != true => !x
										: leftExpr; // x != false => x
								}
								if (hasLeftValue && leftValue is bool lbn && rightIsPureVarOrLiteral)
								{
									return lbn
										? SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, Parens(rightExpr)) // true != x => !x
										: rightExpr; // false != x => x
								}
								break;
						}
					}
				}
			}

			return binary
				.WithLeft((ExpressionSyntax)left!)
				.WithRight((ExpressionSyntax)right!);
		}

		return operation.Syntax;
	}

	public override SyntaxNode? VisitVariableDeclarationGroup(IVariableDeclarationGroupOperation operation, IDictionary<string, VariableItem> argument)
	{
		if (operation.Syntax is LocalDeclarationStatementSyntax local)
		{
			var declarations = operation.Declarations
				.Select(decl => Visit(decl, argument))
				.OfType<VariableDeclarationSyntax>()
				.ToList();

			if (declarations.Count == 0)
			{
				return null;
			}

			return SyntaxFactory.LocalDeclarationStatement(SyntaxFactory.VariableDeclaration(declarations.First().Type, SyntaxFactory.SeparatedList(declarations.SelectMany(d => d.Variables))));
		}

		return operation.Syntax;
	}

	public override SyntaxNode? VisitVariableDeclaration(IVariableDeclarationOperation operation, IDictionary<string, VariableItem> argument)
	{
		if (operation.Syntax is VariableDeclarationSyntax variable)
		{
			var declarators = operation.Declarators
				.Select(d => Visit(d, argument))
				.OfType<VariableDeclaratorSyntax>()
				.ToList();

			if (declarators.Count == 0)
			{
				return null;
			}

			return variable.WithVariables(SyntaxFactory.SeparatedList(declarators));
		}


		return operation.Syntax;
	}

	public override SyntaxNode? VisitVariableDeclarator(IVariableDeclaratorOperation operation, IDictionary<string, VariableItem> argument)
	{
		if (operation.Syntax is VariableDeclaratorSyntax variable)
		{
			var result = (EqualsValueClauseSyntax)Visit(operation.Initializer, argument);
			var item = new VariableItem(operation.Symbol.Type, SyntaxHelpers.TryGetConstantValue(compilation, loader, result?.Value, new VariableItemDictionary(argument), token, out var value), value);

			if (operation.Initializer is null && operation.Symbol is ILocalSymbol local)
			{
				item.Value = local.Type.GetDefaultValue();
				item.HasValue = true;
			}
			
			argument.Add(operation.Symbol.Name, item);

			return variable.WithInitializer(result);
		}

		return operation.Syntax;
	}

	public override SyntaxNode? VisitVariableInitializer(IVariableInitializerOperation operation, IDictionary<string, VariableItem> argument)
	{
		if (operation.Syntax is EqualsValueClauseSyntax syntax)
		{
			return syntax.WithValue(Visit(operation.Value, argument) as ExpressionSyntax);
		}

		return operation.Syntax;
	}

	public override SyntaxNode? VisitConversion(IConversionOperation operation, IDictionary<string, VariableItem> argument)
	{
		var operand = Visit(operation.Operand, argument);
		var conversion = operation.Type;

		if (SyntaxHelpers.TryGetConstantValue(compilation, loader, operand, new VariableItemDictionary(argument), token, out var value))
		{
			if (operation.OperatorMethod is not null)
			{
				// If there's a conversion method, use it and produce a literal syntax node
				return SyntaxHelpers.CreateLiteral(compilation.ExecuteMethod(loader, operation.OperatorMethod, null, new VariableItemDictionary(argument), value));
			}

			// Convert the runtime value to the requested special type, then create a literal syntax node
			return conversion?.SpecialType switch
			{
				SpecialType.System_Boolean => SyntaxHelpers.CreateLiteral(Convert.ToBoolean(value)),
				SpecialType.System_Byte => SyntaxHelpers.CreateLiteral(Convert.ToByte(value)),
				SpecialType.System_Char => SyntaxHelpers.CreateLiteral(Convert.ToChar(value)),
				SpecialType.System_DateTime => SyntaxHelpers.CreateLiteral(Convert.ToDateTime(value)),
				SpecialType.System_Decimal => SyntaxHelpers.CreateLiteral(Convert.ToDecimal(value)),
				SpecialType.System_Double => SyntaxHelpers.CreateLiteral(Convert.ToDouble(value)),
				SpecialType.System_Int16 => SyntaxHelpers.CreateLiteral(Convert.ToInt16(value)),
				SpecialType.System_Int32 => SyntaxHelpers.CreateLiteral(Convert.ToInt32(value)),
				SpecialType.System_Int64 => SyntaxHelpers.CreateLiteral(Convert.ToInt64(value)),
				SpecialType.System_SByte => SyntaxHelpers.CreateLiteral(Convert.ToSByte(value)),
				SpecialType.System_Single => SyntaxHelpers.CreateLiteral(Convert.ToSingle(value)),
				SpecialType.System_String => SyntaxHelpers.CreateLiteral(Convert.ToString(value)),
				SpecialType.System_UInt16 => SyntaxHelpers.CreateLiteral(Convert.ToUInt16(value)),
				SpecialType.System_UInt32 => SyntaxHelpers.CreateLiteral(Convert.ToUInt32(value)),
				SpecialType.System_UInt64 => SyntaxHelpers.CreateLiteral(Convert.ToUInt64(value)),
				SpecialType.System_Object => SyntaxHelpers.CreateLiteral(value),
				_ => operand,
			};
		}

		return operand;
	}

	public override SyntaxNode? VisitInvocation(IInvocationOperation operation, IDictionary<string, VariableItem> argument)
	{
		if (operation.Syntax is InvocationExpressionSyntax invocation)
		{
			var targetMethod = operation.TargetMethod;
			var instance = Visit(operation.Instance, argument);

			var arguments = operation.Arguments
				.Select(arg => Visit(arg.Value, argument));

			var constantArguments = arguments
				.Where(w => SyntaxHelpers.TryGetConstantValue(compilation, loader, w, new VariableItemDictionary(argument), token, out _))
				.Select(s => SyntaxHelpers.GetConstantValue(compilation, loader, s, new VariableItemDictionary(argument), token))
				.ToArray();

			if (constantArguments.Length == operation.Arguments.Length)
			{
				try
				{
					if (instance is null)
					{
						return SyntaxHelpers.CreateLiteral(compilation.ExecuteMethod(loader, targetMethod, null, new VariableItemDictionary(argument), constantArguments));
					}

					if (SyntaxHelpers.TryGetConstantValue(compilation, loader, instance, new VariableItemDictionary(argument), token, out var instanceValue))
					{
						return SyntaxHelpers.CreateLiteral(compilation.ExecuteMethod(loader, targetMethod, instanceValue, new VariableItemDictionary(argument), constantArguments));
					}
				}
				catch (Exception)
				{

				}
			}

			return invocation
				.WithArgumentList(invocation.ArgumentList
					.WithArguments(SyntaxFactory.SeparatedList(arguments.Select(s => SyntaxFactory.Argument((ExpressionSyntax)s)))));
		}

		return operation.Syntax;
	}

	public override SyntaxNode? VisitConditional(IConditionalOperation operation, IDictionary<string, VariableItem> argument)
	{
		if (operation.Syntax is ConditionalExpressionSyntax conditional)
		{
			var condition = Visit(operation.Condition, argument);

			if (SyntaxHelpers.TryGetConstantValue(compilation, loader, condition, new VariableItemDictionary(argument), token, out var value))
			{
				switch (value)
				{
					case true:
						return Visit(operation.WhenTrue, argument);
					case false:
						return Visit(operation.WhenFalse, argument);
				}
			}

			return conditional
				.WithCondition((ExpressionSyntax)condition!)
				.WithWhenTrue((ExpressionSyntax)Visit(operation.WhenTrue, argument)!)
				.WithWhenFalse((ExpressionSyntax)Visit(operation.WhenFalse, argument)!);
		}

		if (operation.Syntax is IfStatementSyntax ifStatement)
		{
			var visitedCondition = Visit(operation.Condition, argument);

			if (SyntaxHelpers.TryGetConstantValue(compilation, loader, visitedCondition, new VariableItemDictionary(argument), token, out var condValue))
			{
				switch (condValue)
				{
					case true:
						// Return only the 'then' part
						return Visit(operation.WhenTrue, argument);
					case false:
					{
						// Return only the 'else' part (if present); otherwise drop the whole if
						if (operation.WhenFalse is null)
						{
							return null;
						}

						return Visit(operation.WhenFalse, argument);
					}
				}
			}

			// Not a constant condition: rebuild the if-statement with visited components
			var conditionExpr = visitedCondition as ExpressionSyntax ?? ifStatement.Condition;
			var thenStmt = Visit(operation.WhenTrue, argument) as StatementSyntax ?? ifStatement.Statement;

			var updatedIf = ifStatement
				.WithCondition(conditionExpr)
				.WithStatement(thenStmt);

			if (operation.WhenFalse is not null)
			{
				var elseStmt = Visit(operation.WhenFalse, argument) as StatementSyntax ?? ifStatement.Else?.Statement;

				if (elseStmt is not null)
				{
					updatedIf = updatedIf.WithElse(SyntaxFactory.ElseClause(elseStmt));
				}
				else
				{
					updatedIf = updatedIf.WithElse(null);
				}
			}
			else
			{
				updatedIf = updatedIf.WithElse(null);
			}

			return updatedIf;
		}

		return operation.Syntax;
	}

	public override SyntaxNode? VisitReturn(IReturnOperation operation, IDictionary<string, VariableItem> argument)
	{
		if (operation.Syntax is ReturnStatementSyntax returnStatement)
		{
			return returnStatement.WithExpression((ExpressionSyntax?)Visit(operation.ReturnedValue, argument));
		}

		return operation.Syntax;
	}

	public override SyntaxNode? VisitTuple(ITupleOperation operation, IDictionary<string, VariableItem> argument)
	{
		if (operation.Syntax is TupleExpressionSyntax tuple)
		{
			var elements = operation.Elements
				.Select(e => Visit(e, argument))
				.OfType<ExpressionSyntax>();

			return tuple.WithArguments(SyntaxFactory.SeparatedList(elements.Select(SyntaxFactory.Argument)));
		}

		return operation.Syntax;
	}

	public override SyntaxNode? VisitSimpleAssignment(ISimpleAssignmentOperation operation, IDictionary<string, VariableItem> argument)
	{
		if (operation.Syntax is AssignmentExpressionSyntax assignment)
		{
			// Do not visit the left/target to avoid turning assignable expressions into constants.
			var visitedRight = Visit(operation.Value, argument);
			var rightExpr = visitedRight as ExpressionSyntax ?? assignment.Right;
			
			var name = operation.Target switch
			{
				ILocalReferenceOperation localRef => localRef.Local.Name,
				IParameterReferenceOperation paramRef => paramRef.Parameter.Name,
				_ => null
			};

			// If RHS is constant, update the environment for locals/params and replace RHS with a literal.
			if (SyntaxHelpers.TryGetConstantValue(compilation, loader, rightExpr, new VariableItemDictionary(argument), token, out var value))
			{
				switch (operation.Target)
				{
					case ILocalReferenceOperation localRef:
						argument[name].Value = value;
						break;
					case IParameterReferenceOperation paramRef:
						argument[name].Value = value;
						break;
				}

				if (SyntaxHelpers.TryGetLiteral(value, out var literal))
				{
					rightExpr = literal;
				}
			}
			else
			{
				argument[name].HasValue = false;
			}
			

			return assignment.WithRight(rightExpr);
		}

		return operation.Syntax;
	}

	public override SyntaxNode? VisitCompoundAssignment(ICompoundAssignmentOperation operation, IDictionary<string, VariableItem> argument)
	{
		if (operation.Syntax is AssignmentExpressionSyntax assignmentSyntax)
		{
			// Do not visit the left/target to avoid turning assignable expressions into constants.
			var visitedRight = Visit(operation.Value, argument);
			var rightExpr = visitedRight as ExpressionSyntax ?? assignmentSyntax.Right;

			object? leftValue = null;
			var hasLeftValue = false;
			
			// Try to obtain current left value from the environment (locals/params) or as a constant expression
			switch (operation.Target)
			{
				case ILocalReferenceOperation localRef:
					hasLeftValue = argument.TryGetValue(localRef.Local.Name, out var  tempLeftValue) && tempLeftValue.HasValue;
					leftValue = tempLeftValue?.Value;
					break;
				case IParameterReferenceOperation paramRef:
					hasLeftValue = argument.TryGetValue(paramRef.Parameter.Name, out tempLeftValue) && tempLeftValue.HasValue;
					leftValue = tempLeftValue?.Value;
					break;
				default:
					hasLeftValue = SyntaxHelpers.TryGetConstantValue(compilation, loader, assignmentSyntax.Left, new VariableItemDictionary(argument), token, out leftValue);
					break;
			}
	
			// If both sides are constant, compute the result and update environment for locals/params
			if (hasLeftValue && SyntaxHelpers.TryGetConstantValue(compilation, loader, rightExpr, new VariableItemDictionary(argument), token, out var rightValue))
			{
				var result = ObjectExtensions.ExecuteBinaryOperation(operation.OperatorKind, leftValue, rightValue);
	
				switch (operation.Target)
				{
					case ILocalReferenceOperation localRef:
						argument[localRef.Local.Name].Value = result;
						break;
					case IParameterReferenceOperation paramRef:
						argument[paramRef.Parameter.Name].Value = result;
						break;
				}
	
				return SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, assignmentSyntax.Left,SyntaxHelpers.CreateLiteral(result));
			}
	
			// Otherwise, rebuild the assignment with the visited RHS
			return assignmentSyntax.WithRight((ExpressionSyntax)rightExpr);
		}
		
		return operation.Syntax;
	}

	// Shared pattern evaluation for both switch statement and switch expression
	private bool? EvaluatePattern(IOperation pattern, object governingValue, IDictionary<string, VariableItem> argument)
	{
		try
		{
			switch (pattern)
			{
				case IDiscardPatternOperation:
					return true; // matches anything
				case IConstantPatternOperation constPat:
				{
					var patNode = Visit(constPat.Value, argument);
					if (SyntaxHelpers.TryGetConstantValue(compilation, loader, patNode, new VariableItemDictionary(argument), token, out var patValue))
					{
						return Equals(governingValue, patValue);
					}
					return null;
				}
				case IRelationalPatternOperation relPat:
				{
					var rightNode = Visit(relPat.Value, argument);
					if (SyntaxHelpers.TryGetConstantValue(compilation, loader, rightNode, new VariableItemDictionary(argument), token, out var rightValue))
					{
						var result = ObjectExtensions.ExecuteBinaryOperation(relPat.OperatorKind, governingValue, rightValue);
						return result is true;
					}
					return null;
				}
				case IBinaryPatternOperation binPat:
				{
					var left = EvaluatePattern(binPat.LeftPattern, governingValue, argument);
					var right = EvaluatePattern(binPat.RightPattern, governingValue, argument);
					if (left is null || right is null)
					{
						return null;
					}
					return binPat.OperatorKind switch
					{
						BinaryOperatorKind.Or => left.Value || right.Value,
						BinaryOperatorKind.And => left.Value && right.Value,
						_ => (bool?)null
					};
				}
				case INegatedPatternOperation notPat:
				{
					var inner = EvaluatePattern(notPat.Pattern, governingValue, argument);
					return inner is null ? null : !inner.Value;
				}
				default:
					return null; // unsupported pattern kinds -> unknown
			}
		}
		catch
		{
			return null;
		}
	}

	public override SyntaxNode? VisitSwitch(ISwitchOperation operation, IDictionary<string, VariableItem> argument)
	{
		if (operation.Syntax is SwitchStatementSyntax switchStmt)
		{
			var visitedGoverning = Visit(operation.Value, argument);

			// Try constant-folding the switch statement
			if (SyntaxHelpers.TryGetConstantValue(compilation, loader, visitedGoverning, new VariableItemDictionary(argument), token, out var governingValue))
			{
				bool? MatchClause(ICaseClauseOperation clause)
				{
					switch (clause)
					{
						case IDefaultCaseClauseOperation:
							return true;
						case ISingleValueCaseClauseOperation single:
						{
							var node = Visit(single.Value, argument);
							if (SyntaxHelpers.TryGetConstantValue(compilation, loader, node, new VariableItemDictionary(argument), token, out var caseValue))
							{
								return Equals(governingValue, caseValue);
							}
							return null;
						}
						case IRelationalCaseClauseOperation rel:
						{
							var rightNode = Visit(rel.Value, argument);
							if (SyntaxHelpers.TryGetConstantValue(compilation, loader, rightNode, new VariableItemDictionary(argument), token, out var rightValue))
							{
								var result = ObjectExtensions.ExecuteBinaryOperation(rel.Relation, governingValue, rightValue);
								return result is true;
							}
							return null;
						}
						case IPatternCaseClauseOperation patClause:
						{
							var patMatch = EvaluatePattern(patClause.Pattern, governingValue, argument);
							if (patMatch is not true)
							{
								return patMatch; // false or null
							}
							if (patClause.Guard is not null)
							{
								var guardVisited = Visit(patClause.Guard, argument);
								if (!SyntaxHelpers.TryGetConstantValue(compilation, loader, guardVisited, new VariableItemDictionary(argument), token, out var guardVal))
								{
									return null;
								}
								return guardVal is true;
							}
							return true;
						}
						default:
							return null;
					}
				}

				for (var i = 0; i < operation.Cases.Length; i++)
				{
					var @case = operation.Cases[i];
					var hasUnknown = false;
					var matched = false;

					foreach (var clause in @case.Clauses)
					{
						var res = MatchClause(clause);
						if (res is true)
						{
							matched = true;
							break;
						}
						if (res is null)
						{
							hasUnknown = true;
						}
					}

					if (matched)
					{
						var statements = new List<StatementSyntax>();
						foreach (var bodyOp in @case.Body)
						{
							var visited = Visit(bodyOp, argument);
							if (visited is null)
							{
								continue;
							}

							switch (visited)
							{
								case BlockSyntax block:
									foreach (var st in block.Statements)
									{
										if (st is BreakStatementSyntax)
										{
											continue;
										}
										statements.Add(st);
									}
									break;
								case StatementSyntax stmt:
									if (stmt is BreakStatementSyntax)
									{
										break;
									}
									statements.Add(stmt);
									break;
								case ExpressionSyntax expr:
									statements.Add(SyntaxFactory.ExpressionStatement(expr));
									break;
							}
						}

						if (statements.Count == 0)
						{
							return null; // nothing to execute
						}

						return SyntaxFactory.Block(statements);
					}

					if (hasUnknown)
					{
						goto Rebuild;
					}
				}

				// No matching clause deterministically
				return null;
			}

			Rebuild:
			{
				var exprSyntax = visitedGoverning as ExpressionSyntax ?? switchStmt.Expression;
				var newSections = new List<SwitchSectionSyntax>();

				var count = Math.Min(switchStmt.Sections.Count, operation.Cases.Length);
				for (var i = 0; i < count; i++)
				{
					var sectionSyntax = switchStmt.Sections[i];
					var caseOp = operation.Cases[i];

					var newStatements = new List<StatementSyntax>();
					foreach (var bodyOp in caseOp.Body)
					{
						var visited = Visit(bodyOp, argument);
						if (visited is null)
						{
							continue;
						}
						switch (visited)
						{
							case BlockSyntax block:
								newStatements.AddRange(block.Statements);
								break;
							case StatementSyntax stmt:
								newStatements.Add(stmt);
								break;
							case ExpressionSyntax expr:
								newStatements.Add(SyntaxFactory.ExpressionStatement(expr));
								break;
						}
					}

					newSections.Add(sectionSyntax.WithStatements(SyntaxFactory.List(newStatements)));
				}

				return switchStmt
					.WithExpression(exprSyntax)
					.WithSections(SyntaxFactory.List(newSections));
			}
		}

		return operation.Syntax;
	}

	public override SyntaxNode? VisitSwitchExpression(ISwitchExpressionOperation operation, IDictionary<string, VariableItem> argument)
	{
		if (operation.Syntax is SwitchExpressionSyntax switchExpr)
		{
			var visitedGoverning = Visit(operation.Value, argument);

			// Try constant-folding the switch expression
			if (SyntaxHelpers.TryGetConstantValue(compilation, loader, visitedGoverning, new VariableItemDictionary(argument), token, out var governingValue))
			{
				// Evaluate arms in order
				for (var i = 0; i < operation.Arms.Length; i++)
				{
					var arm = operation.Arms[i];
					var patternMatches = EvaluatePattern(arm.Pattern, governingValue, argument);
					if (patternMatches is false)
					{
						continue;
					}
					if (patternMatches is null)
					{
						// cannot determine; bail out and rebuild
						goto ReBuild;
					}

					// Pattern matches, check guard if any
					if (arm.Guard is not null)
					{
						var visitedGuard = Visit(arm.Guard, argument);
						if (!SyntaxHelpers.TryGetConstantValue(compilation, loader, visitedGuard, new VariableItemDictionary(argument), token, out var guardVal))
						{
							goto ReBuild;
						}
						if (guardVal is not true)
						{
							continue; // guard failed
						}
					}

					// Evaluate arm value
					var visitedValue = Visit(arm.Value, argument);
					if (SyntaxHelpers.TryGetConstantValue(compilation, loader, visitedValue, new VariableItemDictionary(argument), token, out var armValue))
					{
						return SyntaxHelpers.CreateLiteral(armValue);
					}
					goto ReBuild;
				}
			}

			ReBuild:
			{
				// Rebuild switch expression with visited governing expression and visited guards/values
				var newArms = new List<SwitchExpressionArmSyntax>();
				for (var i = 0; i < switchExpr.Arms.Count; i++)
				{
					var armSyntax = switchExpr.Arms[i];
					var armOp = operation.Arms[i];
					var visitedArmValue = Visit(armOp.Value, argument) as ExpressionSyntax ?? armSyntax.Expression;

					WhenClauseSyntax? newWhen = null;
					if (armOp.Guard is not null)
					{
						var visitedGuard = Visit(armOp.Guard, argument) as ExpressionSyntax;
						if (visitedGuard is not null)
						{
							newWhen = armSyntax.WhenClause is null
								? SyntaxFactory.WhenClause(visitedGuard)
								: armSyntax.WhenClause.WithCondition(visitedGuard);
						}
						else
						{
							newWhen = armSyntax.WhenClause;
						}
					}
					else
					{
						newWhen = null;
					}

					var newArm = armSyntax
						.WithExpression(visitedArmValue)
						.WithWhenClause(newWhen);
					newArms.Add(newArm);
				}

				return switchExpr
					.WithGoverningExpression((ExpressionSyntax)visitedGoverning!)
					.WithArms(SyntaxFactory.SeparatedList(newArms));
			}
		}

		return operation.Syntax;
	}

	// Helpers for algebraic/logical identities
	private static bool IsNumericZero(object? value) => value switch
	{
		byte b => b == 0,
		sbyte sb => sb == 0,
		short s => s == 0,
		ushort us => us == 0,
		int i => i == 0,
		uint ui => ui == 0,
		long l => l == 0,
		ulong ul => ul == 0,
		float f => f == 0f,
		double d => d == 0d,
		decimal m => m == 0m,
		_ => false
	};

	private static bool IsNumericOne(object? value) => value switch
	{
		byte b => b == 1,
		sbyte sb => sb == 1,
		short s => s == 1,
		ushort us => us == 1,
		int i => i == 1,
		uint ui => ui == 1,
		long l => l == 1,
		ulong ul => ul == 1,
		float f => f == 1f,
		double d => d == 1d,
		decimal m => m == 1m,
		_ => false
	};

	private static bool IsNumericTwo(object? value) => value switch
	{
		byte b => b == 2,
		sbyte sb => sb == 2,
		short s => s == 2,
		ushort us => us == 2,
		int i => i == 2,
		uint ui => ui == 2,
		long l => l == 2,
		ulong ul => ul == 2,
		float f => f == 2f,
		double d => d == 2d,
		decimal m => m == 2m,
		_ => false
	};

	public class VariableItemDictionary(IDictionary<string, VariableItem> inner) : IDictionary<string, object?>
	{
		public bool TryGetValue(string key, [UnscopedRef] out object? value)
		{
			if (inner.TryGetValue(key, out var item) && item.HasValue)
			{
				value = item.Value;
				return true;
			}

			value = null;
			return false;
		}

		public object? this[string key]
		{
			get => inner[key].Value;
			set
			{
				if (inner.ContainsKey(key))
				{
					var item = inner[key];
					inner[key] = new VariableItem(item.Type, value is not null, value);
				}
				else
				{
					throw new KeyNotFoundException($"The given key '{key}' was not present in the dictionary.");
				}
			}
		}

		public ICollection<string> Keys => inner.Keys;

		public ICollection<object?> Values => inner.Values
			.Where(w => w.HasValue)
			.Select(v => v.Value)
			.ToList();

		public bool Remove(KeyValuePair<string, object?> item)
		{
			throw new NotSupportedException("Removing keys is not supported.");
		}

		public int Count => inner.Count(c => c.Value.HasValue);

		public bool IsReadOnly => inner.IsReadOnly;

		public void Add(string key, object? value)
		{
			throw new NotSupportedException("Adding new keys is not supported.");
		}

		public void Add(KeyValuePair<string, object?> item)
		{
			throw new NotSupportedException("Adding new keys is not supported.");
		}

		public void Clear()
		{
			throw new NotSupportedException("Clearing the dictionary is not supported.");
		}

		public bool Contains(KeyValuePair<string, object?> item)
		{
			return inner.TryGetValue(item.Key, out var value) && value.HasValue && Equals(value.Value, item.Value);
		}

		public bool ContainsKey(string key)
		{
			return inner.TryGetValue(key, out var item) && item.HasValue;
		}

		public void CopyTo(KeyValuePair<string, object?>[] array, int arrayIndex)
		{
			foreach (var kvp in inner)
			{
				if (kvp.Value.HasValue)
				{
					array[arrayIndex++] = new KeyValuePair<string, object?>(kvp.Key, kvp.Value.Value);
				}
			}
		}

		public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
		{
			foreach (var kvp in inner)
			{
				if (kvp.Value.HasValue)
				{
					yield return new KeyValuePair<string, object?>(kvp.Key, kvp.Value.Value);
				}
			}
		}

		public bool Remove(string key)
		{
			throw new NotSupportedException("Removing keys is not supported.");
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
}

public class VariableItem(ITypeSymbol type, bool hasValue, object? value)
{
	public ITypeSymbol Type { get; } = type;
	
	public object? Value { get; set; } = value;

	public bool HasValue { get; set; } = hasValue;
}