using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vectorize.Helpers;

namespace Vectorize.Visitors;

public partial class ConstExprOperationVisitor(Compilation compilation, CancellationToken token) : OperationVisitor<Dictionary<string, object?>, object?>
{
	public const string ReturnVariableName = "$return$";

	public override object? DefaultVisit(IOperation operation, Dictionary<string, object?> argument)
	{
		if (operation.ConstantValue is { HasValue: true, Value: var value })
		{
			return value;
		}

		foreach (var currentOperation in operation.ChildOperations)
		{
			Visit(currentOperation, argument);
		}

		return null;
	}

	public override object? Visit(IOperation? operation, Dictionary<string, object?> argument)
	{
		token.ThrowIfCancellationRequested();

		return base.Visit(operation, argument);
	}

	public override object? VisitIsNull(IIsNullOperation operation, Dictionary<string, object?> argument)
	{
		return Visit(operation.Operand, argument) is null;
	}

	public override object? VisitArrayCreation(IArrayCreationOperation operation, Dictionary<string, object?> argument)
	{
		return operation.Initializer?.ElementValues
			.Select(s => Visit(s, argument))
			.ToArray();
	}

	public override object? VisitArrayElementReference(IArrayElementReferenceOperation operation, Dictionary<string, object?> argument)
	{
		var array = Visit(operation.ArrayReference, argument);

		if (array is null)
		{
			return null;
		}

		var indexers = operation.Indices
			.Select(s => Visit(s, argument))
			.ToArray();

		foreach (var pi in array.GetType().GetProperties())
		{
			if (pi.GetIndexParameters().Length == indexers.Length)
			{
				return pi.GetValue(array, indexers);
			}
		}

		return null;
	}

	public override object? VisitCoalesce(ICoalesceOperation operation, Dictionary<string, object?> argument)
	{
		return Visit(operation.Value, argument) ?? Visit(operation.WhenNull, argument);
	}

	public override object? VisitCompoundAssignment(ICompoundAssignmentOperation operation, Dictionary<string, object?> argument)
	{
		var target = Visit(operation.Target, argument);
		var value = Visit(operation.Value, argument);

		if (target is null)
		{
			return null;
		}

		return argument[GetVariableName(operation.Target)] = ExecuteBinaryOperation(operation.OperatorKind, target, value);
	}

	public override object? VisitDeconstructionAssignment(IDeconstructionAssignmentOperation operation, Dictionary<string, object?> argument)
	{
		var target = Visit(operation.Target, argument);
		var value = Visit(operation.Value, argument);

		if (target is not object?[] array)
		{
			return null;
		}

		for (var i = 0; i < array.Length; i++)
		{
			array[i] = value is object?[] values
				? values[i]
				: null;
		}

		return array;
	}

	public override object? VisitSimpleAssignment(ISimpleAssignmentOperation operation, Dictionary<string, object?> argument)
	{
		return argument[GetVariableName(operation.Target)] = Visit(operation.Value, argument);
	}

	public override object? VisitBinaryOperator(IBinaryOperation operation, Dictionary<string, object?> argument)
	{
		var left = Visit(operation.LeftOperand, argument);
		var right = Visit(operation.RightOperand, argument);
		var operatorKind = operation.OperatorKind;
		var method = operation.OperatorMethod;

		if (method is not null)
		{
			return SyntaxHelpers.ExecuteMethod(compilation, method, null, left, right);
		}

		return ExecuteBinaryOperation(operatorKind, left, right);
	}

	public override object? VisitBlock(IBlockOperation operation, Dictionary<string, object?> argument)
	{
		var names = argument.Keys;

		foreach (var currentOperation in operation.Operations)
		{
			Visit(currentOperation, argument);
		}

		foreach (var name in argument.Keys.Except(names))
		{
			argument.Remove(name);
		}

		return null;
	}

	public override object? VisitCoalesceAssignment(ICoalesceAssignmentOperation operation, Dictionary<string, object?> argument)
	{
		var target = Visit(operation.Target, argument);

		if (target is not null)
		{
			return target;
		}

		return argument[GetVariableName(operation.Target)] = Visit(operation.Value, argument);
	}

	public override object? VisitCollectionExpression(ICollectionExpressionOperation operation, Dictionary<string, object?> argument)
	{
		return operation.Elements
			.Select(s => Visit(s, argument));
	}

	public override object? VisitConditional(IConditionalOperation operation, Dictionary<string, object?> argument)
	{
		return Visit(operation.Condition, argument) switch
		{
			true => Visit(operation.WhenTrue, argument),
			false => Visit(operation.WhenFalse, argument),
			_ => throw new InvalidOperationException("Invalid conditional operation."),
		};
	}

	public override object? VisitConditionalAccess(IConditionalAccessOperation operation, Dictionary<string, object?> argument)
	{
		return Visit(operation.Operation, argument) is not null
			? Visit(operation.WhenNotNull, argument)
			: null;
	}

	public override object? VisitConversion(IConversionOperation operation, Dictionary<string, object?> argument)
	{
		var operand = Visit(operation.Operand, argument);
		var conversion = operation.Type;

		return conversion?.SpecialType switch
		{
			SpecialType.System_Boolean => Convert.ToBoolean(operand),
			SpecialType.System_Byte => Convert.ToByte(operand),
			SpecialType.System_Char => Convert.ToChar(operand),
			SpecialType.System_DateTime => Convert.ToDateTime(operand),
			SpecialType.System_Decimal => Convert.ToDecimal(operand),
			SpecialType.System_Double => Convert.ToDouble(operand),
			SpecialType.System_Int16 => Convert.ToInt16(operand),
			SpecialType.System_Int32 => Convert.ToInt32(operand),
			SpecialType.System_Int64 => Convert.ToInt64(operand),
			SpecialType.System_SByte => Convert.ToSByte(operand),
			SpecialType.System_Single => Convert.ToSingle(operand),
			SpecialType.System_String => Convert.ToString(operand),
			SpecialType.System_UInt16 => Convert.ToUInt16(operand),
			SpecialType.System_UInt32 => Convert.ToUInt32(operand),
			SpecialType.System_UInt64 => Convert.ToUInt64(operand),
			SpecialType.System_Object => operand,
			SpecialType.System_Collections_IEnumerable => (IEnumerable) operand,
			_ => operand,
		};
	}

	public override object? VisitInvocation(IInvocationOperation operation, Dictionary<string, object?> argument)
	{
		var targetMethod = operation.TargetMethod;
		var instance = Visit(operation.Instance, argument);

		var arguments = operation.Arguments
			.Select(s => Visit(s.Value, argument))
			.ToArray();

		if (SyntaxHelpers.IsInConstExprBody(targetMethod)
		    && SyntaxHelpers.TryGetOperation<IMethodBodyOperation>(compilation, targetMethod, out var methodOperation))
		{
			var syntax = targetMethod.DeclaringSyntaxReferences
				.Select(s => s.GetSyntax(token))
				.OfType<MethodDeclarationSyntax>()
				.FirstOrDefault();

			var variables = new Dictionary<string, object?>();

			for (var i = 0; i < syntax.ParameterList.Parameters.Count; i++)
			{
				var parameterName = syntax.ParameterList.Parameters[i].Identifier.Text;

				variables.Add(parameterName, arguments[i]);
			}

			var visitor = new ConstExprOperationVisitor(compilation, token);
			visitor.VisitBlock(methodOperation.BlockBody, variables);

			return variables[ReturnVariableName];
		}

		return SyntaxHelpers.ExecuteMethod(compilation, targetMethod, instance, arguments);
	}

	public override object? VisitSwitch(ISwitchOperation operation, Dictionary<string, object?> argument)
	{
		var value = Visit(operation.Value, argument);

		foreach (var caseClause in operation.Cases)
		{
			if (caseClause.Clauses
			    .Where(w => w.CaseKind != CaseKind.Default)
			    .Select(s => Visit(s, argument))
			    .Contains(value))
			{
				VisitList(caseClause.Body, argument);

				return null;
			}
		}

		foreach (var caseClause in operation.Cases)
		{
			if (caseClause.Clauses
			    .Where(w => w.CaseKind == CaseKind.Default)
			    .Select(s => Visit(s, argument))
			    .Contains(value))
			{
				VisitList(caseClause.Body, argument);
			}
		}

		return null;
	}

	public override object? VisitVariableDeclaration(IVariableDeclarationOperation operation, Dictionary<string, object?> argument)
	{
		foreach (var variable in operation.Declarators)
		{
			VisitVariableDeclarator(variable, argument);
		}

		return null;
	}

	public override object? VisitVariableDeclarationGroup(IVariableDeclarationGroupOperation operation, Dictionary<string, object?> argument)
	{
		foreach (var variable in operation.Declarations)
		{
			VisitVariableDeclaration(variable, argument);
		}

		return null;
	}

	public override object? VisitVariableDeclarator(IVariableDeclaratorOperation operation, Dictionary<string, object?> argument)
	{
		argument[operation.Symbol.Name] = Visit(operation.Initializer?.Value, argument);

		return null;
	}

	public override object? VisitReturn(IReturnOperation operation, Dictionary<string, object?> argument)
	{
		switch (operation.Kind)
		{
			case OperationKind.Return:
				return argument[ReturnVariableName] = Visit(operation.ReturnedValue, argument);
			case OperationKind.YieldReturn:
				if (!argument.TryGetValue(ReturnVariableName, out var list) || list is not IList data)
				{
					data = new List<object?>();
					argument[ReturnVariableName] = data;
				}

				data.Add(Visit(operation.ReturnedValue, argument));
				return null;
			default:
				return null;
		}
	}

	public override object? VisitWhileLoop(IWhileLoopOperation operation, Dictionary<string, object?> argument)
	{
		while (Visit(operation.Condition, argument) is true)
		{
			Visit(operation.Body, argument);
		}

		return null;
	}

	public override object? VisitForLoop(IForLoopOperation operation, Dictionary<string, object?> argument)
	{
		var names = argument.Keys;

		for (VisitList(operation.Before, argument); Visit(operation.Condition, argument) is true; VisitList(operation.AtLoopBottom, argument))
		{
			Visit(operation.Body, argument);
		}

		foreach (var name in argument.Keys.Except(names))
		{
			argument.Remove(name);
		}

		return null;
	}

	public override object? VisitForEachLoop(IForEachLoopOperation operation, Dictionary<string, object?> argument)
	{
		var itemName = GetVariableName(operation.LoopControlVariable);
		// var names = argument.Keys.ToArray();
		var collection = Visit(operation.Collection, argument);

		foreach (var item in collection as IEnumerable)
		{
			argument[itemName] = item;
			Visit(operation.Body, argument);
		}

		//foreach (var name in argument.Keys.Except(names))
		//{
		//	argument.Remove(name);
		//}

		return null;
	}

	public override object? VisitInterpolation(IInterpolationOperation operation, Dictionary<string, object?> argument)
	{
		var value = Visit(operation.Expression, argument);

		if (value is IFormattable formattable)
		{
			return formattable.ToString(Visit(operation.FormatString, argument) as string, CultureInfo.InvariantCulture);
		}

		return value.ToString();
	}

	public override object? VisitInterpolatedStringText(IInterpolatedStringTextOperation operation, Dictionary<string, object?> argument)
	{
		return operation.Text;
	}

	public override object? VisitInterpolatedString(IInterpolatedStringOperation operation, Dictionary<string, object?> argument)
	{
		return String.Concat(operation.Parts
			.Select(s => Visit(s, argument)));
	}

	public override object? VisitSizeOf(ISizeOfOperation operation, Dictionary<string, object?> argument)
	{
		var type = operation.Type;

		return type?.SpecialType switch
		{
			SpecialType.System_Boolean => sizeof(bool),
			SpecialType.System_Byte => sizeof(byte),
			SpecialType.System_Char => sizeof(char),
			SpecialType.System_Decimal => sizeof(decimal),
			SpecialType.System_Double => sizeof(double),
			SpecialType.System_Int16 => sizeof(short),
			SpecialType.System_Int32 => sizeof(int),
			SpecialType.System_Int64 => sizeof(long),
			SpecialType.System_SByte => sizeof(sbyte),
			SpecialType.System_Single => sizeof(float),
			SpecialType.System_UInt16 => sizeof(ushort),
			SpecialType.System_UInt32 => sizeof(uint),
			SpecialType.System_UInt64 => sizeof(ulong),
			_ => 0,
		};
	}

	public override object? VisitTypeOf(ITypeOfOperation operation, Dictionary<string, object?> argument)
	{
		return Type.GetType(operation.Type.ToDisplayString());
	}

	public override object? VisitArrayInitializer(IArrayInitializerOperation operation, Dictionary<string, object?> argument)
	{
		return operation.ElementValues
			.Select(s => Visit(s, argument))
			.ToArray();
	}

	public override object? VisitUnaryOperator(IUnaryOperation operation, Dictionary<string, object?> argument)
	{
		var operand = Visit(operation.Operand, argument);

		return operation.OperatorKind switch
		{
			UnaryOperatorKind.Plus => operand,
			UnaryOperatorKind.Minus => Subtract(0, operand),
			UnaryOperatorKind.BitwiseNegation => BitwiseNot(operand),
			UnaryOperatorKind.Not => LogicalNot(operand),
			_ => operand,
		};
	}

	public override object? VisitIncrementOrDecrement(IIncrementOrDecrementOperation operation, Dictionary<string, object?> argument)
	{
		var name = GetVariableName(operation.Target);
		var target = Visit(operation.Target, argument);
		var type = operation.Kind;

		return argument[name] = type switch
		{
			OperationKind.Increment => Add(target, 1),
			OperationKind.Decrement => Add(target, -1),
			_ => target,
		};
	}

	public override object? VisitParenthesized(IParenthesizedOperation operation, Dictionary<string, object?> argument)
	{
		return Visit(operation.Operand, argument);
	}

	public override object? VisitObjectCreation(IObjectCreationOperation operation, Dictionary<string, object?> argument)
	{
		var arguments = operation.Arguments
			.Select(s => Visit(s.Value, argument))
			.ToArray();

		return Activator.CreateInstance(Type.GetType(operation.Type.ToDisplayString()), arguments);
	}

	public override object? VisitAnonymousObjectCreation(IAnonymousObjectCreationOperation operation, Dictionary<string, object?> argument)
	{
		var arguments = operation.Initializers
			.Select(s => Visit(s, argument))
			.ToArray();

		return Activator.CreateInstance(Type.GetType(operation.Type.ToDisplayString()), arguments);
	}

	public override object? VisitInstanceReference(IInstanceReferenceOperation operation, Dictionary<string, object?> argument)
	{

		return operation.ReferenceKind switch
		{
			InstanceReferenceKind.ContainingTypeInstance => argument["this"],
			InstanceReferenceKind.ImplicitReceiver => argument["this"],
			_ => null,
		};
	}

	public override object? VisitUtf8String(IUtf8StringOperation operation, Dictionary<string, object?> argument)
	{
		return Encoding.UTF8.GetBytes(operation.Value);
	}

	public override object? VisitDefaultValue(IDefaultValueOperation operation, Dictionary<string, object?> argument)
	{
		return operation.Type?.SpecialType switch
		{
			SpecialType.System_Boolean => false,
			SpecialType.System_Byte => 0,
			SpecialType.System_Char => '\0',
			SpecialType.System_DateTime => default(DateTime),
			SpecialType.System_Decimal => 0,
			SpecialType.System_Double => 0,
			SpecialType.System_Int16 => 0,
			SpecialType.System_Int32 => 0,
			SpecialType.System_Int64 => 0,
			SpecialType.System_SByte => 0,
			SpecialType.System_Single => 0,
			SpecialType.System_String => null,
			SpecialType.System_UInt16 => 0,
			SpecialType.System_UInt32 => 0,
			SpecialType.System_UInt64 => 0,
			_ => null,
		};
	}

	public override object? VisitLocalReference(ILocalReferenceOperation operation, Dictionary<string, object?> argument)
	{
		return argument[operation.Local.Name];
	}

	public override object? VisitParameterReference(IParameterReferenceOperation operation, Dictionary<string, object?> argument)
	{
		return argument[operation.Parameter.Name];
	}

	public override object? VisitFieldReference(IFieldReferenceOperation operation, Dictionary<string, object?> argument)
	{
		return argument[operation.Field.Name];
	}

	public override object? VisitPropertyReference(IPropertyReferenceOperation operation, Dictionary<string, object?> argument)
	{
		var instance = Visit(operation.Instance, argument);
		var type = SyntaxHelpers.GetTypeByType(compilation, operation.Property.ContainingType);

		var propertyInfo = type
			.GetProperties()
			.FirstOrDefault(f => f.Name == operation.Property.Name && f.GetMethod.IsStatic == operation.Property.IsStatic);

		if (propertyInfo == null)
		{
			throw new InvalidOperationException("Property info could not be retrieved.");
		}
		
		if (operation.Property.IsStatic)
		{
			return propertyInfo.GetValue(null);
		}

		if (instance is IConvertible)
		{
			instance = Convert.ChangeType(instance, propertyInfo.PropertyType);
		}

		return propertyInfo.GetValue(instance);
	}

	public override object? VisitAwait(IAwaitOperation operation, Dictionary<string, object?> argument)
	{
		var value = Visit(operation.Operation, argument);

		if (value is null)
		{
			return null;
		}

		var task = value.GetType();

		// Use reflection to call GetAwaiter() and GetResult()
		var getAwaiterMethod = task.GetMethod(nameof(Task.GetAwaiter));
		var awaiter = getAwaiterMethod?.Invoke(value, null);

		var getResultMethod = awaiter?.GetType().GetMethod(nameof(TaskAwaiter.GetResult));
		var result = getResultMethod?.Invoke(awaiter, null);

		return result;
	}

	public override object? VisitUsing(IUsingOperation operation, Dictionary<string, object?> argument)
	{
		var names = argument.Keys.ToArray();

		Visit(operation.Resources, argument);
		Visit(operation.Body, argument);

		foreach (var name in argument.Keys.Except(names))
		{
			var item = argument[name];

			if (item is IDisposable disposable)
			{
				disposable.Dispose();
			}
			else
			{
				item.GetType().GetMethod("Dispose")?.Invoke(item, null);
			}
		}

		return null;
	}

	public override object? VisitNameOf(INameOfOperation operation, Dictionary<string, object?> argument)
	{
		return operation.ConstantValue.Value;
	}

	public override object? VisitLock(ILockOperation operation, Dictionary<string, object?> argument)
	{
		lock (Visit(operation.LockedValue, argument))
		{
			Visit(operation.Body, argument);
		}

		return null;
	}

	public override object? VisitDelegateCreation(IDelegateCreationOperation operation, Dictionary<string, object?> argument)
	{
		return Visit(operation.Target, argument);
	}

	public override object? VisitAnonymousFunction(IAnonymousFunctionOperation operation, Dictionary<string, object?> argument)
	{
		var parameters = operation.Symbol.Parameters
			.Select(p => Expression.Parameter(SyntaxHelpers.GetTypeByType(compilation, p.Type), p.Name))
			.ToArray();

		var body = new ExpressionVisitor(compilation, parameters).VisitBlock(operation.Body, argument);
		var lambda = Expression.Lambda(body, parameters);

		// Compileer de lambda en retourneer de gedelegeerde
		return lambda.Compile();
	}

	public override object? VisitMethodReference(IMethodReferenceOperation operation, Dictionary<string, object?> argument)
	{
		var instance = Visit(operation.Instance, argument);
		var containingType = SyntaxHelpers.GetTypeByType(compilation, operation.Method.ContainingType);
		var method = containingType.GetMethod(operation.Method.Name);

		if (method == null)
		{
			return null;
		}

		return method.CreateDelegate(typeof(Delegate), instance);
	}

	public override object? VisitIsType(IIsTypeOperation operation, Dictionary<string, object?> argument)
	{
		var value = Visit(operation.ValueOperand, argument);

		if (value == null)
		{
			return false;
		}

		var targetType = SyntaxHelpers.GetTypeByType(compilation, operation.TypeOperand);
		return targetType.IsInstanceOfType(value);
	}

	public override object? VisitDynamicMemberReference(IDynamicMemberReferenceOperation operation, Dictionary<string, object?> argument)
	{
		var instance = Visit(operation.Instance, argument);

		if (instance == null)
		{
			return null;
		}

		var memberInfo = instance.GetType().GetMember(operation.MemberName).FirstOrDefault();

		return memberInfo switch
		{
			PropertyInfo propertyInfo => propertyInfo.GetValue(instance),
			FieldInfo fieldInfo => fieldInfo.GetValue(instance),
			_ => null
		};

	}

	public override object? VisitDynamicInvocation(IDynamicInvocationOperation operation, Dictionary<string, object?> argument)
	{
		var instance = Visit(operation.Operation, argument);

		if (instance == null)
		{
			return null;
		}

		var arguments = operation.Arguments
			.Select(s => Visit(s, argument))
			.ToArray();

		if (instance is Delegate del)
		{
			return del.DynamicInvoke(arguments);
		}

		return null;
	}

	public override object? VisitDynamicObjectCreation(IDynamicObjectCreationOperation operation, Dictionary<string, object?> argument)
	{
		var arguments = operation.Arguments
			.Select(s => Visit(s, argument))
			.ToArray();

		if (operation.Type == null)
		{
			return null;
		}

		var type = Type.GetType(operation.Type.ToDisplayString());
		return Activator.CreateInstance(type, arguments);
	}

	public override object? VisitTuple(ITupleOperation operation, Dictionary<string, object?> argument)
	{
		var elements = operation.Elements
			.Select(s => Visit(s, argument))
			.ToArray();

		var type = operation.Type;
		return Activator.CreateInstance(Type.GetType(type.ToDisplayString()), elements);
	}

	public override object? VisitDynamicIndexerAccess(IDynamicIndexerAccessOperation operation, Dictionary<string, object?> argument)
	{
		var instance = Visit(operation.Operation, argument);

		if (instance == null)
		{
			return null;
		}

		var arguments = operation.Arguments
			.Select(s => Visit(s, argument))
			.ToArray();

		var indexProperty = instance.GetType().GetProperty("Item");
		return indexProperty?.GetValue(instance, arguments);
	}
}