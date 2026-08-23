using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Visitors;

public class ExpressionVisitor(SemanticModel model, MetadataLoader loader, IEnumerable<ParameterExpression> parameters) : OperationVisitor<IDictionary<string, object?>, Expression>
{
	// Locals aren't known up front like lambda `parameters` - they're discovered as VisitVariableDeclarator
	// (or a first-reference fallback) runs. scopedLocals tracks which enclosing Block each one must be
	// declared in; localsByName lets any later reference to the same symbol resolve to the same instance.
	private readonly Stack<List<ParameterExpression>> scopedLocals = new();
	private readonly Dictionary<string, ParameterExpression> localsByName = new();
	private readonly Stack<(LabelTarget BreakLabel, LabelTarget ContinueLabel)> loopLabels = new();

	private void RegisterLocal(ParameterExpression local)
	{
		localsByName[local.Name!] = local;

		if (scopedLocals.Count > 0)
		{
			scopedLocals.Peek().Add(local);
		}
	}

	public override Expression DefaultVisit(IOperation operation, IDictionary<string, object?> argument)
	{
		if (operation.ConstantValue is { HasValue: true, Value: var value })
		{
			return Expression.Constant(value);
		}

		// Silently visiting-and-discarding children here used to return a void Expression.Empty()
		// placeholder for any operation kind with no explicit override. That placeholder then blew up
		// wherever it was later used (a comparison, a ternary branch, an assignment target) with a
		// confusing type-mismatch several frames away from the real gap. Fail loudly at the source
		// instead - the caller (ConstExprSourceGenerator) catches this and skips the fallback delegate
		// for method bodies this visitor doesn't fully model yet.
		throw new NotSupportedException($"ExpressionVisitor has no handler for operation kind {operation.Kind} ('{operation.Syntax}')");
	}

	public override Expression VisitBlock(IBlockOperation operation, IDictionary<string, object?> argument)
	{
		scopedLocals.Push([ ]);

		// Only flatten a child's Block when it declares no locals of its own - those variables are
		// scoped to that Block and would be lost (and become "undefined" at Compile()) if only its
		// Expressions were spliced in here.
		var items = operation.Operations
			.Select(item => Visit(item, argument))
			.SelectMany<Expression, Expression>(s => s is BlockExpression { Variables.Count: 0 } blockExpression ? blockExpression.Expressions : [ s ])
			.ToArray();

		var locals = scopedLocals.Pop();

		if (items.Length == 0)
		{
			return locals.Count > 0 ? Expression.Block(locals, Expression.Empty()) : Expression.Empty();
		}

		if (items.Length == 1 && locals.Count == 0)
		{
			return items[0];
		}

		// Note: `parameters` are the enclosing lambda's parameters, not this block's locals -
		// they must not be redeclared here, or they'd shadow the real parameters with
		// default-initialized locals of the same name.
		return Expression.Block(locals, items);
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
			BinaryOperatorKind.And => ExpressionType.And,
			BinaryOperatorKind.Or => ExpressionType.Or,
			BinaryOperatorKind.ExclusiveOr => ExpressionType.ExclusiveOr,
			BinaryOperatorKind.ConditionalAnd => ExpressionType.AndAlso,
			BinaryOperatorKind.ConditionalOr => ExpressionType.OrElse,
			BinaryOperatorKind.LeftShift => ExpressionType.LeftShift,
			BinaryOperatorKind.RightShift => ExpressionType.RightShift,
			_ => throw new NotImplementedException($"Binary operator {operation.OperatorKind} is not supported")
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
			.Select(arg => loader.GetType(arg.Value.Type))
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
		var parameterCount = operation.TargetMethod.Parameters.Length;

		MethodInfo? methodInfo;

		if (operation.TargetMethod.IsGenericMethod)
		{
			// A generic method's open-definition parameters (e.g. IEnumerable<TSource>) never
			// structurally match the concrete argument types, so match by name/arity instead and
			// let MakeGenericMethod apply the type arguments Roslyn already resolved.
			var genericArguments = operation.TargetMethod.TypeArguments.Select(loader.GetType).ToArray();

			var openMethod = containingType.GetMethods()
				.FirstOrDefault(f => f.Name == methodName
				                     && f.IsGenericMethodDefinition
				                     && f.GetGenericArguments().Length == genericArguments.Length
				                     && f.GetParameters().Length == parameterCount);

			if (openMethod == null)
			{
				throw new InvalidOperationException($"Generic method {methodName} not found in {containingType.FullName}");
			}

			methodInfo = openMethod.MakeGenericMethod(genericArguments);
		}
		else
		{
			// Find the method (simplified - may need enhancement for complex overloads)
			methodInfo = containingType.GetMethods().FirstOrDefault(f => f.Name == methodName && f.GetParameters().Select(p => p.ParameterType).SequenceEqual(argumentTypes));
		}

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

	public override Expression VisitConversion(IConversionOperation operation, IDictionary<string, object?> argument)
	{
		var operand = Visit(operation.Operand, argument);
		var targetType = loader.GetType(operation.Type);

		return operand.Type == targetType ? operand : Expression.Convert(operand, targetType);
	}

	public override Expression VisitLiteral(ILiteralOperation operation, IDictionary<string, object?> argument)
	{
		// An untyped `null` literal has no ITypeSymbol of its own - loader.GetType(null) can't
		// produce a Type for it. Let Expression.Constant infer object; Equal/Convert both special-case
		// a null constant against a reference type, so this doesn't need the real target type here.
		if (operation.Type is null)
		{
			return Expression.Constant(operation.ConstantValue.Value);
		}

		return Expression.Constant(operation.ConstantValue.Value, loader.GetType(operation.Type));
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
			_ => operand
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
			_ => target
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
		var localName = operation.Local.Name;

		// Check if we already have this parameter
		var existingParam = parameters.FirstOrDefault(p => p.Name == localName);

		if (existingParam != null)
		{
			return existingParam;
		}

		if (localsByName.TryGetValue(localName, out var knownLocal))
		{
			return knownLocal;
		}

		// A reference reached before its declaring VisitVariableDeclarator (e.g. a `foreach`/`out`
		// binding this visitor doesn't model yet) - register it into the innermost open Block scope
		// so Compile() still finds it declared somewhere.
		var localType = loader.GetType(operation.Local.Type);
		var newLocal = Expression.Parameter(localType, localName);
		RegisterLocal(newLocal);

		return newLocal;
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
		{
			throw new InvalidOperationException($"Constructor with {arguments.Length} parameters not found for type {type.FullName}");
		}

		return Expression.New(constructor, arguments);
	}

	public override Expression VisitInstanceReference(IInstanceReferenceOperation operation, IDictionary<string, object?> argument)
	{
		// In an expression tree context, 'this' is typically represented by a parameter
		var thisParameter = parameters.FirstOrDefault(p => p.Name == "this");

		if (thisParameter != null)
		{
			return thisParameter;
		}

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

		// A statement `if` without an `else` has no WhenFalse operand - that's not a ternary,
		// it's a void conditional, so it needs Expression.IfThen rather than Expression.Condition
		// (which requires both branches and would otherwise throw on the missing operand).
		if (operation.WhenFalse is null)
		{
			return Expression.IfThen(condition, whenTrue);
		}

		var whenFalse = Visit(operation.WhenFalse, argument);

		// Branches can disagree structurally even when Roslyn considers them compatible - e.g. a
		// bare `null` literal is visited as an untyped `object` constant (see VisitLiteral) against
		// a reference-typed other branch. Expression.Condition requires an exact type match, unlike
		// the C# ternary it's built from, so coerce both sides to the operation's real result type.
		if (whenTrue.Type != whenFalse.Type)
		{
			var resultType = operation.Type is not null ? loader.GetType(operation.Type) : whenTrue.Type;
			whenTrue = whenTrue.Type == resultType ? whenTrue : Expression.Convert(whenTrue, resultType);
			whenFalse = whenFalse.Type == resultType ? whenFalse : Expression.Convert(whenFalse, resultType);
		}

		return Expression.Condition(condition, whenTrue, whenFalse);
	}

	public override Expression VisitUtf8String(IUtf8StringOperation operation, IDictionary<string, object?> argument)
	{
		return Expression.Constant(Encoding.UTF8.GetBytes(operation.Value));
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
					Expression.Call(typeof(Monitor), "Enter", null, lockObj, monitorVar),
					body
				),
				Expression.IfThen(
					monitorVar,
					Expression.Call(typeof(Monitor), "Exit", null, lockObj)
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
		var lambdaVisitor = new ExpressionVisitor(model, loader, allParams);

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

	public override Expression VisitExpressionStatement(IExpressionStatementOperation operation, IDictionary<string, object?> argument)
	{
		// DefaultVisit would recurse into this statement's children but discard the result, silently
		// dropping the assignment/call it wraps. It must forward the visited expression instead.
		return Visit(operation.Operation, argument);
	}

	public override Expression VisitSimpleAssignment(ISimpleAssignmentOperation operation, IDictionary<string, object?> argument)
	{
		var target = Visit(operation.Target, argument);
		var value = Visit(operation.Value, argument);

		return Expression.Assign(target, value);
	}

	public override Expression VisitCompoundAssignment(ICompoundAssignmentOperation operation, IDictionary<string, object?> argument)
	{
		var target = Visit(operation.Target, argument);
		var value = Visit(operation.Value, argument);

		return operation.OperatorKind switch
		{
			BinaryOperatorKind.Add => Expression.AddAssign(target, value),
			BinaryOperatorKind.Subtract => Expression.SubtractAssign(target, value),
			BinaryOperatorKind.Multiply => Expression.MultiplyAssign(target, value),
			BinaryOperatorKind.Divide => Expression.DivideAssign(target, value),
			BinaryOperatorKind.Remainder => Expression.ModuloAssign(target, value),
			BinaryOperatorKind.And => Expression.AndAssign(target, value),
			BinaryOperatorKind.Or => Expression.OrAssign(target, value),
			BinaryOperatorKind.ExclusiveOr => Expression.ExclusiveOrAssign(target, value),
			BinaryOperatorKind.LeftShift => Expression.LeftShiftAssign(target, value),
			BinaryOperatorKind.RightShift => Expression.RightShiftAssign(target, value),
			_ => throw new NotImplementedException($"Compound assignment operator {operation.OperatorKind} is not supported")
		};
	}

	public override Expression VisitArrayElementReference(IArrayElementReferenceOperation operation, IDictionary<string, object?> argument)
	{
		var array = Visit(operation.ArrayReference, argument);
		var indices = operation.Indices.Select(i => Visit(i, argument)).ToArray();

		return Expression.ArrayAccess(array, indices);
	}

	public override Expression VisitArrayCreation(IArrayCreationOperation operation, IDictionary<string, object?> argument)
	{
		var elementType = loader.GetType(((IArrayTypeSymbol) operation.Type!).ElementType);

		if (operation.Initializer is not null)
		{
			var elements = operation.Initializer.ElementValues.Select(e => Visit(e, argument)).ToArray();
			return Expression.NewArrayInit(elementType, elements);
		}

		var bounds = operation.DimensionSizes.Select(d => Visit(d, argument)).ToArray();
		return Expression.NewArrayBounds(elementType, bounds);
	}

	public override Expression VisitVariableDeclarationGroup(IVariableDeclarationGroupOperation operation, IDictionary<string, object?> argument)
	{
		var assigns = operation.Declarations
			.SelectMany(decl => decl.Declarators)
			.Select(declarator => VisitVariableDeclarator(declarator, argument))
			.ToArray();

		return assigns.Length switch
		{
			0 => Expression.Empty(),
			1 => assigns[0],
			_ => Expression.Block(assigns)
		};
	}

	public override Expression VisitVariableDeclarator(IVariableDeclaratorOperation operation, IDictionary<string, object?> argument)
	{
		var local = Expression.Parameter(loader.GetType(operation.Symbol.Type), operation.Symbol.Name);
		RegisterLocal(local);

		if (operation.Initializer is null)
		{
			return Expression.Empty();
		}

		var value = Visit(operation.Initializer.Value, argument);
		return Expression.Assign(local, value);
	}

	public override Expression VisitBranch(IBranchOperation operation, IDictionary<string, object?> argument)
	{
		if (loopLabels.Count == 0)
		{
			throw new InvalidOperationException("break/continue used outside of a loop");
		}

		var (breakLabel, continueLabel) = loopLabels.Peek();

		return operation.BranchKind switch
		{
			BranchKind.Break => Expression.Break(breakLabel),
			BranchKind.Continue => Expression.Continue(continueLabel),
			_ => throw new NotImplementedException($"Branch kind {operation.BranchKind} is not supported")
		};
	}

	public override Expression VisitWhileLoop(IWhileLoopOperation operation, IDictionary<string, object?> argument)
	{
		var breakLabel = Expression.Label("break");
		var continueLabel = Expression.Label("continue");
		loopLabels.Push((breakLabel, continueLabel));

		Expression condition;
		Expression body;

		try
		{
			condition = Visit(operation.Condition, argument);

			if (operation.ConditionIsUntil)
			{
				condition = Expression.Not(condition);
			}

			body = Visit(operation.Body, argument);
		}
		finally
		{
			loopLabels.Pop();
		}

		// `do { } while` evaluates the condition after the body; a plain `while` checks it up front.
		return operation.ConditionIsTop
			? Expression.Loop(
				Expression.Block(
					Expression.IfThen(Expression.Not(condition), Expression.Break(breakLabel)),
					body,
					Expression.Label(continueLabel)),
				breakLabel)
			: Expression.Loop(
				Expression.Block(
					body,
					Expression.Label(continueLabel),
					Expression.IfThen(Expression.Not(condition), Expression.Break(breakLabel))),
				breakLabel);
	}

	public override Expression VisitForLoop(IForLoopOperation operation, IDictionary<string, object?> argument)
	{
		scopedLocals.Push([ ]);

		var before = operation.Before.Select(b => Visit(b, argument)).ToArray();
		var condition = operation.Condition is not null ? Visit(operation.Condition, argument) : null;

		var breakLabel = Expression.Label("break");
		var continueLabel = Expression.Label("continue");
		loopLabels.Push((breakLabel, continueLabel));

		Expression body;
		Expression[] atLoopBottom;

		try
		{
			body = Visit(operation.Body, argument);
			atLoopBottom = operation.AtLoopBottom.Select(a => Visit(a, argument)).ToArray();
		}
		finally
		{
			loopLabels.Pop();
		}

		var loopBodyParts = new List<Expression>();

		if (condition is not null)
		{
			loopBodyParts.Add(Expression.IfThen(Expression.Not(condition), Expression.Break(breakLabel)));
		}

		loopBodyParts.Add(body);
		loopBodyParts.Add(Expression.Label(continueLabel));
		loopBodyParts.AddRange(atLoopBottom);

		var loop = Expression.Loop(Expression.Block(loopBodyParts), breakLabel);
		var locals = scopedLocals.Pop();

		var statements = before.Append(loop).ToArray();

		return locals.Count > 0 ? Expression.Block(locals, statements) : Expression.Block(statements);
	}

	public override Expression VisitForEachLoop(IForEachLoopOperation operation, IDictionary<string, object?> argument)
	{
		scopedLocals.Push([ ]);

		var collection = Visit(operation.Collection, argument);
		var getEnumerator = collection.Type.GetMethod("GetEnumerator") ?? typeof(IEnumerable).GetMethod("GetEnumerator")!;
		var enumeratorVar = Expression.Variable(getEnumerator.ReturnType, "enumerator");
		var moveNext = enumeratorVar.Type.GetMethod("MoveNext") ?? typeof(IEnumerator).GetMethod("MoveNext")!;
		var currentProperty = enumeratorVar.Type.GetProperty("Current") ?? typeof(IEnumerator).GetProperty("Current")!;

		var loopVar = operation.LoopControlVariable is IVariableDeclaratorOperation declarator
			? Expression.Parameter(loader.GetType(declarator.Symbol.Type), declarator.Symbol.Name)
			: Expression.Parameter(currentProperty.PropertyType, "item");

		RegisterLocal(loopVar);

		var breakLabel = Expression.Label("break");
		var continueLabel = Expression.Label("continue");
		loopLabels.Push((breakLabel, continueLabel));

		Expression body;

		try
		{
			body = Visit(operation.Body, argument);
		}
		finally
		{
			loopLabels.Pop();
		}

		var loop = Expression.Block(
			[ enumeratorVar ],
			Expression.Assign(enumeratorVar, Expression.Call(collection, getEnumerator)),
			Expression.TryFinally(
				Expression.Loop(
					Expression.IfThenElse(
						Expression.Call(enumeratorVar, moveNext),
						Expression.Block(
							// `Current` can be looser than the loop variable - e.g. arrays only expose
							// the non-generic IEnumerator.Current (object), even when iterated as int[].
							Expression.Assign(loopVar, Expression.Convert(Expression.Property(enumeratorVar, currentProperty), loopVar.Type)),
							body,
							Expression.Label(continueLabel)),
						Expression.Break(breakLabel)),
					breakLabel),
				typeof(IDisposable).IsAssignableFrom(enumeratorVar.Type)
					? Expression.Call(Expression.Convert(enumeratorVar, typeof(IDisposable)), typeof(IDisposable).GetMethod("Dispose")!)
					: Expression.Empty()));

		var locals = scopedLocals.Pop();

		return locals.Count > 0 ? Expression.Block(locals, loop) : loop;
	}
}