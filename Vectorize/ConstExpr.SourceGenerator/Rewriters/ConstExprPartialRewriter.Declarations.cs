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
/// Declaration and assignment visitor methods for the ConstExprPartialRewriter.
/// Handles variable declarations, assignments, and related operations.
/// </summary>
public partial class ConstExprPartialRewriter
{
	public override SyntaxNode? VisitVariableDeclarator(VariableDeclaratorSyntax node)
	{
		var value = Visit(node.Initializer?.Value);

		if (!TryGetOperation(semanticModel, node, out IVariableDeclaratorOperation? operation))
		{
			return base.VisitVariableDeclarator(node);
		}

		var name = operation.Symbol.Name;

		if (!variables.TryGetValue(name, out var item))
		{
			return HandleNewVariableDeclaration(node, operation, name, value);
		}

		return HandleExistingVariableDeclaration(node, item, name, value);
	}

	/// <summary>
	/// Handles declaration of a new variable.
	/// </summary>
	private SyntaxNode HandleNewVariableDeclaration(VariableDeclaratorSyntax node, IVariableDeclaratorOperation operation, string name, SyntaxNode? value)
	{
		var item = new VariableItem(operation.Type ?? operation.Symbol.Type, true, value);

		if (operation.Type.TryGetMinMaxValue(out var min, out var max))
		{
			item.MinValue = min;
			item.MaxValue = max;
		}

		variables.Add(name, item);

		UpdateVariableValue(item, operation, value, node.Initializer?.Value);

		if (node.Initializer is null)
		{
			return node;
		}

		// Handle byte/sbyte literals that need casting
		if (value is LiteralExpressionSyntax literal && operation.Symbol.Type?.SpecialType is SpecialType.System_Byte or SpecialType.System_SByte)
		{
			return node.WithInitializer(node.Initializer.WithValue(
				CastExpression(ParseTypeName(semanticModel.Compilation.GetMinimalString(operation.Symbol.Type)), literal)));
		}

		return node.WithInitializer(node.Initializer.WithValue(value as ExpressionSyntax ?? node.Initializer.Value));
	}

	/// <summary>
	/// Handles declaration of an existing variable (duplicate).
	/// </summary>
	private SyntaxNode? HandleExistingVariableDeclaration(VariableDeclaratorSyntax node, VariableItem item, string name, SyntaxNode? value)
	{
		// Variable is already declared, convert this to an assignment instead
		if (node.Initializer is not null)
		{
			var assignment = AssignmentExpression(
				SyntaxKind.SimpleAssignmentExpression,
				IdentifierName(name),
				value as ExpressionSyntax ?? node.Initializer.Value);

			UpdateVariableFromInitializer(item, value, node.Initializer.Value);

			return ExpressionStatement(assignment);
		}

		// No initializer on the duplicate declaration - just remove it
		return null;
	}

	/// <summary>
	/// Updates the variable value from an initializer.
	/// </summary>
	private void UpdateVariableFromInitializer(VariableItem item, SyntaxNode? value, ExpressionSyntax initializerValue)
	{
		if (value is IdentifierNameSyntax nameSyntax)
		{
			item.Value = nameSyntax;
		}
		else if (TryGetLiteralValue(initializerValue, out var result) || TryGetLiteralValue(value, out result))
		{
			item.Value = result;
		}
		else
		{
			item.HasValue = false;
		}

		item.IsInitialized = true;
	}

	/// <summary>
	/// Updates variable value based on operation and initializer.
	/// </summary>
	private void UpdateVariableValue(VariableItem item, IVariableDeclaratorOperation operation, SyntaxNode? value, ExpressionSyntax? initializerValue)
	{
		if (value is IdentifierNameSyntax nameSyntax)
		{
			item.Value = nameSyntax;
			item.IsInitialized = true;
		}
		else if (operation.Initializer is null && operation.Symbol is { } local)
		{
			item.Value = local.Type.GetDefaultValue();
			item.IsInitialized = false;
		}
		else if (TryGetLiteralValue(initializerValue, out var result) || TryGetLiteralValue(value, out result))
		{
			item.Value = result;
			item.IsInitialized = true;
		}
		else
		{
			item.HasValue = false;
			item.IsInitialized = true;
		}
	}

	public override SyntaxNode? VisitVariableDeclaration(VariableDeclarationSyntax node)
	{
		var visitedVariables = new List<VariableDeclaratorSyntax>();
		var statements = new List<StatementSyntax>();

		foreach (var visited in node.Variables.Select(Visit))
		{
			switch (visited)
			{
				case VariableDeclaratorSyntax declarator:
					visitedVariables.Add(declarator);
					break;
				case ExpressionStatementSyntax expressionStatement:
					statements.Add(expressionStatement);
					break;
			}
		}

		return BuildVariableDeclarationResult(node, visitedVariables, statements);
	}

	/// <summary>
	/// Builds the result for a variable declaration.
	/// </summary>
	private SyntaxNode? BuildVariableDeclarationResult(VariableDeclarationSyntax node, List<VariableDeclaratorSyntax> visitedVariables, List<StatementSyntax> statements)
	{
		if (statements.Count > 0)
		{
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

			return statements.Count == 1 ? statements[0] : Block(statements);
		}

		if (visitedVariables.Count > 0)
		{
			return node
				.WithType(visitedVariables.Count == 1 ? ParseTypeName("var") : node.Type)
				.WithVariables(SeparatedList(visitedVariables));
		}

		return null;
	}

	public override SyntaxNode? VisitAssignmentExpression(AssignmentExpressionSyntax node)
	{
		var visitedRight = Visit(node.Right);
		var rightExpr = visitedRight as ExpressionSyntax ?? node.Right;
		var kind = node.OperatorToken.Kind();
		var hasRightValue = TryGetLiteralValue(rightExpr, out var rightValue);

		switch (node.Left)
		{
			// Handle Tuple deconstruction assignments
			case TupleExpressionSyntax leftTuple when kind == SyntaxKind.EqualsToken:
				return HandleTupleAssignment(node, leftTuple, rightExpr, hasRightValue, rightValue);
			// Handle identifier assignments
			case IdentifierNameSyntax { Identifier.Text: var name } when variables.TryGetValue(name, out var variable):
				return HandleIdentifierAssignment(node, variable, rightExpr, kind);
			// Handle element access assignments
			case ElementAccessExpressionSyntax elementAccess:
			{
				var result = HandleElementAccessAssignment(node, elementAccess, rightExpr, hasRightValue, rightValue);

				if (result is not null)
				{
					return result;
				}
				break;
			}
		}

		// Try compound assignment optimization for non-tracked variables
		if (TryGetOperation(semanticModel, node, out ICompoundAssignmentOperation? compOp) 
		    && TryOptimizeNode(compOp.OperatorKind, compOp.Type, node.Left, compOp.Target.Type, rightExpr, compOp.Value.Type, out var syntaxNode))
		{
			// If the optimized node is a binary expression where left matches the assignment target,
			// try to convert it to a compound assignment (e.g., x << 1 becomes x <<= 1)
			if (syntaxNode is BinaryExpressionSyntax binaryExpr 
			    && binaryExpr.Left.ToString() == node.Left.ToString())
			{
				var compoundKind = TryGetCompoundAssignmentKind(binaryExpr.Kind());
				
				if (compoundKind != SyntaxKind.None)
				{
					return AssignmentExpression(compoundKind, node.Left, binaryExpr.Right);
				}
			}
				
			return AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, node.Left, syntaxNode as ExpressionSyntax);
		}

		// Optimize add assignment
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

	/// <summary>
	/// Handles tuple deconstruction assignments.
	/// </summary>
	private SyntaxNode? HandleTupleAssignment(AssignmentExpressionSyntax node, TupleExpressionSyntax leftTuple, ExpressionSyntax rightExpr, bool hasRightValue, object? rightValue)
	{
		if (hasRightValue)
		{
			var rightType = rightValue?.GetType();

			if (rightType != null && rightType.Name.StartsWith("ValueTuple"))
			{
				return HandleValueTupleAssignment(node, leftTuple, rightExpr, rightValue, rightType);
			}
		}
		else if (rightExpr is TupleExpressionSyntax rightTuple)
		{
			return HandleTupleToTupleAssignment(node, leftTuple, rightTuple, rightExpr);
		}

		return node.WithRight(rightExpr);
	}

	/// <summary>
	/// Handles ValueTuple assignment.
	/// </summary>
	private SyntaxNode HandleValueTupleAssignment(AssignmentExpressionSyntax node, TupleExpressionSyntax leftTuple, ExpressionSyntax rightExpr, object? rightValue, Type rightType)
	{
		var tupleFields = rightType.GetFields();
		var leftArgs = leftTuple.Arguments;

		if (tupleFields.Length == leftArgs.Count)
		{
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
		}

		return node.WithRight(rightExpr);
	}

	/// <summary>
	/// Handles tuple-to-tuple assignment.
	/// </summary>
	private SyntaxNode HandleTupleToTupleAssignment(AssignmentExpressionSyntax node, TupleExpressionSyntax leftTuple, TupleExpressionSyntax rightTuple, ExpressionSyntax rightExpr)
	{
		var leftArgs = leftTuple.Arguments;
		var rightArgs = rightTuple.Arguments;

		if (leftArgs.Count != rightArgs.Count)
		{
			return node.WithRight(rightExpr);
		}

		// Mark all left-hand side variables as altered
		foreach (var leftArg in leftArgs)
		{
			if (leftArg.Expression is IdentifierNameSyntax { Identifier.Text: var leftVarName }
			    && variables.TryGetValue(leftVarName, out var leftVar))
			{
				leftVar.IsAltered = true;
			}
		}

		// Check if all left-hand side variables exist
		var allLeftVarsExist = leftArgs.All(arg =>
			arg.Expression is IdentifierNameSyntax { Identifier.Text: var tupleName }
			&& variables.ContainsKey(tupleName));

		if (!allLeftVarsExist)
		{
			return node.WithRight(rightExpr);
		}

		return OptimizeTupleAssignment(node, leftArgs, rightArgs, rightExpr);
	}


	/// <summary>
	/// Optimizes tuple assignment.
	/// </summary>
	private SyntaxNode OptimizeTupleAssignment(AssignmentExpressionSyntax node, SeparatedSyntaxList<ArgumentSyntax> leftArgs, SeparatedSyntaxList<ArgumentSyntax> rightArgs, ExpressionSyntax rightExpr)
	{
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
				rightValues[i] = rightVar.Value;

				if (TryGetLiteral(rightVar.Value, out var literal))
				{
					newRightArgs[i] = Argument(literal);
					hasOptimization = true;
				}
				else
				{
					newRightArgs[i] = rightArgs[i];
				}
			}
			else
			{
				allRightValuesResolved = false;
				newRightArgs[i] = rightArgs[i];
			}
		}

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
				}
			}
		}

		if (hasOptimization || !allRightValuesResolved)
		{
			var optimizedRightTuple = TupleExpression(SeparatedList(newRightArgs));
			return node.WithRight(optimizedRightTuple);
		}

		return node.WithRight(rightExpr);
	}

	/// <summary>
	/// Handles identifier assignment.
	/// </summary>
	private SyntaxNode? HandleIdentifierAssignment(AssignmentExpressionSyntax node, VariableItem variable, ExpressionSyntax rightExpr, SyntaxKind kind)
	{
		if (!variable.IsInitialized)
		{
			InitializeVariable(variable, rightExpr);
			variable.IsInitialized = true;
		}

		// Try to get the literal value from the right expression
		if (!TryGetLiteralValue(rightExpr, out var tempValue))
		{
			variable.HasValue = false;
			variable.IsAltered = true;
			
			return node
				.WithLeft(Visit(node.Left) as ExpressionSyntax ?? node.Left)
				.WithRight(rightExpr);
		}

		// For simple assignment, just set the value directly
		if (kind == SyntaxKind.EqualsToken)
		{
			variable.Value = tempValue;
			variable.HasValue = true;

			if (TryGetLiteral(tempValue, out var literal))
			{
				return node.WithRight(literal);
			}
			
			return node
				.WithLeft(Visit(node.Left) as ExpressionSyntax ?? node.Left)
				.WithRight(rightExpr);
		}

		// For compound assignments, we need the current variable value
		if (!variable.HasValue)
		{
			variable.HasValue = false;
			variable.IsAltered = true;
			
			// Try compound assignment optimization even when variable value is unknown
			if (TryGetOperation(semanticModel, node, out ICompoundAssignmentOperation? compOp)
			    && TryOptimizeNode(compOp.OperatorKind, compOp.Type, node.Left, compOp.Target.Type, rightExpr, compOp.Value.Type, out var optimizedNode))
			{
				// If the optimized node is a binary expression where left matches the assignment target,
				// convert it to a compound assignment (e.g., x << 1 becomes x <<= 1)
				if (optimizedNode is BinaryExpressionSyntax binaryExpr 
				    && binaryExpr.Left.ToString() == node.Left.ToString())
				{
					var compoundKind = TryGetCompoundAssignmentKind(binaryExpr.Kind());
					if (compoundKind != SyntaxKind.None)
					{
						return AssignmentExpression(compoundKind, node.Left, binaryExpr.Right);
					}
				}
				
				return AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, node.Left, optimizedNode as ExpressionSyntax ?? rightExpr);
			}
			
			return node
				.WithLeft(Visit(node.Left) as ExpressionSyntax ?? node.Left)
				.WithRight(rightExpr);
		}

		// Compute the new value using the compound operation
		var newValue = ObjectExtensions.ExecuteBinaryOperation(kind, variable.Value, tempValue);
		
		if (newValue is not null)
		{
			variable.Value = newValue;
			variable.HasValue = true;
			
			// The assignment can be removed since we've computed the value
			return null;
		}

		// Could not compute, mark as altered
		variable.HasValue = false;
		variable.IsAltered = true;
		
		return node
			.WithLeft(Visit(node.Left) as ExpressionSyntax ?? node.Left)
			.WithRight(rightExpr);
	}

	/// <summary>
	/// Initializes a variable from an expression.
	/// </summary>
	private void InitializeVariable(VariableItem variable, ExpressionSyntax rightExpr)
	{
		if (rightExpr is IdentifierNameSyntax nameSyntax)
		{
			variable.Value = nameSyntax;
			variable.HasValue = true;
		}
		else if (TryGetLiteralValue(rightExpr, out var value))
		{
			variable.Value = ObjectExtensions.ExecuteBinaryOperation(SyntaxKind.EqualsToken, variable.Value, value) ?? value;
			variable.HasValue = true;
		}
		else
		{
			variable.HasValue = false;
		}
	}

	/// <summary>
	/// Handles element access assignment.
	/// </summary>
	private SyntaxNode? HandleElementAccessAssignment(AssignmentExpressionSyntax node, ElementAccessExpressionSyntax elementAccess, ExpressionSyntax rightExpr, bool hasRightValue, object? rightValue)
	{
		if (!hasRightValue || !TryGetLiteralValue(elementAccess.Expression, out var instanceVal))
		{
			return null;
		}

		if (!TryGetOperation(semanticModel, elementAccess, out IOperation? op))
		{
			return null;
		}

		var indexConsts = elementAccess.ArgumentList.Arguments
			.Select(a => a.Expression)
			.WhereSelect<SyntaxNode, object?>(TryGetLiteralValue)
			.ToArray();

		switch (op)
		{
			case IArrayElementReferenceOperation arrayOp:
				return HandleArrayElementAssignment(instanceVal as Array, indexConsts, arrayOp.Indices.Length, rightValue, rightExpr);

			case IPropertyReferenceOperation propOp:
				if (propOp.Property.IsIndexer && instanceVal is not null && indexConsts.Length == propOp.Arguments.Length
				    && loader.TryExecuteMethod(propOp.Property.SetMethod, instanceVal, new VariableItemDictionary(variables), indexConsts.Append(rightValue), out _))
				{
					return null;
				}
				break;
		}

		return null;
	}

	/// <summary>
	/// Handles array element assignment.
	/// </summary>
	private SyntaxNode? HandleArrayElementAssignment(Array? arr, object?[] indexConsts, int indicesLength, object? rightValue, ExpressionSyntax rightExpr)
	{
		if (arr is null || indexConsts.Length != indicesLength)
		{
			return null;
		}

		try
		{
			if (indexConsts.Length == 1)
			{
				var arg0 = indexConsts[0];

				// Handle System.Index
				if (arg0 is not null && (arg0.GetType().FullName == "System.Index" || arg0.GetType().Name == "Index"))
				{
					var getOffset = arg0.GetType().GetMethod("GetOffset", [typeof(int)]);
					var offset = getOffset?.Invoke(arg0, [arr.Length]);

					if (offset is int idx)
					{
						arr.SetValue(rightValue, idx);
						return rightExpr;
					}
				}
				else switch (arg0)
				{
					case int i0:
						arr.SetValue(rightValue, i0);
						return rightExpr;
					case long l0:
						arr.SetValue(rightValue, l0);
						return rightExpr;
				}
			}
			else
			{
				if (indexConsts.All(a => a is int))
				{
					arr.SetValue(rightValue, indexConsts.OfType<int>().ToArray());
					return rightExpr;
				}

				if (indexConsts.All(a => a is long))
				{
					arr.SetValue(rightValue, indexConsts.OfType<long>().ToArray());
					return rightExpr;
				}
			}
		}
		catch { }

		return null;
	}
}

