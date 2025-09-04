using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
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
		if (argument.TryGetValue(operation.Local.Name, out var value) && value.HasValue && SyntaxHelpers.TryGetLiteral(value.Value, out var expression))
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

			if (SyntaxHelpers.TryGetConstantValue(compilation, loader, left, new VariableItemDictionary(argument), token, out var leftValue)
				&& SyntaxHelpers.TryGetConstantValue(compilation, loader, right, new VariableItemDictionary(argument), token, out var rightValue))
			{
				return SyntaxHelpers.CreateLiteral(ObjectExtensions.ExecuteBinaryOperation(operation.OperatorKind, leftValue, rightValue));
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

		return operation.Syntax;
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
				if (instance is null)
				{
					return SyntaxHelpers.CreateLiteral(compilation.ExecuteMethod(loader, targetMethod, null, new VariableItemDictionary(argument), constantArguments));
				}

				if (SyntaxHelpers.TryGetConstantValue(compilation, loader, instance, new VariableItemDictionary(argument), token, out var instanceValue))
				{
					return SyntaxHelpers.CreateLiteral(compilation.ExecuteMethod(loader, targetMethod, instanceValue, new VariableItemDictionary(argument), constantArguments));
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