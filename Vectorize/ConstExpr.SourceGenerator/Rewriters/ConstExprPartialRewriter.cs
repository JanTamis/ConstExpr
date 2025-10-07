using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;
using ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;
using ConstExpr.SourceGenerator.Visitors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using SourceGen.Utilities.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading;
using static ConstExpr.SourceGenerator.Helpers.SyntaxHelpers;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Rewriters;

public class ConstExprPartialRewriter(SemanticModel semanticModel, MetadataLoader loader, Action<SyntaxNode?, Exception> exceptionHandler, IDictionary<string, VariableItem> variables, IDictionary<SyntaxNode, bool> additionalMethods, ConstExprAttribute attribute, CancellationToken token) : BaseRewriter(semanticModel, loader, variables)
{
	[return: NotNullIfNotNull(nameof(node))]
	public override SyntaxNode? Visit(SyntaxNode? node)
	{
		try
		{
			return base.Visit(node);
		}
		catch (Exception e)
		{
			exceptionHandler(node, e);
			return node;
		}
	}

	public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
	{
		return null;
	}

	public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
	{
		if (variables.TryGetValue(node.Identifier.Text, out var value)
				&& value.HasValue)
		{
			if (TryGetLiteral(value.Value, out var expression))
			{
				return expression;
			}

			return value.Value as SyntaxNode;
		}

		return node;
	}

	public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
	{
		var result = Visit(node.Expression);

		if (result is ExpressionSyntax expression)
		{
			return node.WithExpression(expression);
		}

		return result;
	}

	public override SyntaxNode? VisitLiteralExpression(LiteralExpressionSyntax node)
	{
		if (TryGetLiteral(node.Token.Value, out var expression))
		{
			return expression;
		}

		return node;
	}

	public override SyntaxList<TNode> VisitList<TNode>(SyntaxList<TNode> list)
	{
		var result = new List<TNode>();
		var shouldStop = false;

		foreach (var node in list)
		{
			if (shouldStop) break;

			var visited = Visit(node);

			switch (visited)
			{
				case null:
					continue;
				case BlockSyntax block:
					{
						foreach (var st in block.Statements)
						{
							if (st is TNode t)
							{
								result.Add(t);

								if (st is ReturnStatementSyntax)
								{
									shouldStop = true;
									break;
								}
							}
						}
						break;
					}
				case TNode t:
					{
						result.Add(t);

						if (visited is ReturnStatementSyntax)
						{
							shouldStop = true;
						}

						break;
					}
			}
		}

		return List(result);
	}

	public override SyntaxNode? VisitArgument(ArgumentSyntax node)
	{
		var expression = Visit(node.Expression);

		return node.WithExpression(expression as ExpressionSyntax ?? node.Expression);
	}

	public override SyntaxNode? VisitBinaryExpression(BinaryExpressionSyntax node)
	{
		var left = Visit(node.Left);
		var right = Visit(node.Right);

		var hasLeftValue = TryGetLiteralValue(left, out var leftValue);
		var hasRightValue = TryGetLiteralValue(right, out var rightValue);

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
				if (loader.TryExecuteMethod(operation.OperatorMethod, null, new VariableItemDictionary(variables), [leftValue, rightValue], out var result))
				{
					return CreateLiteral(result);
				}

				return CreateLiteral(ObjectExtensions.ExecuteBinaryOperation(node.Kind(), leftValue, rightValue));
			}

			// Try algebraic/logical simplifications when one side is a constant and operator is built-in.
			// We avoid transforms that would duplicate or skip evaluation of non-constant operands.
			if (left is ExpressionSyntax leftExpr
					&& right is ExpressionSyntax rightExpr)
			{
				var opMethod = operation.OperatorMethod; // null => built-in operator
				var isBuiltIn = opMethod is null;

				if (isBuiltIn && operation.Type is not null)
				{
					if (TryOptimizeNode(operation.OperatorKind, operation.Type, leftExpr, operation.LeftOperand.Type, rightExpr, operation.RightOperand.Type, out var syntaxNode))
						return syntaxNode;

					// Numeric identities
					if (operation is not null
						&& operation.LeftOperand.Type.IsNumericType()
						&& operation.RightOperand.Type.IsNumericType())
					{
						switch (operation.OperatorKind)
						{
							case BinaryOperatorKind.Subtract:
								if (hasRightValue && rightValue.IsNumericZero()) return leftExpr;
								if (hasLeftValue && leftValue.IsNumericZero()) return PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, Parens(rightExpr));
								break;
							case BinaryOperatorKind.Multiply:
								if (hasRightValue && rightValue.IsNumericOne()) return leftExpr;
								if (hasLeftValue && leftValue.IsNumericOne()) return rightExpr;

								// x * 0 => 0 and 0 * x => 0 (only for non-floating numeric types to avoid NaN/-0.0 semantics)
								var nonFloating = attribute.FloatingPointMode == FloatingPointEvaluationMode.FastMath || operation.LeftOperand.Type.IsNonFloatingNumeric() && operation.RightOperand.Type.IsNonFloatingNumeric();

								if (nonFloating && hasRightValue && rightValue.IsNumericZero()
										|| nonFloating && hasLeftValue && leftValue.IsNumericZero())
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
							case BinaryOperatorKind.GreaterThan:
								{
									// x > x => false
									if (TryGetVariableItem<SyntaxNode>(left, out var leftVariable)
											&& TryGetVariableItem<SyntaxNode>(right, out var rightVariable)
											&& leftVariable.IsEquivalentTo(rightVariable))
									{
										return CreateLiteral(false);
									}

									break;
								}
							case BinaryOperatorKind.LessThan:
								{
									// x < x => false
									if (TryGetVariableItem<SyntaxNode>(left, out var leftVariable)
											&& TryGetVariableItem<SyntaxNode>(right, out var rightVariable)
											&& leftVariable.IsEquivalentTo(rightVariable))
									{
										return CreateLiteral(false);
									}

									break;
								}
						}
					}

					// Boolean logical identities
					if (operation is not null
						&& operation.LeftOperand.Type.IsBoolType()
						&& operation.RightOperand.Type.IsBoolType())
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
		}

		return node
			.WithLeft(left as ExpressionSyntax ?? node.Left)
			.WithRight(right as ExpressionSyntax ?? node.Right);

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
	}
	
	public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
	{
		// Handle nameof(...) directly (in addition to TryGetLiteralValue) so the invocation itself is collapsed early.
		if (node is { Expression: IdentifierNameSyntax { Identifier.Text: "nameof" }, ArgumentList.Arguments.Count: 1 })
		{
			var arg = node.ArgumentList.Arguments[0].Expression;
			string? name = null;

			if (semanticModel.TryGetSymbol(arg, out ISymbol? sym))
			{
				name = sym.Name;
			}
			else
			{
				switch (arg)
				{
					case IdentifierNameSyntax id: name = id.Identifier.Text; break;
					case MemberAccessExpressionSyntax { Name: IdentifierNameSyntax last }: name = last.Identifier.Text; break;
					case QualifiedNameSyntax qn: name = qn.Right.Identifier.Text; break;
					case GenericNameSyntax gen: name = gen.Identifier.Text; break;
				}
			}

			if (name is not null)
			{
				return LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(name));
			}

			// Fallback to base if we could not resolve (should be rare)
			return base.VisitInvocationExpression(node);
		}

		if (semanticModel.TryGetSymbol(node, out IMethodSymbol? targetMethod))
		{
			var arguments = node.ArgumentList.Arguments
				.Select(arg => Visit(arg.Expression))
				.ToList();

			var constantArguments = arguments
				.WhereSelect<SyntaxNode?, object?>(TryGetLiteralValue)
				.ToArray();

			if (constantArguments.Length == targetMethod.Parameters.Length)
			{
				if (node.Expression is MemberAccessExpressionSyntax { Expression: var instanceName })
				{
					TryGetLiteralValue(instanceName, out var instance);

					if (loader.TryExecuteMethod(targetMethod, instance, new VariableItemDictionary(variables), constantArguments, out var value)
							&& TryGetLiteral(value, out var literal))
					{
						if (targetMethod.ReturnsVoid)
						{
							return null;
						}

						return literal;
					}
				}
				else if (TryGetOperation<IOperation>(semanticModel, targetMethod, out var methodOperation))
				{
					var parameters = methodOperation.Syntax switch
					{
						LocalFunctionStatementSyntax localFunc => localFunc.ParameterList,
						MethodDeclarationSyntax methodDecl => methodDecl.ParameterList,
						_ => null,
					};

					var variables = new Dictionary<string, object?>();

					for (var i = 0; i < parameters.Parameters.Count; i++)
					{
						var parameterName = parameters.Parameters[i].Identifier.Text;
						variables.Add(parameterName, constantArguments[i]);
					}

					var visitor = new ConstExprOperationVisitor(semanticModel.Compilation, loader, (_, _) => { }, token);

					switch (methodOperation)
					{
						case ILocalFunctionOperation localFunction when localFunction.Body is not null:
							visitor.VisitBlock(localFunction.Body, variables);
							break;
						case IMethodBodyOperation methodBody when methodBody.BlockBody is not null:
							visitor.VisitBlock(methodBody.BlockBody, variables);
							break;
					}

					if (TryGetLiteral(variables[ConstExprOperationVisitor.RETURNVARIABLENAME], out var result))
					{
						return result;
					}
				}
			}
			else
			{
				IEnumerable<BaseFunctionOptimizer> optimizers = arguments.Count switch
				{
					1 => [new AbsBaseFunctionOptimizer(), new SignFunctionOptimizer(), new RoundFunctionOptimizer(), new SqrtFunctionOptimizer(), new CbrtFunctionOptimizer(), new CeilingFunctionOptimizer(), new FloorFunctionOptimizer(), new CosFunctionOptimizer(), new CoshFunctionOptimizer(), new CosPiFunctionOptimizer(), new SinCosFunctionOptimizer(), new SinCosPiFunctionOptimizer(), new AsinFunctionOptimizer(), new AcosFunctionOptimizer()],
					2 => [new MaxFunctionOptimizer(), new MinFunctionOptimizer(), new RoundFunctionOptimizer(), new PowFunctionOptimizer()],
					3 => [new RoundFunctionOptimizer()],
					_ => []
				};

				foreach (var optimizer in optimizers)
				{
					if (optimizer.TryOptimize(targetMethod, attribute.FloatingPointMode, arguments.OfType<ExpressionSyntax>().ToArray(), additionalMethods, out var optimized))
					{
						return optimized;
					}
				}
			}

			if (targetMethod.IsStatic)
			{
				var syntax = targetMethod.DeclaringSyntaxReferences
					.Select(s => s.GetSyntax(token))
					.Select<SyntaxNode, SyntaxNode?>(s =>
					{
						var mods = TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.StaticKeyword));

						switch (s)
						{
							case MethodDeclarationSyntax method:
								{
									var parameters = method.ParameterList.Parameters
										.ToDictionary(d => d.Identifier.Text, d => new VariableItem(semanticModel.GetTypeInfo(d.Type).Type ?? semanticModel.Compilation.ObjectType, false, null));

									var visitor = new ConstExprPartialRewriter(semanticModel, loader, (_, _) => { }, parameters, additionalMethods, attribute, token);
									var body = visitor.Visit(method.Body) as BlockSyntax;

									return method.WithBody(body).WithModifiers(mods);
								}
							case LocalFunctionStatementSyntax localFunc:
								{
									var parameters = localFunc.ParameterList.Parameters
										.ToDictionary(d => d.Identifier.Text, d => new VariableItem(semanticModel.GetTypeInfo(d.Type).Type ?? semanticModel.Compilation.ObjectType, false, null));

									var visitor = new ConstExprPartialRewriter(semanticModel, loader, (_, _) => { }, parameters, additionalMethods, attribute, token);
									var body = visitor.Visit(localFunc.Body) as BlockSyntax;

									return localFunc.WithBody(body).WithModifiers(mods);
								}
							default:
								{
									return null;
								}
						}
					})
					.FirstOrDefault(f => f is not null);

				if (syntax is not null && !additionalMethods.ContainsKey(syntax))
				{
					additionalMethods.Add(syntax, true);
				}

				return node.WithArgumentList(node.ArgumentList.WithArguments(SeparatedList(arguments.OfType<ExpressionSyntax>().Select(Argument))));
			}
		}

		return base.VisitInvocationExpression(node);
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
			else if (TryGetLiteralValue(node.Initializer?.Value, out var result)
							 || TryGetLiteralValue(value, out result))
			{
				item.Value = result;
				item.IsInitialized = true;
			}
			else
			{
				item.HasValue = false;
				item.IsInitialized = true;
			}

			if (node.Initializer is not null)
			{
				return node.WithInitializer(node.Initializer.WithValue(value as ExpressionSyntax ?? node.Initializer.Value));
			}
		}

		return base.VisitVariableDeclarator(node);
	}

	public override SyntaxNode? VisitIfStatement(IfStatementSyntax node)
	{
		var condition = Visit(node.Condition);

		if (TryGetLiteralValue(condition, out var value))
		{
			if (value is true)
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
			.WithCondition(condition as ExpressionSyntax ?? node.Condition)
			.WithStatement(statement as StatementSyntax ?? node.Statement)
			.WithElse(@else as ElseClauseSyntax);
	}

	public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
	{
		var result = new List<SyntaxNode>();
		// var count = 0;

		for (Visit(node.Declaration); TryGetLiteralValue(Visit(node.Condition), out var value) && value is true; VisitList(node.Incrementors))
		{
			result.Add(Visit(node.Statement));

			// if (++count > 5)
			// {
			// 	return operation.Syntax;
			// }
		}

		return ToStatementSyntax(result);
	}

	public override SyntaxNode? VisitAssignmentExpression(AssignmentExpressionSyntax node)
	{
		// Do not visit the left/target to avoid turning assignable expressions into constants.
		var visitedRight = Visit(node.Right);
		var rightExpr = visitedRight as ExpressionSyntax ?? node.Right;
		var kind = node.OperatorToken.Kind();

		if (node.Left is IdentifierNameSyntax { Identifier.Text: var name } && variables.TryGetValue(name, out var variable))
		{
			if (!variable.IsInitialized)
			{
				if (rightExpr is IdentifierNameSyntax nameSyntax)
				{
					variable.Value = nameSyntax;
					variable.HasValue = true;
				}
				else if (TryGetLiteralValue(rightExpr, out var value))
				{
					variable.Value = ObjectExtensions.ExecuteBinaryOperation(kind, variable.Value, value) ?? value;
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

			if (TryGetLiteralValue(rightExpr, out var tempValue) && variable.HasValue)
			{
				variable.Value = ObjectExtensions.ExecuteBinaryOperation(kind, variable.Value, tempValue) ?? tempValue;

				if (TryGetLiteral(tempValue, out var literal))
				{
					return node.WithRight(literal).WithOperatorToken(Token(SyntaxKind.EqualsToken));
				}
			}
			else
			{
				variable.HasValue = false;
			}
		}
		else if (node.Left is ElementAccessExpressionSyntax elementAccess)
		{
			// Handle compound assignments to element/indexer: a[i] op= c  => a[i] = (a[i] op c)
			// Only if we can obtain the current element value and RHS is constant
			if (TryGetLiteralValue(rightExpr, out var rightVal) && TryGetLiteralValue(elementAccess.Expression, out var instanceVal))
			{
				if (TryGetOperation(semanticModel, elementAccess, out IOperation? op))
				{
					// Collect constant indices without visiting the left target
					var indexConsts = elementAccess.ArgumentList.Arguments
						.Select(a => a.Expression)
						.WhereSelect<SyntaxNode, object?>(TryGetLiteralValue)
						.ToArray();

					switch (op)
					{
						case IArrayElementReferenceOperation arrayOp:
							{
								if (instanceVal is Array arr && indexConsts.Length == arrayOp.Indices.Length)
								{
									try
									{
										object? current = null;

										if (indexConsts.Length == 1)
										{
											var arg0 = indexConsts[0];

											// Index (System.Index)
											if (arg0 is not null && (arg0.GetType().FullName == "System.Index" || arg0.GetType().Name == "Index"))
											{
												var getOffset = arg0.GetType().GetMethod("GetOffset", [typeof(int)]);
												var offset = getOffset?.Invoke(arg0, [arr.Length]);

												if (offset is int idx)
												{
													current = arr.GetValue(idx);
												}
											}
											// Range on the left is not assignable in C#; skip
											else if (arg0 is not null && (arg0.GetType().FullName == "System.Range" || arg0.GetType().Name == "Range"))
											{
												// cannot handle slice assignment
												break;
											}
											else if (arg0 is int i0)
											{
												current = arr.GetValue(i0);
											}
											else if (arg0 is long l0)
											{
												current = arr.GetValue(l0);
											}
										}

										if (current is null)
										{
											if (indexConsts.All(a => a is int))
											{
												current = arr.GetValue(indexConsts.OfType<int>().ToArray());
											}
											else if (indexConsts.All(a => a is long))
											{
												current = arr.GetValue(indexConsts.OfType<long>().ToArray());
											}
										}

										if (current is not null)
										{
											var newVal = ObjectExtensions.ExecuteBinaryOperation(kind, current, rightVal) ?? rightVal;

											if (TryGetLiteral(newVal, out var litRhs))
											{
												return AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, elementAccess, litRhs);
											}
										}
									}
									catch { }
								}
								break;
							}
						case IPropertyReferenceOperation propOp:
							{
								if (propOp.Property.IsIndexer && instanceVal is not null && indexConsts.Length == propOp.Arguments.Length
										&& loader.TryExecuteMethod(propOp.Property.SetMethod, instanceVal, new VariableItemDictionary(variables), indexConsts.Append(rightVal), out _))
								{
									return null;
									//var newVal = ObjectExtensions.ExecuteBinaryOperation(kind, cur, rightVal) ?? rightVal;

									//if (TryGetLiteral(newVal, out var litRhs))
									//{
									//	return AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, elementAccess, litRhs);
									//}
								}
								break;
							}
					}
				}
			}
		}

		if (TryGetOperation(semanticModel, node, out ICompoundAssignmentOperation? compOp))
		{
			if (TryOptimizeNode(compOp.OperatorKind, compOp.Type, node.Left, compOp.Target.Type, rightExpr, compOp.Value.Type, out var syntaxNode))
			{
				return AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, node.Left, syntaxNode as ExpressionSyntax);
			}
		}

		return node.WithRight(rightExpr);
	}

	public override SyntaxNode? VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
	{
		var operand = Visit(node.Operand);

		// Support ++i and --i
		if (node.OperatorToken.IsKind(SyntaxKind.PlusPlusToken) || node.OperatorToken.IsKind(SyntaxKind.MinusMinusToken))
		{
			if (node.Operand is IdentifierNameSyntax id && variables.TryGetValue(id.Identifier.Text, out var variable))
			{
				// Only operate when we have a known value and the variable is initialized
				if (variable.IsInitialized && TryGetLiteralValue(id, out var current))
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
						// Built-in behavior: add/subtract 1 and convert to the variable's special type when applicable
						var st = variable.Type.SpecialType;
						var one = 1.ToSpecialType(st) ?? 1; // fall back to int
						var kind = node.OperatorToken.IsKind(SyntaxKind.PlusPlusToken) ? SyntaxKind.AddExpression : SyntaxKind.SubtractExpression;

						if (st == SpecialType.System_Char)
						{
							var i = Convert.ToInt32(current);
							updated = node.OperatorToken.IsKind(SyntaxKind.PlusPlusToken) ? i + 1 : i - 1;
							updated = Convert.ToChar(updated);
						}
						else
						{
							updated = ObjectExtensions.ExecuteBinaryOperation(kind, current, one) ?? current;
						}
					}

					variable.Value = updated;
					variable.HasValue = true;

					// Prefix returns the updated value
					return TryGetLiteral(updated, out var lit) ? lit : (SyntaxNode)node.WithOperand(id);
				}
			}
		}
		else if (node.OperatorToken.IsKind(SyntaxKind.ExclamationToken)
						 && TryGetLiteralValue(operand, out var value)
						 && value is bool b)
		{
			return CreateLiteral(!b);
		}

		return node.WithOperand(operand as ExpressionSyntax ?? node.Operand);
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
					object? updated = null;

					// Attempt overloaded operator method first
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
						var st = variable.Type.SpecialType;
						var one = 1.ToSpecialType(st) ?? 1;
						var kind = node.OperatorToken.IsKind(SyntaxKind.PlusPlusToken) ? SyntaxKind.AddExpression : SyntaxKind.SubtractExpression;

						if (st == SpecialType.System_Char)
						{
							var i = Convert.ToInt32(current);
							updated = node.OperatorToken.IsKind(SyntaxKind.PlusPlusToken) ? i + 1 : i - 1;
							updated = Convert.ToChar(updated);
						}
						else
						{
							updated = ObjectExtensions.ExecuteBinaryOperation(kind, current, one) ?? current;
						}
					}

					// Postfix returns the original value, but updates the variable
					variable.Value = updated;
					variable.HasValue = true;

					return TryGetLiteral(current, out var lit) ? lit : (SyntaxNode)node.WithOperand(id);
				}
			}
		}

		return base.VisitPostfixUnaryExpression(node);
	}

	public override SyntaxNode? VisitParenthesizedExpression(ParenthesizedExpressionSyntax node)
	{
		var expression = Visit(node.Expression);

		if (expression is LiteralExpressionSyntax or IdentifierNameSyntax)
		{
			return expression;
		}

		return node.WithExpression((ExpressionSyntax)expression);
	}

	public override SyntaxNode? VisitCastExpression(CastExpressionSyntax node)
	{
		if (semanticModel.TryGetSymbol(node.Type, out ITypeSymbol? symbol))
		{
			var expression = Visit(node.Expression);

			if (TryGetLiteralValue(expression, out var value))
			{
				// if (loader.TryExecuteMethod(operation.OperatorMethod, null, new VariableItemDictionary(variables), [ value ], out value)
				//     && TryGetLiteral(value, out var literal))
				// {
				// 	// If there's a conversion method, use it and produce a literal syntax node
				// 	return literal;
				// }

				// Convert the runtime value to the requested special type, then create a literal syntax node
				switch (symbol.SpecialType)
				{
					case SpecialType.System_Boolean: return CreateLiteral(Convert.ToBoolean(value));
					case SpecialType.System_Byte: return CreateLiteral(Convert.ToByte(value));
					case SpecialType.System_Char: return CreateLiteral(Convert.ToChar(value));
					case SpecialType.System_DateTime: return CreateLiteral(Convert.ToDateTime(value));
					case SpecialType.System_Decimal: return CreateLiteral(Convert.ToDecimal(value));
					case SpecialType.System_Double: return CreateLiteral(Convert.ToDouble(value));
					case SpecialType.System_Int16: return CreateLiteral(Convert.ToInt16(value));
					case SpecialType.System_Int32: return CreateLiteral(Convert.ToInt32(value));
					case SpecialType.System_Int64: return CreateLiteral(Convert.ToInt64(value));
					case SpecialType.System_SByte: return CreateLiteral(Convert.ToSByte(value));
					case SpecialType.System_Single: return CreateLiteral(Convert.ToSingle(value));
					case SpecialType.System_String: return CreateLiteral(Convert.ToString(value));
					case SpecialType.System_UInt16: return CreateLiteral(Convert.ToUInt16(value));
					case SpecialType.System_UInt32: return CreateLiteral(Convert.ToUInt32(value));
					case SpecialType.System_UInt64: return CreateLiteral(Convert.ToUInt64(value));
					case SpecialType.System_Object: return node.WithExpression(expression as ExpressionSyntax ?? node.Expression);
					default:
						{
							if (TryGetOperation(semanticModel, node, out IConversionOperation? operation))
							{
								if (loader.TryExecuteMethod(operation.OperatorMethod, null, new VariableItemDictionary(variables), [value], out var result)
										&& TryGetLiteral(result, out var literal))
								{
									return literal;
								}

								return node.WithExpression(expression as ExpressionSyntax ?? node.Expression);
							}

							break;
						}
				}
			}

			return node.WithExpression(expression as ExpressionSyntax ?? node.Expression);
		}

		return base.VisitCastExpression(node);
	}

	public override SyntaxNode? VisitSwitchStatement(SwitchStatementSyntax node)
	{
		var visitedGoverning = Visit(node.Expression);

		if (TryGetConstantValue(semanticModel.Compilation, loader, visitedGoverning ?? node.Expression, new VariableItemDictionary(variables), token, out var governingValue))
		{
			bool? EvaluatePattern(PatternSyntax pattern, object? value)
			{
				try
				{
					switch (pattern)
					{
						case DiscardPatternSyntax:
							return true;
						case ConstantPatternSyntax constPat:
							{
								var visited = Visit(constPat.Expression) ?? constPat.Expression;
								return TryGetConstantValue(semanticModel.Compilation, loader, visited, new VariableItemDictionary(variables), token, out var patVal)
									? Equals(value, patVal)
									: null;
							}
						case RelationalPatternSyntax relPat:
							{
								var visited = Visit(relPat.Expression) ?? relPat.Expression;

								if (!TryGetConstantValue(semanticModel.Compilation, loader, visited, new VariableItemDictionary(variables), token, out var rightVal))
								{
									return null;
								}

								var op = relPat.OperatorToken.Kind();

								var result = op switch
								{
									SyntaxKind.LessThanToken => ObjectExtensions.ExecuteBinaryOperation(BinaryOperatorKind.LessThan, value, rightVal),
									SyntaxKind.LessThanEqualsToken => ObjectExtensions.ExecuteBinaryOperation(BinaryOperatorKind.LessThanOrEqual, value, rightVal),
									SyntaxKind.GreaterThanToken => ObjectExtensions.ExecuteBinaryOperation(BinaryOperatorKind.GreaterThan, value, rightVal),
									SyntaxKind.GreaterThanEqualsToken => ObjectExtensions.ExecuteBinaryOperation(BinaryOperatorKind.GreaterThanOrEqual, value, rightVal),
									_ => null,
								};

								return result is true;
							}
						case BinaryPatternSyntax binPat:
							{
								var l = EvaluatePattern(binPat.Left, value);
								var r = EvaluatePattern(binPat.Right, value);

								if (l is null || r is null)
								{
									return null;
								}

								return binPat.OperatorToken.Kind() switch
								{
									SyntaxKind.OrKeyword => l.Value || r.Value,
									SyntaxKind.AndKeyword => l.Value && r.Value,
									_ => null,
								};
							}
						case UnaryPatternSyntax unary when unary.OperatorToken.IsKind(SyntaxKind.NotKeyword):
							{
								var inner = EvaluatePattern(unary.Pattern, value);
								return inner is null ? null : !inner.Value;
							}
						case ParenthesizedPatternSyntax parPat:
							return EvaluatePattern(parPat.Pattern, value);
						case VarPatternSyntax:
							return true;
						case DeclarationPatternSyntax declPat:
							{
								if (semanticModel.Compilation.TryGetSemanticModel(declPat.Type, out var model))
								{
									var typeInfo = model.GetTypeInfo(declPat.Type, token).Type;

									if (typeInfo is not null && value is not null)
									{
										return string.Equals(typeInfo.ToDisplayString(), value.GetType().FullName, StringComparison.Ordinal)
													 || string.Equals(typeInfo.Name, value.GetType().Name, StringComparison.Ordinal);
									}
									return false;
								}
								return null;
							}
						default:
							return null;
					}
				}
				catch
				{
					return null;
				}
			}

			bool? EvaluateWhen(WhenClauseSyntax when)
			{
				var visited = Visit(when.Condition) ?? when.Condition;
				return TryGetConstantValue(semanticModel.Compilation, loader, visited, new VariableItemDictionary(variables), token, out var val)
					? val is true
					: null;
			}

			bool? LabelMatches(SwitchLabelSyntax label)
			{
				return label switch
				{
					DefaultSwitchLabelSyntax => true,
					CaseSwitchLabelSyntax constCase =>
						TryGetConstantValue(semanticModel.Compilation, loader, Visit(constCase.Value) ?? constCase.Value, new VariableItemDictionary(variables), token, out var caseValue)
							? Equals(governingValue, caseValue)
							: null,
					CasePatternSwitchLabelSyntax patCase =>
						EvaluatePattern(patCase.Pattern, governingValue) is not bool patMatch
							? null
							: patCase.WhenClause is null
								? patMatch
								: EvaluateWhen(patCase.WhenClause) switch
								{
									true => patMatch,
									false => false,
									null => null,
								},
					_ => null
				};
			}

			for (var i = 0; i < node.Sections.Count; i++)
			{
				var section = node.Sections[i];
				var matched = false;

				foreach (var label in section.Labels)
				{
					var res = LabelMatches(label);

					if (res is true)
					{
						matched = true;
						break;
					}
				}

				if (matched)
				{
					var statements = new List<StatementSyntax>();

					foreach (var st in section.Statements)
					{
						var visited = Visit(st);

						if (visited is null)
						{
							continue;
						}

						switch (visited)
						{
							case BlockSyntax block:
								foreach (var inner in block.Statements)
								{
									if (inner is BreakStatementSyntax) { continue; }
									statements.Add(inner);
								}
								break;
							case StatementSyntax stmt:
								if (stmt is BreakStatementSyntax) { break; }
								statements.Add(stmt);
								break;
							case ExpressionSyntax expr:
								statements.Add(ExpressionStatement(expr));
								break;
						}
					}

					return statements.Count == 0 ? null : Block(statements);
				}
			}

			return null;
		}

		var exprSyntax = visitedGoverning as ExpressionSyntax ?? node.Expression;
		var newSections = new List<SwitchSectionSyntax>(node.Sections.Count);

		foreach (var section in node.Sections)
		{
			var newStatements = new List<StatementSyntax>(section.Statements.Count);

			foreach (var st in section.Statements)
			{
				var visited = Visit(st);

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
						newStatements.Add(ExpressionStatement(expr));
						break;
				}
			}

			newSections.Add(section.WithStatements(List(newStatements)));
		}

		return node
			.WithExpression(exprSyntax)
			.WithSections(List(newSections));
	}

	public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
	{
		return node;
		// var body = Visit(node.Body);
		//
		// return body switch
		// {
		// 	ExpressionSyntax expr => node.WithBody(expr),
		// 	BlockSyntax block => node.WithBody(block),
		// 	_ => base.VisitSimpleLambdaExpression(node)
		// };
	}

	public override SyntaxNode? VisitElementAccessExpression(ElementAccessExpressionSyntax node)
	{
		var instance = Visit(node.Expression);

		var arguments = node.ArgumentList.Arguments
			.Select(arg => Visit(arg.Expression));

		var constantArguments = arguments
			.WhereSelect<SyntaxNode, object?>(TryGetLiteralValue)
			.ToArray();

		if (TryGetLiteralValue(node.Expression, out var instanceValue))
		{
			if (TryGetOperation(semanticModel, node, out IOperation? operation))
			{
				var type = instanceValue?.GetType();

				switch (operation)
				{
					case IArrayElementReferenceOperation arrayOp:
						if (instanceValue is Array arr
								&& constantArguments.Length == arrayOp.Indices.Length)
						{
							try
							{
								if (constantArguments.Length == 1)
								{
									var arg = constantArguments[0];

									if (arg is not null && (arg.GetType().FullName == "System.Range" || arg.GetType().Name == "Range"))
									{
										var getOffsetAndLength = arg.GetType().GetMethod("GetOffsetAndLength", [typeof(int)]);

										if (getOffsetAndLength is not null)
										{
											var tuple = getOffsetAndLength.Invoke(arg, [arr.Length]);

											if (tuple is not null)
											{
												var tType = tuple.GetType();
												var item1 = tType.GetField("Item1")?.GetValue(tuple);
												var item2 = tType.GetField("Item2")?.GetValue(tuple);

												if (item1 is int offset && item2 is int length)
												{
													var slice = Array.CreateInstance(type?.GetElementType() ?? typeof(object), length);
													Array.Copy(arr, offset, slice, 0, length);

													if (TryGetLiteral(slice, out var result))
													{
														return result;
													}
												}
											}
										}
									}
									else if (arg is not null && (arg.GetType().FullName == "System.Index" || arg.GetType().Name == "Index"))
									{
										var getOffset = arg.GetType().GetMethod("GetOffset", [typeof(int)]);

										var offset = getOffset?.Invoke(arg, [arr.Length]);

										if (offset is int idx)
										{
											var value = arr.GetValue(idx);

											if (TryGetLiteral(value, out var literal))
											{
												return literal;
											}
										}
									}
								}

								if (constantArguments.All(a => a is int))
								{
									var value = arr.GetValue(constantArguments.OfType<int>().ToArray());

									if (TryGetLiteral(value, out var literal))
									{
										return literal;
									}
								}
								else if (constantArguments.All(a => a is long))
								{
									var value = arr.GetValue(constantArguments.OfType<long>().ToArray());

									if (TryGetLiteral(value, out var literal))
									{
										return literal;
									}
								}
							}
							catch (Exception)
							{
							}
						}
						break;
					case IPropertyReferenceOperation propOp:
						if (propOp.Property.IsIndexer
								&& instanceValue is not null
								&& constantArguments.Length == propOp.Arguments.Length)
						{
							try
							{
								if (loader.TryExecuteMethod(propOp.Property.GetMethod, instanceValue, new VariableItemDictionary(variables), constantArguments, out var value)
										&& TryGetLiteral(value, out var literal))
								{
									return literal;
								}
							}
							catch (Exception)
							{
							}
						}
						break;
				}
			}

			if (semanticModel.TryGetSymbol(node, out IPropertySymbol? propertySymbol)
					&& constantArguments.Length == propertySymbol.Parameters.Length)
			{
				try
				{
					if (loader.TryExecuteMethod(propertySymbol.GetMethod, instanceValue, new VariableItemDictionary(variables), constantArguments, out var value)
							&& TryGetLiteral(value, out var literal))
					{
						return literal;
					}
				}
				catch (Exception)
				{
				}

				return node
					.WithExpression(instance as ExpressionSyntax ?? node.Expression)
					.WithArgumentList(node.ArgumentList
						.WithArguments(SeparatedList(arguments.Select(s => Argument((ExpressionSyntax)s)))));
			}
		}

		return base.VisitElementAccessExpression(node);
	}

	public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
	{
		TryGetLiteralValue(Visit(node.Expression), out var instanceValue);

		if (semanticModel.TryGetSymbol(node, out ISymbol? symbol))
		{
			switch (symbol)
			{
				case IFieldSymbol fieldSymbol:
					if (loader.TryGetFieldValue(fieldSymbol, instanceValue, out var value)
							&& TryGetLiteral(value, out var literal))
					{
						return literal;
					}
					break;
				case IPropertySymbol propertySymbol:
					if (propertySymbol.Parameters.Length == 0)
					{
						if (loader.TryExecuteMethod(propertySymbol.GetMethod, instanceValue, new VariableItemDictionary(variables), [], out value)
								&& TryGetLiteral(value, out literal))
						{
							return literal;
						}
					}
					break;
			}
		}

		return node;
	}

	public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
	{
		var visitedExpr = Visit(node.Expression);

		if (TryGetLiteralValue(visitedExpr, out var collection))
		{
			IEnumerable<object?> Enumerate(object? obj)
			{
				switch (obj)
				{
					case null:
						yield break;
					case Array arr:
						foreach (var it in arr) yield return it;
						yield break;
					case string s:
						foreach (var ch in s) yield return ch;
						yield break;
					case System.Collections.IEnumerable en:
						foreach (var it in en) yield return it;
						yield break;
				}
			}

			if (TryGetOperation(semanticModel, node, out IForEachLoopOperation? operation))
			{
				var name = node.Identifier.Text;
				var hadPrev = variables.TryGetValue(name, out var prevItem);
				var statements = new List<SyntaxNode>();
				var elementType =
					operation.Type
					?? semanticModel.GetTypeInfo(node.Type, token).Type
					?? operation.LoopControlVariable?.Type
					?? semanticModel.Compilation.GetSpecialType(SpecialType.System_Object);

				foreach (var item in Enumerate(collection))
				{
					var loopVar = new VariableItem(elementType, true, item, true);

					variables[name] = loopVar;

					var visitedBody = Visit(node.Statement);

					if (visitedBody is not null)
					{
						statements.Add(visitedBody);
					}
				}

				if (hadPrev)
				{
					variables[name] = prevItem;
				}
				else
				{
					variables.Remove(name);
				}

				return ToStatementSyntax(statements);
			}
		}

		return node
			.WithExpression(visitedExpr as ExpressionSyntax ?? node.Expression)
			.WithStatement(Visit(node.Statement) as StatementSyntax ?? node.Statement);
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

							result.Add(InterpolatedStringText(Token(interp.GetLeadingTrivia(), SyntaxKind.InterpolatedStringTextToken, str, str, interp.GetTrailingTrivia())));
						}
						else
						{
							result.Add(interp.WithExpression(visited as ExpressionSyntax ?? interp.Expression));
						}

						break;
					}
			}
		}

		if (result.All(a => a is InterpolatedStringTextSyntax))
		{
			return LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(String.Concat(result.OfType<InterpolatedStringTextSyntax>().Select(s => s.TextToken.ValueText))));
		}

		return node.WithContents(List(result));
	}

	public override SyntaxNode VisitBlock(BlockSyntax node)
	{
		return node.WithStatements(VisitList(node.Statements));
	}

	public override SyntaxNode? VisitReturnStatement(ReturnStatementSyntax node)
	{
		return node.WithExpression(Visit(node.Expression) as ExpressionSyntax);
	}

	public override SyntaxNode? VisitArgumentList(ArgumentListSyntax node)
	{
		return node.WithArguments(VisitList(node.Arguments));
	}

	private object? ExecuteConversion(IConversionOperation conversion, object? value)
	{
		// If there's a conversion method, use it and produce a literal syntax node
		if (loader.TryExecuteMethod(conversion.OperatorMethod, null, new VariableItemDictionary(variables), [value], out var result))
		{
			return result;
		}

		// Convert the runtime value to the requested special type, then create a literal syntax node
		return conversion.Type?.SpecialType switch
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
			_ => value,
		};
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

		return Block(items);
	}

	private bool TryGetVariableItem<TValue>(SyntaxNode? node, [NotNullWhen(true)] out TValue? item)
	{
		if (node is IdentifierNameSyntax { Identifier.Text: var name }
				&& variables.TryGetValue(name, out var variable)
				&& variable.Value is TValue value)
		{
			item = value;
			return true;
		}

		item = default;
		return false;
	}

	private bool TryOptimizeNode(BinaryOperatorKind kind, ITypeSymbol type, ExpressionSyntax leftExpr, ITypeSymbol leftType, ExpressionSyntax rightExpr, ITypeSymbol rightType, out SyntaxNode? syntaxNode)
	{
		// Select optimizer based on operator kind
		var optimizer = BaseBinaryOptimizer.Create(kind, type, leftExpr, leftType, rightExpr, rightType, attribute.FloatingPointMode);

		if (optimizer is not null)
		{
			if (optimizer.Kind == kind
			    && optimizer.TryOptimize(loader, variables, out var optimized))
			{
				syntaxNode = optimized;
				return true;
			}
		}

		syntaxNode = null;
		return false;
	}
}
