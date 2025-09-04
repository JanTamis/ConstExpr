using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ConstExpr.SourceGenerator.Visitors;

public class ConstExprPartialVisitor(Compilation compilation, MetadataLoader loader, Action<IOperation?, Exception> exceptionHandler, CancellationToken token) : OperationVisitor<IDictionary<string, object?>, SyntaxNode>
{
	public override SyntaxNode? DefaultVisit(IOperation operation, IDictionary<string, object?> argument)
	{
		if (operation.ConstantValue is { HasValue: true, Value: var value } && SyntaxHelpers.TryGetLiteral(value, out var expression))
		{
			return expression;
		}

		// exceptionHandler(operation, new NotImplementedException($"Operation of type {operation.Kind} is not supported."));

		return operation.Syntax;
	}

	public override SyntaxNode? VisitExpressionStatement(IExpressionStatementOperation operation, IDictionary<string, object?> argument)
	{
		return Visit(operation.Operation, argument);
	}

	public override SyntaxNode? VisitBlock(IBlockOperation operation, IDictionary<string, object?> argument)
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

	public override SyntaxNode? VisitParameterReference(IParameterReferenceOperation operation, IDictionary<string, object?> argument)
	{
		if (argument.TryGetValue(operation.Parameter.Name, out var value) && SyntaxHelpers.TryGetLiteral(value, out var expression))
		{
			return expression;
		}

		return operation.Syntax;
	}

	public override SyntaxNode? VisitLocalReference(ILocalReferenceOperation operation, IDictionary<string, object?> argument)
	{
		if (argument.TryGetValue(operation.Local.Name, out var value) && SyntaxHelpers.TryGetLiteral(value, out var expression))
		{
			return expression;
		}

		return operation.Syntax;
	}

	public override SyntaxNode? VisitBinaryOperator(IBinaryOperation operation, IDictionary<string, object?> argument)
	{
		if (operation.Syntax is BinaryExpressionSyntax binary)
		{
			var left = Visit(operation.LeftOperand, argument);
			var right = Visit(operation.RightOperand, argument);

			if (SyntaxHelpers.TryGetConstantValue(compilation, loader, left, argument, token, out var leftValue)
				&& SyntaxHelpers.TryGetConstantValue(compilation, loader, right, argument, token, out var rightValue))
			{
				return SyntaxHelpers.CreateLiteral(ObjectExtensions.ExecuteBinaryOperation(operation.OperatorKind, leftValue, rightValue));
			}

			return binary
				.WithLeft((ExpressionSyntax)left!)
				.WithRight((ExpressionSyntax)right!);
		}

		return operation.Syntax;
	}

	public override SyntaxNode? VisitVariableDeclarationGroup(IVariableDeclarationGroupOperation operation, IDictionary<string, object?> argument)
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

	public override SyntaxNode? VisitVariableDeclaration(IVariableDeclarationOperation operation, IDictionary<string, object?> argument)
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

	public override SyntaxNode? VisitVariableDeclarator(IVariableDeclaratorOperation operation, IDictionary<string, object?> argument)
	{
		if (operation.Syntax is VariableDeclaratorSyntax variable)
		{
			var result = (EqualsValueClauseSyntax)Visit(operation.Initializer, argument);

			if (SyntaxHelpers.TryGetConstantValue(compilation, loader, result?.Value, argument, token, out var value))
			{
				argument.Add(operation.Symbol.Name, value);
			}

			return variable.WithInitializer(result);
		}

		return operation.Syntax;
	}

	public override SyntaxNode? VisitVariableInitializer(IVariableInitializerOperation operation, IDictionary<string, object?> argument)
	{
		if (operation.Syntax is EqualsValueClauseSyntax syntax)
		{
			return syntax.WithValue(Visit(operation.Value, argument) as ExpressionSyntax);
		}

		return operation.Syntax;
	}

	public override SyntaxNode? VisitConversion(IConversionOperation operation, IDictionary<string, object?> argument)
	{
		var operand = Visit(operation.Operand, argument);
		var conversion = operation.Type;

		if (SyntaxHelpers.TryGetConstantValue(compilation, loader, operand, argument, token, out var value))
		{
			if (operation.OperatorMethod is not null)
			{
				// If there's a conversion method, use it and produce a literal syntax node
				return SyntaxHelpers.CreateLiteral(compilation.ExecuteMethod(loader, operation.OperatorMethod, null, argument, value));
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

		return operation.Syntax;
	}

	public override SyntaxNode? VisitInvocation(IInvocationOperation operation, IDictionary<string, object?> argument)
	{
		if (operation.Syntax is InvocationExpressionSyntax invocation)
		{
			var targetMethod = operation.TargetMethod;
			var instance = Visit(operation.Instance, argument);

			var arguments = operation.Arguments
				.Select(arg => Visit(arg.Value, argument));

			var constantArguments = arguments
				.Where(w => SyntaxHelpers.TryGetConstantValue(compilation, loader, w, argument, token, out _))
				.Select(s => SyntaxHelpers.GetConstantValue(compilation, loader, s, argument, token))
				.ToArray();

			if (constantArguments.Length == operation.Arguments.Length)
			{
				if (instance is null)
				{
					return SyntaxHelpers.CreateLiteral(compilation.ExecuteMethod(loader, targetMethod, null, argument, constantArguments));
				}

				if (SyntaxHelpers.TryGetConstantValue(compilation, loader, instance, argument, token, out var instanceValue))
				{
					return SyntaxHelpers.CreateLiteral(compilation.ExecuteMethod(loader, targetMethod, instanceValue, argument, constantArguments));
				}
			}

			return invocation
				.WithArgumentList(invocation.ArgumentList
					.WithArguments(SyntaxFactory.SeparatedList(arguments.Select(s => SyntaxFactory.Argument((ExpressionSyntax)s)))));
		}

		return operation.Syntax;
	}

	public override SyntaxNode? VisitConditional(IConditionalOperation operation, IDictionary<string, object?> argument)
	{
		if (operation.Syntax is ConditionalExpressionSyntax conditional)
		{
			var condition = Visit(operation.Condition, argument);

			if (SyntaxHelpers.TryGetConstantValue(compilation, loader, condition, argument, token, out var value))
			{
				if (value is true)
				{
					return Visit(operation.WhenTrue, argument);
				}
				else if (value is false)
				{
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

			if (SyntaxHelpers.TryGetConstantValue(compilation, loader, visitedCondition, argument, token, out var condValue))
			{
				if (condValue is true)
				{
					// Return only the 'then' part
					return Visit(operation.WhenTrue, argument);
				}
				else if (condValue is false)
				{
					// Return only the 'else' part (if present); otherwise drop the whole if
					if (operation.WhenFalse is null)
					{
						return null;
					}

					return Visit(operation.WhenFalse, argument);
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

	public override SyntaxNode? VisitReturn(IReturnOperation operation, IDictionary<string, object?> argument)
	{
		if (operation.Syntax is ReturnStatementSyntax returnStatement)
		{
			return returnStatement.WithExpression((ExpressionSyntax?)Visit(operation.ReturnedValue, argument));
		}

		return operation.Syntax;
	}

	public override SyntaxNode? VisitTuple(ITupleOperation operation, IDictionary<string, object?> argument)
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

	public override SyntaxNode? VisitSimpleAssignment(ISimpleAssignmentOperation operation, IDictionary<string, object?> argument)
	{
		if (operation.Syntax is AssignmentExpressionSyntax assignment)
		{
			// Do not visit the left/target to avoid turning assignable expressions into constants.
			var visitedRight = Visit(operation.Value, argument);
			var rightExpr = visitedRight as ExpressionSyntax ?? assignment.Right;

			// If RHS is constant, update the environment for locals/params and replace RHS with a literal.
			if (SyntaxHelpers.TryGetConstantValue(compilation, loader, rightExpr, argument, token, out var value))
			{
				switch (operation.Target)
				{
					case ILocalReferenceOperation localRef:
						argument[localRef.Local.Name] = value;
						break;
					case IParameterReferenceOperation paramRef:
						argument[paramRef.Parameter.Name] = value;
						break;
				}

				if (SyntaxHelpers.TryGetLiteral(value, out var literal))
				{
					rightExpr = literal;
				}
			}

			return assignment.WithRight(rightExpr);
		}

		return operation.Syntax;
	}
}
