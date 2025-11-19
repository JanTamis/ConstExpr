using ConstExpr.Core.Attributes;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers;
using ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;
using ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;
using ConstExpr.SourceGenerator.Visitors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using SourceGen.Utilities.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading;
using static ConstExpr.SourceGenerator.Helpers.SyntaxHelpers;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
/// Rewriter that performs constant folding and safe partial evaluation over C# syntax trees.
/// This class is intentionally split across multiple partial files to keep concerns focused
/// and the code easier to navigate and extend.
/// </summary>
public partial class ConstExprPartialRewriter(
	SemanticModel semanticModel,
	MetadataLoader loader,
	Action<SyntaxNode?, Exception> exceptionHandler,
	IDictionary<string, VariableItem> variables,
	IDictionary<SyntaxNode, bool> additionalMethods,
	ISet<string> usings,
	ConstExprAttribute attribute,
	CancellationToken token,
	HashSet<IMethodSymbol>? visitingMethods = null)
	: BaseRewriter(semanticModel, loader, variables)
{
	private readonly Lazy<Type[]> _stringOptimizers = new(() =>
	{
		return typeof(BaseStringFunctionOptimizer).Assembly
			.GetTypes()
			.Where(t => !t.IsAbstract && typeof(BaseStringFunctionOptimizer).IsAssignableFrom(t))
			.ToArray();
	}, isThreadSafe: true);

	private readonly Lazy<BaseMathFunctionOptimizer[]> _mathOptimizers = new(() =>
	{
		return typeof(BaseMathFunctionOptimizer).Assembly
			.GetTypes()
			.Where(t => !t.IsAbstract && typeof(BaseMathFunctionOptimizer).IsAssignableFrom(t))
			.Select(t => Activator.CreateInstance(t) as BaseMathFunctionOptimizer)
			.OfType<BaseMathFunctionOptimizer>()
			.ToArray();
	}, isThreadSafe: true);

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
		    && value.HasValue
		    && !value.IsAltered)
		{
			if (!value.HasValue)
			{
				value.IsAccessed = true;
			}

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
		// Special handling for increment/decrement expressions used as statements
		// When used as a statement, we need to preserve the side-effect, not the return value
		if (node.Expression is PostfixUnaryExpressionSyntax or PrefixUnaryExpressionSyntax)
		{
			var originalExpression = node.Expression;
			var result = Visit(originalExpression);

			// If the result is a literal (the return value), we need to preserve the original
			// increment/decrement statement because it's being used for side-effects only
			if (result is LiteralExpressionSyntax)
			{
				// Keep the original increment/decrement statement
				return node;
			}

			if (result is ExpressionSyntax expression)
			{
				return node.WithExpression(expression);
			}

			return result;
		}

		var visitedResult = Visit(node.Expression);

		// If the result is not an expression (e.g., it got removed or transformed),
		// check if we should preserve the original statement
		if (visitedResult is not ExpressionSyntax syntax)
		{
			// If Visit returned null or a non-expression, preserve the original statement
			// to maintain side-effects (e.g., method calls like StringBuilder.Append)
			if (visitedResult is null)
			{
				return node;
			}

			return visitedResult;
		}

		return node.WithExpression(syntax);
	}

	/// <summary>
	/// Visits an expression that may be an increment/decrement used for side-effects.
	/// Preserves the original expression if it evaluates to a literal (indicating it was simplified).
	/// </summary>
	private ExpressionSyntax VisitIncrementExpression(ExpressionSyntax expression)
	{
		if (expression is PostfixUnaryExpressionSyntax or PrefixUnaryExpressionSyntax)
		{
			var result = Visit(expression);

			// If the result is a literal, keep the original increment/decrement for side-effects
			if (result is LiteralExpressionSyntax)
			{
				return expression;
			}

			if (result is ExpressionSyntax expr)
			{
				return expr;
			}
		}

		var visited = Visit(expression);
		return visited as ExpressionSyntax ?? expression;
	}

	public override SyntaxNode? VisitLiteralExpression(LiteralExpressionSyntax node)
	{
		if (TryGetLiteral(node.Token.Value, out var expression))
		{
			if (semanticModel.GetOperation(node) is IOperation operation)
			{
				
			}
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
			if (shouldStop) 
				break;

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

	public override SeparatedSyntaxList<TNode> VisitList<TNode>(SeparatedSyntaxList<TNode> list)
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

		return SeparatedList(result);
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
				    && loader.TryExecuteMethod(operation.OperatorMethod, null, new VariableItemDictionary(variables), [ leftValue, rightValue ], out var result))
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

				if (isBuiltIn
				    && operation.Type is not null
				    && attribute.FloatingPointMode == FloatingPointEvaluationMode.FastMath
				    && TryOptimizeNode(operation.OperatorKind, operation.Type, leftExpr, operation.LeftOperand.Type, rightExpr, operation.RightOperand.Type, out var syntaxNode))
				{
					return syntaxNode;
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

			var hasCharOverload = attribute.FloatingPointMode == FloatingPointEvaluationMode.FastMath
			                      && TryGetCharOverload(targetMethod, arguments, out _);

			var constantArguments = new List<object>(arguments.Count);

			for (var i = 0; i < arguments.Count; i++)
			{
				if (TryGetLiteralValue(arguments[i], out var value) || TryGetLiteralValue(node.ArgumentList.Arguments[i], out value))
				{
					constantArguments.Add(value);
				}
			}

			if (constantArguments.Count == targetMethod.Parameters.Length)
			{
				if (node.Expression is MemberAccessExpressionSyntax { Expression: var instanceName }
				    && !targetMethod.ContainingType.EqualsType(semanticModel.Compilation.GetTypeByMetadataName("System.Random")))
				{
					// instanceName = Visit(instanceName) as ExpressionSyntax ?? instanceName;

					var hasLiteral = TryGetLiteralValue(instanceName, out var instance) || TryGetLiteralValue(Visit(instanceName), out instance);

					if (hasLiteral)
					{
						try
						{
							instance = Convert.ChangeType(instance, loader.GetType(targetMethod.ContainingType));
						}
						catch (InvalidCastException)
						{

						}
					}

					if ((targetMethod.IsStatic || (hasLiteral && (instanceName is not IdentifierNameSyntax identifier || CanBePruned(identifier.Identifier.Text))))
					    && loader.TryExecuteMethod(targetMethod, instance, new VariableItemDictionary(variables), constantArguments, out var value)
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

					for (var i = 0; i < (parameters?.Parameters.Count ?? 0); i++)
					{
						var parameterName = parameters!.Parameters[i].Identifier.Text;
						variables.Add(parameterName, constantArguments[i]);
					}

					var visitor = new ConstExprOperationVisitor(semanticModel.Compilation, loader, (_, _) => { }, token);

					switch (methodOperation)
					{
						case ILocalFunctionOperation { Body: not null } localFunction:
							visitor.VisitBlock(localFunction.Body, variables);
							break;
						case IMethodBodyOperation { BlockBody: not null } methodBody:
							visitor.VisitBlock(methodBody.BlockBody, variables);
							break;
					}

					if (TryGetLiteral(variables[ConstExprOperationVisitor.RETURNVARIABLENAME], out var result))
					{
						return result;
					}
				}
			}
			else if (targetMethod.ContainingType.SpecialType == SpecialType.System_String
			         && node.Expression is MemberAccessExpressionSyntax memberAccess)
			{
				var instance = Visit(memberAccess.Expression);

				var optimizers = _stringOptimizers.Value
					.Select(s => Activator.CreateInstance(s, instance) as BaseStringFunctionOptimizer)
					.Where(o => String.Equals(o.Name, targetMethod.Name, StringComparison.Ordinal));

				foreach (var stringOptimizer in optimizers)
				{
					if (stringOptimizer.TryOptimize(targetMethod, node, arguments.OfType<ExpressionSyntax>().ToArray(), additionalMethods, out var optimized))
					{
						return optimized;
					}
				}

				if (targetMethod.IsStatic)
				{
					return node.WithExpression(memberAccess.WithExpression(ParseTypeName(targetMethod.ContainingType.Name)));
				}
			}
			else if (attribute.FloatingPointMode == FloatingPointEvaluationMode.FastMath)
			{
				var mathOptimizers = _mathOptimizers.Value
					.Where(o => String.Equals(o.Name, targetMethod.Name, StringComparison.Ordinal)
					            && o.ParameterCounts.Contains(targetMethod.Parameters.Length));

				foreach (var mathOptimizer in mathOptimizers)
				{
					if (mathOptimizer.TryOptimize(targetMethod, node, arguments.OfType<ExpressionSyntax>().ToArray(), additionalMethods, out var optimized))
					{
						return optimized;
					}
				}
			}

			if (hasCharOverload)
			{
				arguments = arguments.Select(s =>
					{
						if (TryGetLiteralValue(s, out var value) && value is string { Length: 1 } charValue)
						{
							return LiteralExpression(
								SyntaxKind.CharacterLiteralExpression,
								Literal(charValue[0]));
						}

						return s;
					})
					.ToList();
			}

			if (targetMethod.IsStatic)
			{
				// Check if we're already visiting this method to prevent infinite recursion
				if (visitingMethods?.Contains(targetMethod) is true)
				{
					// Don't inline this method - just keep the invocation
					usings.Add(targetMethod.ContainingType.ContainingNamespace.ToString());
					return node.WithArgumentList(node.ArgumentList.WithArguments(SeparatedList(arguments.OfType<ExpressionSyntax>().Select(Argument))));
				}

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

								// Add this method to the visiting set before recursing
								visitingMethods?.Add(targetMethod);

								var visitor = new ConstExprPartialRewriter(semanticModel, loader, (_, _) => { }, parameters, additionalMethods, usings, attribute, token, visitingMethods);
								var body = visitor.Visit(method.Body) as BlockSyntax;

								visitingMethods?.Remove(targetMethod);

								return method.WithBody(body).WithModifiers(mods);
							}
							case LocalFunctionStatementSyntax localFunc:
							{
								var parameters = localFunc.ParameterList.Parameters
									.ToDictionary(d => d.Identifier.Text, d => new VariableItem(semanticModel.GetTypeInfo(d.Type).Type ?? semanticModel.Compilation.ObjectType, false, null));

								// Add this method to the visiting set before recursing
								visitingMethods?.Add(targetMethod);
								var visitor = new ConstExprPartialRewriter(semanticModel, loader, (_, _) => { }, parameters, additionalMethods, usings, attribute, token, visitingMethods);
								var body = visitor.Visit(localFunc.Body) as BlockSyntax;
								visitingMethods?.Remove(targetMethod);

								return localFunc.WithBody(body).WithModifiers(mods);
							}
							default:
							{
								return null;
							}
						}
					})
					.FirstOrDefault(f => f is not null);

				if (syntax is not null)
				{
					if (!additionalMethods.ContainsKey(syntax))
					{
						additionalMethods.Add(syntax, true);
					}
				}
				else
				{
					usings.Add(targetMethod.ContainingType.ContainingNamespace.ToString());
				}

				return node.WithArgumentList(node.ArgumentList.WithArguments(SeparatedList(arguments.OfType<ExpressionSyntax>().Select(Argument))));
			}

			usings.Add(targetMethod.ContainingType.ContainingNamespace.ToString());

			if (node.Expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax identifierName }
			    && variables.TryGetValue(identifierName.Identifier.Text, out var variable))
			{
				variable.IsAccessed = true;
				variable.IsAltered = true;
			}

			var expression = Visit(node.Expression) as ExpressionSyntax ?? node.Expression;

			if (expression is MemberAccessExpressionSyntax { Expression: CollectionExpressionSyntax collection } && targetMethod.IsMethod(typeof(Enumerable), "ToArray", "ToList"))
			{
				return collection;
			}

			// Return with the optimized/visited arguments
			return node
				.WithExpression(expression)
				.WithArgumentList(node.ArgumentList.WithArguments(SeparatedList(arguments.OfType<ExpressionSyntax>().Select(Argument))));
		}

		// Symbol not found, visit arguments normally using base implementation
		return node
			.WithExpression(Visit(node.Expression) as ExpressionSyntax ?? node.Expression)
			.WithArgumentList(VisitArgumentList(node.ArgumentList) as ArgumentListSyntax ?? node.ArgumentList);
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

				if (operation.Type.TryGetMinMaxValue(out var min, out var max))
				{
					item.MinValue = min;
					item.MaxValue = max;
				}

				variables.Add(name, item);
			}
			else
			{
				// Variable is already declared, convert this to an assignment instead
				if (node.Initializer is not null)
				{
					var assignment = AssignmentExpression(
						SyntaxKind.SimpleAssignmentExpression,
						IdentifierName(name),
						value as ExpressionSyntax ?? node.Initializer.Value);

					// Update the variable value
					if (value is IdentifierNameSyntax nameSyntax)
					{
						item.Value = nameSyntax;
						item.IsInitialized = true;
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

					return ExpressionStatement(assignment);
				}

				// No initializer on the duplicate declaration - just remove it
				return null;
			}

			if (value is IdentifierNameSyntax nameSyntax2)
			{
				item.Value = nameSyntax2;
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
				if (value is LiteralExpressionSyntax literal && operation.Symbol.Type?.SpecialType is SpecialType.System_Byte or SpecialType.System_SByte)
				{
					return node.WithInitializer(node.Initializer.WithValue(CastExpression(ParseTypeName(semanticModel.Compilation.GetMinimalString(operation.Symbol.Type)), literal)));
				}

				return node.WithInitializer(node.Initializer.WithValue(value as ExpressionSyntax ?? node.Initializer.Value));
			}
		}

		return base.VisitVariableDeclarator(node);
	}

	public override SyntaxNode? VisitVariableDeclaration(VariableDeclarationSyntax node)
	{
		var visitedVariables = new List<VariableDeclaratorSyntax>();
		var statements = new List<StatementSyntax>();

		foreach (var variable in node.Variables)
		{
			var visited = Visit(variable);

			switch (visited)
			{
				case VariableDeclaratorSyntax declarator:
					visitedVariables.Add(declarator);
					break;
				case ExpressionStatementSyntax expressionStatement:
					// This is a duplicate declaration converted to an assignment
					statements.Add(expressionStatement);
					break;
				case null:
					// Variable was removed (e.g., duplicate with no initializer)
					break;
			}
		}

		// If we have assignment statements from duplicate declarations,
		// we need to return them separately
		if (statements.Count > 0)
		{
			// If we also have valid declarations, create a block with both
			if (visitedVariables.Count > 0)
			{
				var declaration = node
					.WithType(visitedVariables.Count == 1 ? ParseTypeName("var") : node.Type)
					.WithVariables(SeparatedList(visitedVariables));

				return Block(
					List(
						new[] { LocalDeclarationStatement(declaration) }
							.Concat(statements)
					)
				);
			}

			// Only assignments, no declarations
			return statements.Count == 1
				? statements[0]
				: Block(statements);
		}

		// No duplicate declarations, just return the updated variable declaration
		if (visitedVariables.Count > 0)
		{
			return node
				.WithType(visitedVariables.Count == 1 ? ParseTypeName("var") : node.Type)
				.WithVariables(SeparatedList(visitedVariables));
		}

		// All variables were removed
		return null;
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
		var names = variables.Keys.ToImmutableHashSet();

		Visit(node.Declaration);

		var condition = Visit(node.Condition);

		if (TryGetLiteralValue(condition, out var value))
		{
			switch (value)
			{
				case false:
					// Condition is always false; remove the loop
					return null;
				case true:
				{
					// Skip loop unrolling if MaxUnrollIterations is 0
					if (attribute.MaxUnrollIterations == 0)
					{
						return base.VisitForStatement(node);
					}

					var result = new List<SyntaxNode?>();
					var iteratorCount = 0;

					do
					{
						if (iteratorCount++ >= attribute.MaxUnrollIterations)
						{
							foreach (var name in AssignedVariables(node))
							{
								if (variables.TryGetValue(name, out var variable))
								{
									variable.HasValue = false;
								}
							}

							return base.VisitForStatement(node);
						}

						var statement = Visit(node.Statement);

						if (statement is not BlockSyntax)
						{
							result.Add(statement);
						}

						// Check if statement contains break or return - if so, stop unrolling
						if (statement is BreakStatementSyntax or ReturnStatementSyntax)
						{
							break;
						}

						if (statement is BlockSyntax block && block.Statements.Any(s => s is BreakStatementSyntax or ReturnStatementSyntax))
						{
							foreach (var item in block.Statements)
							{
								if (item is BreakStatementSyntax)
								{
									break;
								}

								result.Add(item);

								if (item is ReturnStatementSyntax)
								{
									break;
								}
							}

							break;
						}

						VisitList(node.Incrementors);
					} while (TryGetLiteralValue(Visit(node.Condition), out value) && value is true);

					if (result.Count > 0)
					{
						return ToStatementSyntax(result);
					}

					return null;
				}
			}
		}

		// Restore variable states after visiting the loop
		foreach (var name in variables.Keys.Except(names).ToList())
		{
			variables.Remove(name);
		}

		var declaration = Visit(node.Declaration);
		var assignedVariables = AssignedVariables(node);

		foreach (var name in assignedVariables)
		{
			if (variables.TryGetValue(name, out var variable))
			{
				variable.HasValue = false;
			}
		}

		return node
			.WithInitializers(VisitList(node.Initializers))
			.WithCondition(Visit(node.Condition) as ExpressionSyntax ?? node.Condition)
			.WithDeclaration(declaration as VariableDeclarationSyntax ?? node.Declaration)
			.WithStatement(Visit(node.Statement) as StatementSyntax ?? node.Statement);
	}

	public override SyntaxNode? VisitAssignmentExpression(AssignmentExpressionSyntax node)
	{
		// Do not visit the left/target to avoid turning assignable expressions into constants.
		var visitedRight = Visit(node.Right);
		var rightExpr = visitedRight as ExpressionSyntax ?? node.Right;
		var kind = node.OperatorToken.Kind();

		var hasRightValue = TryGetLiteralValue(rightExpr, out var rightValue);

		// Handle Tuple deconstruction assignments: (a, b) = (1, 2)
		if (node.Left is TupleExpressionSyntax leftTuple && kind == SyntaxKind.EqualsToken)
		{
			// Try to get the right-hand side as a tuple value
			if (hasRightValue)
			{
				var rightType = rightValue?.GetType();

				// Check if it's a ValueTuple
				if (rightType != null && rightType.Name.StartsWith("ValueTuple"))
				{
					var tupleFields = rightType.GetFields();
					var leftArgs = leftTuple.Arguments;

					if (tupleFields.Length == leftArgs.Count)
					{
						// Update each variable in the tuple
						for (var i = 0; i < leftArgs.Count; i++)
						{
							if (leftArgs[i].Expression is IdentifierNameSyntax { Identifier.Text: var tupleName }
							    && variables.TryGetValue(tupleName, out var tupleVariable))
							{
								var fieldValue = tupleFields[i].GetValue(rightValue);
								tupleVariable.Value = fieldValue;
								tupleVariable.HasValue = true;
								tupleVariable.IsInitialized = true;
							}
						}

						// Return the assignment with the optimized right-hand side
						return node.WithRight(rightExpr);
					}
				}
			}
			// If right side is also a tuple expression, match elements
			else if (rightExpr is TupleExpressionSyntax rightTuple)
			{
				var leftArgs = leftTuple.Arguments;
				var rightArgs = rightTuple.Arguments;

				if (leftArgs.Count == rightArgs.Count)
				{
					// Mark all left-hand side variables as altered (they're being assigned to)
					// This MUST happen before we check allLeftVarsExist to prevent pruning
					for (var i = 0; i < leftArgs.Count; i++)
					{
						if (leftArgs[i].Expression is IdentifierNameSyntax { Identifier.Text: var leftVarName }
						    && variables.TryGetValue(leftVarName, out var leftVar))
						{
							leftVar.IsAltered = true;
						}
					}

					// Check if all left-hand side variables exist in our tracking dictionary
					var allLeftVarsExist = true;

					for (var i = 0; i < leftArgs.Count; i++)
					{
						if (leftArgs[i].Expression is IdentifierNameSyntax { Identifier.Text: var tupleName })
						{
							if (!variables.TryGetValue(tupleName, out _))
							{
								allLeftVarsExist = false;
								break;
							}
						}
						else
						{
							// Non-identifier on left side (e.g., member access)
							allLeftVarsExist = false;
							break;
						}
					}

					// Only try to optimize if all left-hand side variables are tracked
					if (allLeftVarsExist)
					{
						// Collect right-side values FIRST (to support swaps like (x, y) = (y, x))
						var rightValues = new object?[rightArgs.Count];
						var allRightValuesResolved = true;
						var newRightArgs = new ArgumentSyntax[rightArgs.Count];
						var hasOptimization = false;

						for (var i = 0; i < rightArgs.Count; i++)
						{
							if (TryGetLiteralValue(rightArgs[i].Expression, out var value))
							{
								rightValues[i] = value;
								newRightArgs[i] = rightArgs[i];
							}
							else if (rightArgs[i].Expression is IdentifierNameSyntax rightVarName
							         && variables.TryGetValue(rightVarName.Identifier.Text, out var rightVar)
							         && rightVar.HasValue)
							{
								// Store the current value for swapping
								rightValues[i] = rightVar.Value;

								// Replace identifier with literal value if possible
								if (TryGetLiteral(rightVar.Value, out var literal))
								{
									newRightArgs[i] = SyntaxFactory.Argument(literal);
									hasOptimization = true;
								}
								else
								{
									// Keep the identifier and mark as accessed to prevent pruning
									newRightArgs[i] = rightArgs[i];
									rightVar.IsAccessed = true;
								}
							}
							else
							{
								// Cannot resolve this value, keep original
								allRightValuesResolved = false;
								newRightArgs[i] = rightArgs[i];

								// Mark identifier as accessed if it's a variable reference
								if (rightArgs[i].Expression is IdentifierNameSyntax varName
								    && variables.TryGetValue(varName.Identifier.Text, out var varItem))
								{
									varItem.IsAccessed = true;
								}
							}
						}

						// Update left-hand side variables AFTER collecting all right-side values
						if (allRightValuesResolved)
						{
							for (var i = 0; i < leftArgs.Count; i++)
							{
								if (leftArgs[i].Expression is IdentifierNameSyntax { Identifier.Text: var tupleName }
								    && variables.TryGetValue(tupleName, out var tupleVariable))
								{
									tupleVariable.Value = rightValues[i];
									tupleVariable.HasValue = true;
									tupleVariable.IsInitialized = true;
									// IsAltered is already set earlier for all left-hand variables
								}
							}
						}

						// Return optimized tuple if we made any changes
						if (hasOptimization || !allRightValuesResolved)
						{
							var optimizedRightTuple = SyntaxFactory.TupleExpression(
								SyntaxFactory.SeparatedList(newRightArgs));
							return node.WithRight(optimizedRightTuple);
						}
					}
					else
					{
						// Not all left variables are tracked, mark right-side identifiers as accessed
						for (var i = 0; i < rightArgs.Count; i++)
						{
							if (rightArgs[i].Expression is IdentifierNameSyntax rightVarName
							    && variables.TryGetValue(rightVarName.Identifier.Text, out var rightVar))
							{
								rightVar.IsAccessed = true;
							}
						}
					}

					return node.WithRight(rightExpr);
				}
			}
		}

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

				// var result = LocalDeclarationStatement(VariableDeclaration(ParseTypeName("var"), SingletonSeparatedList(
				// 	VariableDeclarator(Identifier(name))
				// 		.WithInitializer(EqualsValueClause(rightExpr)))
				// ));
				//
				// return result;
			}

			// Only simplify assignments when the variable has a known constant value
			// For compound assignments (+=, -=, etc.), only simplify if we can compute the result
			if (TryGetLiteralValue(rightExpr, out var tempValue) && variable.HasValue)
			{
				variable.Value = ObjectExtensions.ExecuteBinaryOperation(kind, variable.Value, tempValue) ?? tempValue;
				variable.HasValue = true;

				// Only convert to simple assignment with literal if it's already a simple assignment
				// or if we're dealing with constant values on both sides
				if (kind == SyntaxKind.SimpleAssignmentExpression && TryGetLiteral(tempValue, out var literal))
				{
					return node.WithRight(literal);
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
											var getOffset = arg0.GetType().GetMethod("GetOffset", [ typeof(int) ]);
											var offset = getOffset?.Invoke(arg0, [ arr.Length ]);

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
									else
									{
										if (indexConsts.All(a => a is int))
										{
											arr.SetValue(rightVal, indexConsts.OfType<int>().ToArray());

											return rightExpr;
										}

										if (indexConsts.All(a => a is long))
										{
											arr.SetValue(rightVal, indexConsts.OfType<long>().ToArray());

											return rightExpr;
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

		if (node.IsKind(SyntaxKind.AddAssignmentExpression) && hasRightValue)
		{
			if (rightValue.IsNumericZero())
			{
				return null;
			}

			if (rightValue.IsNumericOne())
			{
				return PostfixUnaryExpression(SyntaxKind.PostIncrementExpression, node.Left);
			}
		}

		return node
			.WithLeft(Visit(node.Left) as ExpressionSyntax ?? node.Left)
			.WithRight(rightExpr);
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
							if (loader.TryExecuteMethod(op.OperatorMethod, null, new VariableItemDictionary(variables), [ current ], out var res))
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
					return TryGetLiteral(updated, out var lit) ? lit : (SyntaxNode) node.WithOperand(id);
				}
				else
				{
					variable.IsAltered = true;
				}
			}
		}
		else if (node.OperatorToken.IsKind(SyntaxKind.ExclamationToken)
		         && TryGetLiteralValue(operand, out var value)
		         && value is bool logicalBool)
		{
			return CreateLiteral(!logicalBool);
		}

		if (semanticModel.GetOperation(node) is IUnaryOperation { ConstantValue.HasValue: true } operation)
		{
			if (operation.Parent is IConversionOperation conversionOperation
				&& TryGetLiteral(conversionOperation.ConstantValue.Value, out var lit))
			{
				return lit;
			}
			else if (TryGetLiteral(operation.ConstantValue.Value, out lit))
			{
				return lit;
			}
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
							if (loader.TryExecuteMethod(op.OperatorMethod, null, new VariableItemDictionary(variables), [ current ], out var res))
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


					return TryGetLiteral(current, out var lit) ? lit : (SyntaxNode) node.WithOperand(id);
				}
				else
				{
					variable.IsAltered = true;
				}
			}
		}

		return base.VisitPostfixUnaryExpression(node);
	}

	public override SyntaxNode? VisitParenthesizedExpression(ParenthesizedExpressionSyntax node)
	{
		var expression = Visit(node.Expression);

		if (node.WithExpression(expression as ExpressionSyntax ?? node.Expression).CanRemoveParentheses(semanticModel, token)
		    || expression is ParenthesizedExpressionSyntax or IdentifierNameSyntax or LiteralExpressionSyntax or InvocationExpressionSyntax or ObjectCreationExpressionSyntax or IsPatternExpressionSyntax)
		{
			return expression;
		}

		return node.WithExpression(expression as ExpressionSyntax ?? node.Expression);
	}

	public override SyntaxNode? VisitCastExpression(CastExpressionSyntax node)
	{
		if (semanticModel.TryGetSymbol(node.Type, out ITypeSymbol? symbol))
		{
			var expression = Visit(node.Expression);

			if (TryGetLiteralValue(expression, out var value) || TryGetLiteralValue(node.Expression, out value))
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
							if (loader.TryExecuteMethod(operation.OperatorMethod, null, new VariableItemDictionary(variables), [ value ], out var result)
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
							return !inner;
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
		if (semanticModel.TryGetSymbol(node, out IMethodSymbol? method))
		{
			foreach (var methodParameter in method.Parameters)
			{
				variables.TryAdd(methodParameter.Name, new VariableItem(methodParameter.Type, false, null, true));
			}
		}

		if (node.Block is not null)
		{
			var block = Visit(node.Block);

			return node.WithBlock(block as BlockSyntax ?? node.Block);
		}

		var body = Visit(node.Body);

		return node.WithBody(body as CSharpSyntaxNode ?? node.Body);
	}

	public override SyntaxNode? VisitElementAccessExpression(ElementAccessExpressionSyntax node)
	{
		var instance = Visit(node.Expression);

		var arguments = node.ArgumentList.Arguments
			.Select(arg => Visit(arg.Expression));

		var constantArguments = arguments
			.WhereSelect<SyntaxNode, object?>(TryGetLiteralValue)
			.ToArray();

		if (TryGetLiteralValue(node.Expression, out var instanceValue) || TryGetLiteralValue(instance, out instanceValue))
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
										var getOffsetAndLength = arg.GetType().GetMethod("GetOffsetAndLength", [ typeof(int) ]);

										if (getOffsetAndLength is not null)
										{
											var tuple = getOffsetAndLength.Invoke(arg, [ arr.Length ]);

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
										var getOffset = arg.GetType().GetMethod("GetOffset", [ typeof(int) ]);

										var offset = getOffset?.Invoke(arg, [ arr.Length ]);

										if (offset is int idx)
										{
											var value = arr.GetValue(idx);

											if (TryGetLiteral(value, out var literal))
											{
												return literal;
											}
											// TODO: Handle case where index is valid but value is not a compile-time constant
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
									// TODO: Handle case where all indices are ints but value is not a compile-time constant
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
						.WithArguments(SeparatedList(arguments.Select(s => Argument((ExpressionSyntax) s)))));
			}
		}

		return base.VisitElementAccessExpression(node);
	}

	public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
	{
		var expression = Visit(node.Expression);
		var hasLiteral = TryGetLiteralValue(node.Expression, out var instanceValue);

		if (!hasLiteral)
		{
			hasLiteral = TryGetLiteralValue(expression, out instanceValue);
		}

		if (semanticModel.TryGetSymbol(node, out ISymbol? symbol))
		{
			switch (symbol)
			{
				case IFieldSymbol fieldSymbol:
					if (fieldSymbol.ContainingType.EnumUnderlyingType is not null)
					{
						return node;
					}

					if (loader.TryGetFieldValue(fieldSymbol, instanceValue, out var value)
					    && TryGetLiteral(value, out var literal))
					{
						return literal;
					}
					break;
				case IPropertySymbol propertySymbol:
					if (propertySymbol.Parameters.Length == 0)
					{
						if (loader.TryExecuteMethod(propertySymbol.GetMethod, instanceValue, new VariableItemDictionary(variables), [ ], out value)
						    && TryGetLiteral(value, out literal))
						{
							return literal;
						}
					}
					break;
			}
		}

		if (hasLiteral && instanceValue != null && TryGetLiteral(instanceValue, out var instanceLiteral))
		{
			return node.WithExpression(instanceLiteral);
		}

		return node.WithExpression(expression as ExpressionSyntax ?? node.Expression);
	}

	public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
	{
		var names = variables.Keys.ToImmutableHashSet();
		var collection = Visit(node.Expression);

		var items = collection switch
		{
			CollectionExpressionSyntax collectionExpression => collectionExpression.Elements,
			LiteralExpressionSyntax { RawKind: (int) SyntaxKind.StringLiteralExpression } stringLiteral => (IReadOnlyList<CSharpSyntaxNode>) stringLiteral.Token.ValueText.Select(s => CreateLiteral(s) as CSharpSyntaxNode).ToList(),
			_ => null,
		};

		if (items is not null && attribute.MaxUnrollIterations > 0 && items.Count < attribute.MaxUnrollIterations)
		{
			var name = node.Identifier.Text;

			if (semanticModel.GetOperation(node) is IForEachLoopOperation operation)
			{
				var variable = new VariableItem(operation.LoopControlVariable.Type, true, null, true);
				variables.Add(name, variable);

				var statements = new List<SyntaxNode>();

				foreach (var item in items)
				{
					if (TryGetLiteralValue(item, out var val))
					{
						variable.Value = val;

						var statement = Visit(node.Statement);

						if (statement is not BlockSyntax)
						{
							statements.Add(statement);
						}

						// Check if statement contains break or return - if so, stop unrolling
						if (statement is BreakStatementSyntax or ReturnStatementSyntax)
						{
							break;
						}

						if (statement is BlockSyntax block)
						{
							if (block.Statements.Any(s => s is BreakStatementSyntax or ReturnStatementSyntax))
							{
								foreach (var item2 in block.Statements)
								{
									if (item2 is BreakStatementSyntax)
									{
										break;
									}

									statements.Add(item2);

									if (item2 is ReturnStatementSyntax)
									{
										break;
									}
								}

								break;
							}

							statements.Add(block);
						}
					}
				}

				return ToStatementSyntax(statements);
			}
		}

		var assignedVariables = AssignedVariables(node);

		foreach (var name in names)
		{
			if (variables.TryGetValue(name, out var variable)
			    && assignedVariables.Contains(name))
			{
				variable.HasValue = false;
			}
		}

		return base.VisitForEachStatement(node);
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

	public override SyntaxNode? VisitTupleExpression(TupleExpressionSyntax node)
	{
		var arguments = node.Arguments
			.Select(arg => Visit(arg.Expression))
			.ToList();

		var constantArguments = arguments
			.WhereSelect<SyntaxNode?, object?>(TryGetLiteralValue)
			.ToArray();

		// If all tuple elements are constant, create a tuple literal
		if (constantArguments.Length == arguments.Count)
		{
			if (TryGetOperation(semanticModel, node, out ITupleOperation? operation))
			{
				var tupleType = operation.Type ?? semanticModel.GetTypeInfo(node, token).Type;

				if (tupleType is not null)
				{
					try
					{
						var tuple = Activator.CreateInstance(tupleType.GetType(), constantArguments);

						if (TryGetLiteral(tuple, out var literal))
						{
							return literal;
						}
					}
					catch
					{
						// Fall through to return updated tuple expression
					}
				}
			}
		}

		// Return tuple with visited arguments
		return node.WithArguments(
			SeparatedList(arguments
				.Select((arg, i) => Argument(arg as ExpressionSyntax ?? node.Arguments[i].Expression))));
	}

	public override SyntaxNode? VisitConditionalExpression(ConditionalExpressionSyntax node)
	{
		var condition = Visit(node.Condition);

		if (TryGetLiteralValue(condition, out var value) && value is bool b)
		{
			return b ? Visit(node.WhenTrue) : Visit(node.WhenFalse);
		}

		return node
			.WithCondition(condition as ExpressionSyntax ?? node.Condition)
			.WithWhenTrue(Visit(node.WhenTrue) as ExpressionSyntax ?? node.WhenTrue)
			.WithWhenFalse(Visit(node.WhenFalse) as ExpressionSyntax ?? node.WhenFalse);
	}

	public override SyntaxNode? VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
	{
		if (semanticModel.TryGetSymbol(node.Type, out ITypeSymbol? type))
		{
			usings.Add(type.ContainingNamespace.ToDisplayString());

			if (type.EqualsType(semanticModel.Compilation.GetTypeByMetadataName("System.Random")))
			{
				return base.VisitObjectCreationExpression(node);
			}

			// Try to create the object and convert it to a literal
			var runtimeType = loader.GetType(type);

			if (runtimeType != null)
			{
				try
				{
					var arguments = node.ArgumentList?.Arguments
						.Select(arg => Visit(arg.Expression))
						.OfType<ExpressionSyntax>()
						.ToList() ?? [ ];



					// Extract literal values from arguments
					var argumentValues = arguments
						.WhereSelect<ExpressionSyntax, object?>(TryGetLiteralValue)
						.ToList();

					// Only proceed if all arguments have literal values
					if (arguments.Count == argumentValues.Count)
					{
						var constructors = runtimeType.GetConstructors();
						var matchingConstructor = constructors.FirstOrDefault(c => c.GetParameters().Length == arguments.Count);

						if (matchingConstructor != null)
						{
							try
							{
								// Invoke the constructor with literal values
								var constructedObject = matchingConstructor.Invoke(argumentValues.ToArray());

								// Try to convert the constructed object to a literal
								if (TryGetLiteral(constructedObject, out var literalExpression))
								{
									return literalExpression;
								}
							}
							catch (Exception ex)
							{
								exceptionHandler(node, ex);
							}
						}
					}
				}
				catch (Exception ex)
				{
					exceptionHandler(node, ex);
				}
			}
		}

		return base.VisitObjectCreationExpression(node);
	}

	public override SyntaxNode? VisitQualifiedName(QualifiedNameSyntax node)
	{
		usings.Add(node.Left.ToString());

		return node.Right;
	}

	public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node)
	{
		var condition = Visit(node.Condition);

		if (TryGetLiteralValue(condition, out var value))
		{
			if (value is false)
			{
				// Condition is always false; remove the loop
				return null;
			}

			if (value is true)
			{
				// Skip loop unrolling if MaxUnrollIterations is 0
				if (attribute.MaxUnrollIterations == 0)
				{
					return base.VisitWhileStatement(node);
				}

				var result = new List<SyntaxNode?>();
				var iteratorCount = 0;

				do
				{
					if (iteratorCount++ >= attribute.MaxUnrollIterations)
					{
						foreach (var name in AssignedVariables(node))
						{
							if (variables.TryGetValue(name, out var variable))
							{
								variable.HasValue = false;
							}
						}

						return base.VisitWhileStatement(node);
					}

					var statement = Visit(node.Statement);

					if (statement is not BlockSyntax)
					{
						result.Add(statement);
					}

					// Check if statement contains break or return - if so, stop unrolling
					if (statement is BreakStatementSyntax or ReturnStatementSyntax)
					{
						break;
					}

					if (statement is BlockSyntax block && block.Statements.Any(s => s is BreakStatementSyntax or ReturnStatementSyntax))
					{
						foreach (var item in block.Statements)
						{
							if (item is BreakStatementSyntax)
							{
								break;
							}

							result.Add(item);

							if (item is ReturnStatementSyntax)
							{
								break;
							}
						}

						break;
					}
				} while (TryGetLiteralValue(Visit(node.Condition), out value) && value is true);

				if (result.Count > 0)
				{
					return ToStatementSyntax(result);
				}

				return null;
			}
		}

		foreach (var name in AssignedVariables(node))
		{
			if (variables.TryGetValue(name, out var variable))
			{
				variable.HasValue = false;
			}
		}

		return base.VisitWhileStatement(node);
	}

	public override SyntaxNode? VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
	{
		var visited = Visit(node.Declaration);

		return visited switch
		{
			null =>
				// All variables were removed
				null,
			BlockSyntax block =>
				// VisitVariableDeclaration returned a block (declaration + assignments)
				// Return the block to replace the local declaration statement
				block,
			VariableDeclarationSyntax declaration =>
				// Normal case: return the updated declaration
				node.WithDeclaration(declaration),
			ExpressionStatementSyntax expressionStatement =>
				// Only assignments, no declarations
				expressionStatement,
			_ => node
		};
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
}