using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Visitors;

public class ExpressionVisitor(Compilation compilation, MetadataLoader loader, IEnumerable<ParameterExpression> parameters) : OperationVisitor<IDictionary<string, object?>, Expression>
{
	public override Expression DefaultVisit(IOperation operation, IDictionary<string, object?> argument)
	{
		if (operation.ConstantValue is { HasValue: true, Value: var value })
		{
			return Expression.Constant(value);
		}

		foreach (var currentOperation in operation.ChildOperations)
		{
			Visit(currentOperation, argument);
		}

		return Expression.Empty();
	}

	public override Expression VisitBlock(IBlockOperation operation, IDictionary<string, object?> argument)
	{
		var items = operation.Operations
			.Select(item => Visit(item, argument))
			.SelectMany<Expression, Expression>(s => s is BlockExpression blockExpression ? blockExpression.Expressions : [ s ])
			.ToArray();

		if (items.Length == 1)
		{
			return items[0];
		}

		return Expression.Block(parameters, items);
	}

	public override Expression VisitReturn(IReturnOperation operation, IDictionary<string, object?> argument)
	{
		return Visit(operation.ReturnedValue, argument);

		// var returnType = compilation.GetTypeByType(operation.ReturnedValue.Type);
		// var returnLabel = Expression.Label(returnType);
		// var returnValue = Visit(operation.ReturnedValue, argument);
		//
		// // Build a block statement that performs the return
		// return Expression.Block(
		// 	Expression.Return(returnLabel, returnValue, returnType),
		// 	Expression.Label(returnLabel, Expression.Default(returnType))
		// );
	}

	public override Expression VisitBinaryOperator(IBinaryOperation operation, IDictionary<string, object?> argument)
	{
		var kind = operation.OperatorKind switch
		{
			BinaryOperatorKind.Add => ExpressionType.Add,
			BinaryOperatorKind.Subtract => ExpressionType.Subtract,
			BinaryOperatorKind.Multiply => ExpressionType.Multiply,
			BinaryOperatorKind.Divide => ExpressionType.Divide,
			BinaryOperatorKind.Remainder => ExpressionType.Modulo,
			BinaryOperatorKind.Equals => ExpressionType.Equal,
			BinaryOperatorKind.NotEquals => ExpressionType.NotEqual,
			BinaryOperatorKind.LessThan => ExpressionType.LessThan,
			BinaryOperatorKind.LessThanOrEqual => ExpressionType.LessThanOrEqual,
			BinaryOperatorKind.GreaterThan => ExpressionType.GreaterThan,
			BinaryOperatorKind.GreaterThanOrEqual => ExpressionType.GreaterThanOrEqual,
			_ => throw new NotImplementedException(),
		};

		var left = Visit(operation.LeftOperand, argument);
		var right = Visit(operation.RightOperand, argument);

		return Expression.MakeBinary(kind, left, right);
	}

	public override Expression VisitParameterReference(IParameterReferenceOperation operation, IDictionary<string, object?> argument)
	{
		return parameters.First(x => x.Name == operation.Parameter.Name);
	}

	public override Expression? VisitInvocation(IInvocationOperation operation, IDictionary<string, object?> argument)
	{
		// Get method arguments as expressions
		var arguments = operation.Arguments.Select(arg => Visit(arg.Value, argument)).ToArray();
		var argumentTypes = operation.Arguments
			.Select(arg => loader.GetType(arg.Type))
			.ToArray();

		// If this is a delegate invocation
		if (operation.TargetMethod == null)
		{
			var target = Visit(operation.Instance, argument);
			return Expression.Invoke(target, arguments);
		}

		// For method calls
		var containingType = loader.GetType(operation.TargetMethod.ContainingType);
		var methodName = operation.TargetMethod.Name;

		// Find the method (simplified - may need enhancement for complex overloads)
		var methodInfo = containingType.GetMethods().First(f => f.Name == methodName && f.GetParameters().Select(p => p.ParameterType).SequenceEqual(argumentTypes));

		if (methodInfo == null)
		{
			throw new InvalidOperationException($"Method {methodName} not found in {containingType.FullName}");
		}

		// For static methods
		if (operation.TargetMethod.IsStatic)
		{
			return Expression.Call(methodInfo, arguments);
		}

		// For instance methods
		var instance = Visit(operation.Instance, argument);
		return Expression.Call(instance, methodInfo, arguments);
	}

	public override Expression VisitLiteral(ILiteralOperation operation, IDictionary<string, object?> argument)
	{
		return Expression.Constant(operation.ConstantValue.Value,
			loader.GetType(operation.Type));
	}

	public override Expression VisitUnaryOperator(IUnaryOperation operation, IDictionary<string, object?> argument)
	{
		var operand = Visit(operation.Operand, argument);

		return operation.OperatorKind switch
		{
			UnaryOperatorKind.Plus => operand,
			UnaryOperatorKind.Minus => Expression.Negate(operand),
			UnaryOperatorKind.BitwiseNegation => Expression.OnesComplement(operand),
			UnaryOperatorKind.Not => Expression.Not(operand),
			_ => operand,
		};
	}

	public override Expression VisitIncrementOrDecrement(IIncrementOrDecrementOperation operation, IDictionary<string, object?> argument)
	{
		var target = Visit(operation.Target, argument);
		var one = Expression.Constant(1);

		return operation.Kind switch
		{
			OperationKind.Increment => Expression.AddAssign(target, one),
			OperationKind.Decrement => Expression.SubtractAssign(target, one),
			_ => target,
		};
	}

	public override Expression VisitParenthesized(IParenthesizedOperation operation, IDictionary<string, object?> argument)
	{
		return Visit(operation.Operand, argument);
	}

	public override Expression VisitFieldReference(IFieldReferenceOperation operation, IDictionary<string, object?> argument)
	{
		var instance = operation.Instance != null ? Visit(operation.Instance, argument) : null;

		var containingType = loader.GetType(operation.Field.ContainingType);
		var fieldInfo = containingType.GetField(operation.Field.Name);

		return operation.Field.IsStatic
			? Expression.Field(null, fieldInfo)
			: Expression.Field(instance, fieldInfo);
	}

	public override Expression VisitPropertyReference(IPropertyReferenceOperation operation, IDictionary<string, object?> argument)
	{
		var instance = operation.Instance != null ? Visit(operation.Instance, argument) : null;

		var containingType = loader.GetType(operation.Property.ContainingType);
		var propertyInfo = containingType.GetProperty(operation.Property.Name);

		return operation.Property.IsStatic
			? Expression.Property(null, propertyInfo)
			: Expression.Property(instance, propertyInfo);
	}

	public override Expression VisitLocalReference(ILocalReferenceOperation operation, IDictionary<string, object?> argument)
	{
		// For local variables, we need to find or create a parameter expression
		string localName = operation.Local.Name;

		// Check if we already have this parameter
		var existingParam = parameters.FirstOrDefault(p => p.Name == localName);

		if (existingParam != null)
		{
			return existingParam;
		}

		// Otherwise, we might need to create a new parameter or handle differently
		var localType = loader.GetType(operation.Local.Type);
		return Expression.Parameter(localType, localName);
	}

	public override Expression VisitDefaultValue(IDefaultValueOperation operation, IDictionary<string, object?> argument)
	{
		var type = loader.GetType(operation.Type);
		return Expression.Default(type);
	}

	public override Expression VisitObjectCreation(IObjectCreationOperation operation, IDictionary<string, object?> argument)
	{
		var type = loader.GetType(operation.Type);

		var arguments = operation.Arguments
			.Select(arg => Visit(arg.Value, argument))
			.ToArray();

		var constructor = type.GetConstructors()
			.FirstOrDefault(c => c.GetParameters().Length == arguments.Length);

		if (constructor == null)
			throw new InvalidOperationException($"Constructor with {arguments.Length} parameters not found for type {type.FullName}");

		return Expression.New(constructor, arguments);
	}

	public override Expression VisitInstanceReference(IInstanceReferenceOperation operation, IDictionary<string, object?> argument)
	{
		// In an expression tree context, 'this' is typically represented by a parameter
		var thisParameter = parameters.FirstOrDefault(p => p.Name == "this");

		if (thisParameter != null)
			return thisParameter;

		// If no 'this' parameter exists, throw an exception or handle appropriately
		throw new InvalidOperationException("No 'this' parameter available in the current context.");
	}

	public override Expression VisitNameOf(INameOfOperation operation, IDictionary<string, object?> argument)
	{
		return Expression.Constant(operation.ConstantValue.Value, typeof(string));
	}

	public override Expression VisitConditional(IConditionalOperation operation, IDictionary<string, object?> argument)
	{
		var condition = Visit(operation.Condition, argument);
		var whenTrue = Visit(operation.WhenTrue, argument);
		var whenFalse = Visit(operation.WhenFalse, argument);

		return Expression.Condition(condition, whenTrue, whenFalse);
	}

	public override Expression VisitUtf8String(IUtf8StringOperation operation, IDictionary<string, object?> argument)
	{
		return Expression.Constant(System.Text.Encoding.UTF8.GetBytes(operation.Value));
	}

	public override Expression VisitAwait(IAwaitOperation operation, IDictionary<string, object?> argument)
	{
		var operand = Visit(operation.Operation, argument);
		return Expression.Call(
			operand,
			operand.Type.GetMethod("GetAwaiter"),
			[ ]);
	}

	public override Expression VisitUsing(IUsingOperation operation, IDictionary<string, object?> argument)
	{
		var resource = Visit(operation.Resources, argument);
		var body = Visit(operation.Body, argument);

		// Create a using block
		return Expression.TryFinally(
			body,
			Expression.Call(resource, typeof(IDisposable).GetMethod("Dispose"))
		);
	}

	public override Expression VisitLock(ILockOperation operation, IDictionary<string, object?> argument)
	{
		var lockObj = Visit(operation.LockedValue, argument);
		var body = Visit(operation.Body, argument);

		// Create a lock statement using Monitor.Enter/Exit
		var monitorVar = Expression.Variable(typeof(bool), "lockTaken");

		return Expression.Block(
			[ monitorVar ],
			Expression.Assign(monitorVar, Expression.Constant(false)),
			Expression.TryFinally(
				Expression.Block(
					Expression.Call(typeof(System.Threading.Monitor), "Enter", null, lockObj, monitorVar),
					body
				),
				Expression.IfThen(
					monitorVar,
					Expression.Call(typeof(System.Threading.Monitor), "Exit", null, lockObj)
				)
			)
		);
	}

	public override Expression VisitDelegateCreation(IDelegateCreationOperation operation, IDictionary<string, object?> argument)
	{
		return Visit(operation.Target, argument);
	}

	public override Expression VisitAnonymousFunction(IAnonymousFunctionOperation operation, IDictionary<string, object?> argument)
	{
		// Create parameters for the lambda
		var lambdaParams = operation.Symbol.Parameters
			.Select(p => Expression.Parameter(loader.GetType(p.Type), p.Name))
			.ToArray();

		// Create a new visitor with the lambda parameters included
		var allParams = parameters.Concat(lambdaParams);
		var lambdaVisitor = new ExpressionVisitor(compilation, loader, allParams);

		// Visit the body with the new visitor
		var body = lambdaVisitor.VisitBlock(operation.Body, argument);

		// Create the lambda expression
		return Expression.Lambda(body, lambdaParams);
	}

	public override Expression VisitAnonymousObjectCreation(IAnonymousObjectCreationOperation operation, IDictionary<string, object?> argument)
	{
		var type = loader.GetType(operation.Type);

		var initializers = operation.Initializers
			.Select(init => Visit(init, argument))
			.ToArray();

		// Use MemberInit to create and initialize the anonymous object
		var newExpression = Expression.New(type);

		var bindings = operation.Initializers
			.Select((init, i) =>
			{
				var property = type.GetProperties()[i];
				return Expression.Bind(property, initializers[i]);
			})
			.ToArray();

		return Expression.MemberInit(newExpression, bindings);
	}

	public override Expression VisitTry(ITryOperation operation, IDictionary<string, object?> argument)
	{
		var tryBlock = Visit(operation.Body, argument);
		var finallyBlock = operation.Finally != null
			? Visit(operation.Finally, argument)
			: null;

		if (operation.Catches.IsEmpty)
		{
			return Expression.TryFinally(tryBlock, finallyBlock);
		}

		// Handle catch blocks
		var catchBlocks = operation.Catches
			.Select(c =>
			{
				var exType = loader.GetType(c.ExceptionType);
				var exVar = c.ExceptionDeclarationOrExpression != null
					? Expression.Parameter(exType, c.ExceptionDeclarationOrExpression.ToString())
					: Expression.Parameter(exType);

				return Expression.Catch(exVar, Visit(c.Handler, argument));
			})
			.ToArray();

		if (finallyBlock != null)
		{
			return Expression.TryCatchFinally(tryBlock, finallyBlock, catchBlocks);
		}

		return Expression.TryCatch(tryBlock, catchBlocks);
	}

	public override Expression VisitThrow(IThrowOperation operation, IDictionary<string, object?> argument)
	{
		var exception = Visit(operation.Exception, argument);
		return Expression.Throw(exception);
	}

	public override Expression VisitConditionalAccess(IConditionalAccessOperation operation, IDictionary<string, object?> argument)
	{
		var receiver = Visit(operation.Operation, argument);
		var whenNotNull = Visit(operation.WhenNotNull, argument);

		var targetType = loader.GetType(operation.Type);
		var resultVar = Expression.Variable(targetType, "conditionalResult");

		return Expression.Block(
			[ resultVar ],
			Expression.IfThenElse(
				Expression.NotEqual(receiver, Expression.Constant(null)),
				Expression.Assign(resultVar, whenNotNull),
				Expression.Assign(resultVar, Expression.Default(targetType))
			),
			resultVar
		);
	}
}