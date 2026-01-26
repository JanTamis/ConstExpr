using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
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
				&& value.HasValue)
		{
			if (SyntaxHelpers.TryGetLiteral(value.Value, out var expression))
			{
				return expression;
			}

			return value.Value as SyntaxNode;
		}

		return operation.Syntax;
	}

	public override SyntaxNode? VisitPropertyReference(IPropertyReferenceOperation operation, IDictionary<string, VariableItem> argument)
	{
		var name = GetVariableName(operation.Instance);


		if (name is not null && argument.TryGetValue(name, out var item) && item.HasValue)
		{
			var instance = item.Value;
			var type = instance?.GetType() ?? loader.GetType(operation.Property.ContainingType);

			// Handle indexer properties (usually named "Item")
			if (operation.Arguments.Length > 0)
			{
				var propertyInfo = type
					.GetProperties()
					.FirstOrDefault(f => f.GetIndexParameters().Length == operation.Arguments.Length);

				if (propertyInfo == null)
				{
					throw new InvalidOperationException("Indexer property info could not be retrieved.");
				}

				var indices = operation.Arguments
					.Select(a => Visit(a.Value, argument))
					.ToArray();

				if (instance is Array array)
				{
					if (indices.All(a => a is int))
					{
						return SyntaxHelpers.CreateLiteral(array.GetValue(indices.Cast<int>().ToArray()));
					}

					if (indices.All(a => a is long))
					{
						return SyntaxHelpers.CreateLiteral(array.GetValue(indices.Cast<long>().ToArray()));
					}
				}

				var indexValues = indices
					.Select(i => SyntaxHelpers.GetConstantValue(compilation, loader, i, new VariableItemDictionary(argument), token))
					.ToArray();

				return SyntaxHelpers.CreateLiteral(propertyInfo.GetValue(instance, indexValues));
			}
			else
			{
				var propertyName = operation.Property.Name;

				var propertyInfo = type
					.GetProperties()
					.FirstOrDefault(f => f.Name == propertyName && f.GetMethod.IsStatic == operation.Property.IsStatic);

				if (propertyInfo == null)
				{
					throw new InvalidOperationException("Property info could not be retrieved.");
				}

				if (operation.Property.IsStatic)
				{
					return SyntaxHelpers.CreateLiteral(propertyInfo.GetValue(null));
				}

				if (instance is IConvertible)
				{
					instance = Convert.ChangeType(instance, propertyInfo.PropertyType);
				}

				return SyntaxHelpers.CreateLiteral(propertyInfo.GetValue(instance));
			}
		}

		if (operation.Syntax is ElementAccessExpressionSyntax elementAccessSyntax)
		{
			var arguments = operation.Arguments
				.Select(a => Visit(a.Value, argument))
				.OfType<ExpressionSyntax>()
				.ToArray();

			return elementAccessSyntax.WithArgumentList(
				SyntaxFactory.BracketedArgumentList(
					SyntaxFactory.SeparatedList(arguments.Select(SyntaxFactory.Argument))
				)
			);
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

			if (left is ExpressionSyntax leftExpr
					&& right is ExpressionSyntax rightExpr)
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
								if (hasRightValue && rightValue.IsNumericZero())
                {
                  return leftExpr;
                }

                if (hasLeftValue && leftValue.IsNumericZero())
                {
                  return rightExpr;
                }

                break;
							case BinaryOperatorKind.Subtract:
								if (hasRightValue && rightValue.IsNumericZero())
                {
                  return leftExpr;
                }

                if (hasLeftValue && leftValue.IsNumericZero())
                {
                  return SyntaxFactory.PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, Parens(rightExpr));
                }

                break;
							case BinaryOperatorKind.Multiply:
								if (hasRightValue && rightValue.IsNumericOne())
                {
                  return leftExpr;
                }

                if (hasLeftValue && leftValue.IsNumericOne())
                {
                  return rightExpr;
                }

                // x * 0 => 0 and 0 * x => 0 (only for non-floating numeric types to avoid NaN/-0.0 semantics)
                var nonFloating = IsNonFloatingNumeric(operation.LeftOperand.Type) && IsNonFloatingNumeric(operation.RightOperand.Type);

								if (nonFloating && hasRightValue && rightValue.IsNumericZero())
								{
									return SyntaxHelpers.CreateLiteral(0.ToSpecialType(operation.Type.SpecialType));
								}

								if (nonFloating && hasLeftValue && leftValue.IsNumericZero())
								{
									return SyntaxHelpers.CreateLiteral(0.ToSpecialType(operation.Type.SpecialType));
								}

								// 2 * x => (x + x), x * 2 => (x + x) when x is safe to duplicate
								if (hasLeftValue && leftValue.IsNumericTwo() && IsSafeToDuplicate(operation.RightOperand))
								{
									var dup = SyntaxFactory.BinaryExpression(SyntaxKind.AddExpression, rightExpr, rightExpr);
									return Parens(dup);
								}

								if (hasRightValue && rightValue.IsNumericTwo() && IsSafeToDuplicate(operation.LeftOperand))
								{
									var dup = SyntaxFactory.BinaryExpression(SyntaxKind.AddExpression, leftExpr, leftExpr);
									return Parens(dup);
								}
								break;
							case BinaryOperatorKind.Divide:
								if (hasRightValue && rightValue.IsNumericOne())
                {
                  return leftExpr;
                }

                break;
							case BinaryOperatorKind.ExclusiveOr:
								// integral XOR 0 => x
								if (hasRightValue && rightValue.IsNumericZero())
                {
                  return leftExpr;
                }

                if (hasLeftValue && leftValue.IsNumericZero())
                {
                  return rightExpr;
                }

                break;
							case BinaryOperatorKind.Or:
								// x | 0 => x
								if (hasRightValue && rightValue.IsNumericZero())
                {
                  return leftExpr;
                }

                if (hasLeftValue && leftValue.IsNumericZero())
                {
                  return rightExpr;
                }

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
								if (hasRightValue && rightValue is true)
                {
                  return leftExpr; // x && true => x
                }

                if (hasLeftValue && leftValue is true)
                {
                  return rightExpr; // true && x => x
                }

                if (hasLeftValue && leftValue is false)
                {
                  return SyntaxHelpers.CreateLiteral(false); // false && x => false
                }

                break;
							case BinaryOperatorKind.ConditionalOr: // ||
								if (hasRightValue && rightValue is false)
                {
                  return leftExpr; // x || false => x
                }

                if (hasLeftValue && leftValue is false)
                {
                  return rightExpr; // false || x => x
                }

                if (hasLeftValue && leftValue is true)
                {
                  return SyntaxHelpers.CreateLiteral(true); // true || x => true
                }

                break;
							case BinaryOperatorKind.And: // & (bool)
								if (hasRightValue && rightValue is true)
                {
                  return leftExpr; // x & true => x
                }

                if (hasLeftValue && leftValue is true)
                {
                  return rightExpr; // true & x => x
                }

                break; // avoid collapsing to false to preserve evaluation of the other side
							case BinaryOperatorKind.Or: // | (bool)
								if (hasRightValue && rightValue is false)
                {
                  return leftExpr; // x | false => x
                }

                if (hasLeftValue && leftValue is false)
                {
                  return rightExpr; // false | x => x
                }

                break; // avoid collapsing to true to preserve evaluation of the other side
							case BinaryOperatorKind.ExclusiveOr: // ^ (bool)
								if (hasRightValue && rightValue is false)
                {
                  return leftExpr; // x ^ false => x
                }

                if (hasLeftValue && leftValue is false)
                {
                  return rightExpr; // false ^ x => x
                }

                if (hasRightValue && rightValue is true)
                {
                  return SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, Parens(leftExpr)); // x ^ true => !x
                }

                if (hasLeftValue && leftValue is true)
                {
                  return SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, Parens(rightExpr)); // true ^ x => !x
                }

                break;
							case BinaryOperatorKind.Equals:
								if (hasRightValue && rightValue is bool rb)
								{
									return rb
										? leftExpr // x == true => x
										: SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, Parens(leftExpr)); // x == false => !x
								}

								if (hasLeftValue && leftValue is bool lb)
								{
									return lb
										? rightExpr // true == x => x
										: SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, Parens(rightExpr)); // false == x => !x
								}
								break;
							case BinaryOperatorKind.NotEquals:
								if (hasRightValue && rightValue is bool rbn)
								{
									return rbn
										? SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, Parens(leftExpr)) // x != true => !x
										: leftExpr; // x != false => x
								}

								if (hasLeftValue && leftValue is bool lbn)
								{
									return lbn
										? SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, Parens(rightExpr)) // true != x => !x
										: rightExpr; // false != x => x
								}
								break;
						}
					}
				}

				ExpressionSyntax result = binary
					.WithLeft(leftExpr)
					.WithRight(rightExpr);

				if (operation.Syntax.Parent is ParenthesizedExpressionSyntax)
				{
					result = SyntaxFactory.ParenthesizedExpression(result);
				}

				return result;
			}
		}

		return operation.Syntax;

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
				: SyntaxFactory.ParenthesizedExpression(e);
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

	public override SyntaxNode? VisitVariableDeclarationGroup(IVariableDeclarationGroupOperation operation, IDictionary<string, VariableItem> argument)
	{
		if (operation.Syntax is LocalDeclarationStatementSyntax or VariableDeclarationSyntax)
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
		var name = operation.Symbol.Name;

		if (operation.Syntax is VariableDeclaratorSyntax variable)
		{
			var result = (EqualsValueClauseSyntax)Visit(operation.Initializer, argument);

			if (!argument.TryGetValue(name, out var item))
			{
				item = new VariableItem(operation.Type ?? operation.Symbol.Type, true, result?.Value);
				argument.Add(name, item);
			}

			if (result?.Value is IdentifierNameSyntax nameSyntax)
			{
				item.Value = nameSyntax;
				item.IsInitialized = true;
			}
			else if (operation.Initializer is null && operation.Symbol is ILocalSymbol local)
			{
				item.Value = local.Type.GetDefaultValue();
				item.IsInitialized = false;
			}
			else if (SyntaxHelpers.TryGetConstantValue(compilation, loader, result?.Value, new VariableItemDictionary(argument), token, out var value))
			{
				item.Value = value;
				item.IsInitialized = true;
			}
			else
			{
				item.HasValue = false;
				item.IsInitialized = true;
			}

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
			if (loader.TryExecuteMethod(operation.OperatorMethod, null, new VariableItemDictionary(argument), [value], out value)
			    && SyntaxHelpers.TryGetLiteral(value, out var literal))
			{
				// If there's a conversion method, use it and produce a literal syntax node
				return literal;
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

		if (operation.Syntax is CastExpressionSyntax castExpressionSyntax)
		{
			return castExpressionSyntax.WithExpression((ExpressionSyntax)operand);
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
					SyntaxHelpers.TryGetConstantValue(compilation, loader, instance, new VariableItemDictionary(argument), token, out var instanceValue);
						
					if (loader.TryExecuteMethod(targetMethod, instanceValue, new VariableItemDictionary(argument), constantArguments, out var value)
					    && SyntaxHelpers.TryGetLiteral(value, out var literal))
					{
						return literal;
					}
				}
				catch (Exception)
				{
					if (SyntaxHelpers.TryGetOperation<IOperation>(compilation, targetMethod, out var methodOperation))
					{
						var parameters = methodOperation.Syntax switch
						{
							LocalFunctionStatementSyntax localFunc => localFunc.ParameterList,
							MethodDeclarationSyntax methodDecl => methodDecl.ParameterList,
						};

						var variables = new Dictionary<string, object?>();

						for (var i = 0; i < parameters.Parameters.Count; i++)
						{
							var parameterName = parameters.Parameters[i].Identifier.Text;

							variables.Add(parameterName, constantArguments[i]);
						}

						var visitor = new ConstExprOperationVisitor(compilation, loader, exceptionHandler, token);

						switch (methodOperation)
						{
							case ILocalFunctionOperation localFunction:
								visitor.VisitBlock(localFunction.Body, variables);
								break;
							case IMethodBodyOperation methodBody:
								visitor.VisitBlock(methodBody.BlockBody, variables);
								break;
						}

						if (SyntaxHelpers.TryGetLiteral(variables[ConstExprOperationVisitor.RETURNVARIABLENAME], out var result))
						{
							return result;
						}
					}
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
		return operation.Syntax switch
		{
			ReturnStatementSyntax returnStatement => returnStatement.WithExpression((ExpressionSyntax?)Visit(operation.ReturnedValue, argument)),
			YieldStatementSyntax yieldStatementSyntax => yieldStatementSyntax.WithExpression((ExpressionSyntax?)Visit(operation.ReturnedValue, argument)),
			_ => operation.Syntax
		};

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
		if (operation is { Target: IPropertyReferenceOperation propertyReference, Syntax: AssignmentExpressionSyntax assignmentExpression })
		{
			var instance = Visit(propertyReference.Instance, argument);

			if (propertyReference.Arguments.Length > 0)
			{
				var indices = propertyReference.Arguments.Select(a => Visit(a.Value, argument)).ToArray();

				return assignmentExpression.WithLeft(SyntaxFactory.ElementAccessExpression(
					(ExpressionSyntax)instance!,
					SyntaxFactory.BracketedArgumentList(SyntaxFactory.SeparatedList(indices.OfType<ExpressionSyntax>().Select(SyntaxFactory.Argument)))
				));
			}
		}

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

			if (name is not null && argument.TryGetValue(name, out var variable))
			{
				if (!variable.IsInitialized)
				{
					if (rightExpr is IdentifierNameSyntax nameSyntax)
					{
						variable.Value = nameSyntax;
						variable.HasValue = true;
					}
					else if (SyntaxHelpers.TryGetConstantValue(compilation, loader, rightExpr, new VariableItemDictionary(argument), token, out var value))
					{
						variable.Value = value;
						variable.HasValue = true;
					}
					else
					{
						variable.HasValue = false;
					}

					variable.IsInitialized = true;

					var result = SyntaxFactory.LocalDeclarationStatement(SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName("var"), SyntaxFactory.SingletonSeparatedList(
						SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier(name))
							.WithInitializer(SyntaxFactory.EqualsValueClause(rightExpr)))
					));

					return result;
				}

				if (SyntaxHelpers.TryGetConstantValue(compilation, loader, rightExpr, new VariableItemDictionary(argument), token, out var tempValue))
				{
					variable.Value = tempValue;

					if (SyntaxHelpers.TryGetLiteral(tempValue, out var literal))
					{
						rightExpr = literal;
					}
				}
				else
				{
					variable.HasValue = false;
				}
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
					hasLeftValue = argument.TryGetValue(localRef.Local.Name, out var tempLeftValue) && tempLeftValue.HasValue;
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

				return SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, assignmentSyntax.Left, SyntaxHelpers.CreateLiteral(result));
			}

			// Otherwise, rebuild the assignment with the visited RHS
			return assignmentSyntax.WithRight(rightExpr);
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

					WhenClauseSyntax? newWhen;

					if (armOp.Guard is not null)
					{
						if (Visit(armOp.Guard, argument) is ExpressionSyntax visitedGuard)
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

	public override SyntaxNode? VisitLocalFunction(ILocalFunctionOperation operation, IDictionary<string, VariableItem> argument)
	{
		return operation.Syntax;
	}

	public override SyntaxNode? VisitObjectCreation(IObjectCreationOperation operation, IDictionary<string, VariableItem> argument)
	{
		if (operation.Syntax is ObjectCreationExpressionSyntax objCreation)
		{
			var arguments = new List<ArgumentSyntax>(operation.Arguments.Length);

			foreach (var arg in operation.Arguments)
			{
				if (arg.Syntax is ArgumentSyntax argumentSyntax)
				{
					var value = Visit(arg.Value, argument);

					arguments.Add(argumentSyntax.WithExpression((ExpressionSyntax)value!));
				}
			}

			return objCreation.WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments)));
		}



		return base.VisitObjectCreation(operation, argument);
	}

	public override SyntaxNode? VisitForLoop(IForLoopOperation operation, IDictionary<string, VariableItem> argument)
	{
		if (operation.Syntax is ForStatementSyntax forStatement)
		{
			var result = new List<SyntaxNode>();
			var count = 0;

			for (VisitList(operation.Before, argument); Visit(operation.Condition, argument) is LiteralExpressionSyntax { Token.Value: true }; VisitList(operation.AtLoopBottom, argument))
			{
				result.Add(Visit(operation.Body, argument));

				// if (++count > 5)
				// {
				// 	return operation.Syntax;
				// }
			}

			return ToStatementSyntax(result);
		}

		return operation.Syntax;
	}

	public override SyntaxNode? VisitIncrementOrDecrement(IIncrementOrDecrementOperation operation, IDictionary<string, VariableItem> argument)
	{
		var name = GetVariableName(operation.Target);

		if (name is not null && argument.TryGetValue(name, out var variable) && variable.HasValue)
		{
			variable.Value = operation.Kind switch
			{
				OperationKind.Increment => variable.Value.Add(1),
				OperationKind.Decrement => variable.Value.Add(-1),
				_ => variable.Value,
			};

			if (SyntaxHelpers.TryGetLiteral(variable.Value, out var literal))
			{
				return literal;
			}
		}

		return operation.Syntax;
	}

	public override SyntaxNode? VisitUnaryOperator(IUnaryOperation operation, IDictionary<string, VariableItem> argument)
	{
		var name = GetVariableName(operation.Operand);

		if (name is not null && argument.TryGetValue(name, out var variable) && variable.HasValue)
		{
			variable.Value = operation.OperatorKind switch
			{
				UnaryOperatorKind.Plus => variable.Value,
				UnaryOperatorKind.Minus => 0.Subtract(variable.Value),
				UnaryOperatorKind.BitwiseNegation => variable.Value.BitwiseNot(),
				UnaryOperatorKind.Not => variable.Value.LogicalNot(),
				_ => variable.Value,
			};

			if (SyntaxHelpers.TryGetLiteral(variable.Value, out var literal))
			{
				return literal;
			}
		}

		if (operation.Syntax is PrefixUnaryExpressionSyntax prefixUnary)
		{
			var operand = Visit(operation.Operand, argument);
			var operandExpr = operand as ExpressionSyntax ?? prefixUnary.Operand;

			return prefixUnary.WithOperand(operandExpr);
		}

		return operation.Syntax;
	}

	public override SyntaxNode? VisitForEachLoop(IForEachLoopOperation operation, IDictionary<string, VariableItem> argument)
	{
		if (operation.Syntax is ForEachStatementSyntax forEachStatement)
		{
			var result = new List<SyntaxNode>();
			var collection = Visit(operation.Collection, argument);

			if (SyntaxHelpers.TryGetConstantValue(compilation, loader, collection, new VariableItemDictionary(argument), token, out var collectionValue) &&
					collectionValue is IEnumerable enumerable)
			{
				if (operation.LoopControlVariable is IVariableDeclaratorOperation loopControlVariable)
				{
					foreach (var item in enumerable)
					{
						var itemName = loopControlVariable.Symbol.Name;

						if (!argument.TryGetValue(itemName, out var itemVar))
						{
							itemVar = new VariableItem(operation.LoopControlVariable.Type, true, SyntaxHelpers.CreateLiteral(item), true);
							argument.Add(itemName, itemVar);
						}
						else
						{
							itemVar.Value = SyntaxHelpers.CreateLiteral(item);
							itemVar.HasValue = true;
							itemVar.IsInitialized = true;
						}

						result.Add(Visit(operation.Body, argument));
					}
				}

				return ToStatementSyntax(result);
			}
			else if (operation.LoopControlVariable is IVariableDeclaratorOperation loopControlVariable)
			{
				// var itemVar = new VariableItem(operation.LoopControlVariable.Type, false, null, false);
				// argument.Add(loopControlVariable.Symbol.Name, itemVar);

				Visit(operation.LoopControlVariable, argument);
				Visit(operation.Body, argument);
			}

			// return forEachStatement.WithExpression((ExpressionSyntax) collection!)
			// 	.with
			// 	.WithStatement((StatementSyntax)Visit(operation.Body, argument));
		}

		return operation.Syntax;
	}

	private void VisitList(ImmutableArray<IOperation> operations, IDictionary<string, VariableItem> argument)
	{
		foreach (var operation in operations)
		{
			Visit(operation, argument);
		}
	}

	private StatementSyntax ToStatementSyntax(IEnumerable<SyntaxNode> nodes)
	{
		var items = nodes
			.SelectMany<SyntaxNode, SyntaxNode>(s => s is BlockSyntax block ? block.Statements : [s])
			.OfType<StatementSyntax>()
			.ToList();

		if (items.Count == 1)
		{
			return items[0];
		}

		return SyntaxFactory.Block(items);
	}

	private string? GetVariableName(IOperation? operation)
	{
		return operation switch
		{
			ILocalReferenceOperation localReferenceOperation => localReferenceOperation.Local.Name,
			IParameterReferenceOperation parameterReferenceOperation => parameterReferenceOperation.Parameter.Name,
			// IPropertyReferenceOperation propertyReferenceOperation => propertyReferenceOperation.Property.Name,
			IArrayElementReferenceOperation arrayElementReferenceOperation => GetVariableName(arrayElementReferenceOperation.ArrayReference),
			IFieldReferenceOperation fieldReferenceOperation => fieldReferenceOperation.Field.Name,
			IVariableDeclaratorOperation variableDeclaratorOperation => variableDeclaratorOperation.Symbol.Name,
			_ => null,
		};
	}
}