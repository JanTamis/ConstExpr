using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConstExpr.SourceGenerator.Visitors;

public partial class ConstExprOperationVisitor(Compilation compilation, MetadataLoader loader, Action<IOperation?, Exception> exceptionHandler, CancellationToken token) : OperationVisitor<IDictionary<string, object?>, object?>
{
	public const string RETURNVARIABLENAME = "$return$";

	public override object? DefaultVisit(IOperation operation, IDictionary<string, object?> argument)
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

	public override object? Visit(IOperation? operation, IDictionary<string, object?> argument)
	{
		token.ThrowIfCancellationRequested();

		if (argument.ContainsKey(RETURNVARIABLENAME))
		{
			return null;
		}

		try
		{
			return base.Visit(operation, argument);
		}
		catch (Exception ex)
		{
			exceptionHandler(operation, ex);
			return null;
		}
	}

	public override object VisitIsNull(IIsNullOperation operation, IDictionary<string, object?> argument)
	{
		return Visit(operation.Operand, argument) is null;
	}

	public override object? VisitArrayCreation(IArrayCreationOperation operation, IDictionary<string, object?> argument)
	{
		var arrayType = loader.GetType(operation.Type);

		if (arrayType is null)
		{
			return null;
		}

		var elementType = arrayType.GetElementType();
		var dimensionSizes = operation.DimensionSizes
			.Select(dim => Convert.ToInt32(Visit(dim, argument)))
			.ToArray();

		var data = Array.CreateInstance(elementType, dimensionSizes);

		if (operation.Initializer?.ElementValues is { } values)
		{
			SetValues(data, [ ], 0);
		}

		return data;

		void SetValues(Array arr, int[] indices, int dim)
		{
			if (dim == arr.Rank - 1)
			{
				for (var i = 0; i < arr.GetLength(dim); i++)
				{
					var idx = indices.Append(i).ToArray();
					var flatIndex = GetFlatIndex(idx, arr);

					if (flatIndex < values.Length)
					{
						arr.SetValue(Visit(values[flatIndex], argument), idx);
					}
				}
			}
			else
			{
				for (var i = 0; i < arr.GetLength(dim); i++)
				{
					SetValues(arr, indices.Append(i).ToArray(), dim + 1);
				}
			}
		}

		int GetFlatIndex(int[] indices, Array arr)
		{
			int flat = 0, mul = 1;

			for (var d = arr.Rank - 1; d >= 0; d--)
			{
				flat += indices[d] * mul;
				mul *= arr.GetLength(d);
			}
			return flat;
		}
	}

	public override object? VisitArrayElementReference(IArrayElementReferenceOperation operation, IDictionary<string, object?> argument)
	{
		var array = Visit(operation.ArrayReference, argument);

		if (array is null)
		{
			return null;
		}

		var indexers = operation.Indices
			.Select(s => Visit(s, argument))
			.ToArray();

		// Handle regular arrays (single and multi-dimensional)
		if (array.GetType().IsArray)
		{
			return array.GetType().GetMethod("Get")?.Invoke(array, indexers)
			       ?? array.GetType().GetMethod("GetValue", indexers.Select(i => typeof(int)).ToArray())?.Invoke(array, indexers);
		}

		// Handle collections with indexers (List<T>, Dictionary<K,V>, etc.)
		foreach (var pi in array.GetType().GetProperties())
		{
			var parameters = pi.GetIndexParameters();

			if (parameters.Length == indexers.Length
			    && parameters
				    .Select((p, i) => indexers[i] != null
				                      && (p.ParameterType.IsAssignableFrom(indexers[i].GetType())
				                          || indexers[i] is IConvertible))
				    .All(x => x))
			{
				// Convert indices to the expected parameter types if needed
				for (var i = 0; i < indexers.Length; i++)
				{
					if (indexers[i] is IConvertible && indexers[i].GetType() != parameters[i].ParameterType)
					{
						indexers[i] = Convert.ChangeType(indexers[i], parameters[i].ParameterType);
					}
				}

				return pi.GetValue(array, indexers);
			}
		}

		return null;
	}

	public override object? VisitCoalesce(ICoalesceOperation operation, IDictionary<string, object?> argument)
	{
		return Visit(operation.Value, argument) ?? Visit(operation.WhenNull, argument);
	}

	public override object? VisitCompoundAssignment(ICompoundAssignmentOperation operation, IDictionary<string, object?> argument)
	{
		var target = Visit(operation.Target, argument);
		var value = Visit(operation.Value, argument);

		if (target is null)
		{
			return null;
		}

		return argument[GetVariableName(operation.Target)] = ExecuteBinaryOperation(operation.OperatorKind, target, value);
	}

	public override object? VisitDeconstructionAssignment(IDeconstructionAssignmentOperation operation, IDictionary<string, object?> argument)
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

	public override object? VisitSimpleAssignment(ISimpleAssignmentOperation operation, IDictionary<string, object?> argument)
	{
		return argument[GetVariableName(operation.Target)] = Visit(operation.Value, argument);
	}

	public override object? VisitBinaryOperator(IBinaryOperation operation, IDictionary<string, object?> argument)
	{
		var left = Visit(operation.LeftOperand, argument);
		var right = Visit(operation.RightOperand, argument);

		var operatorKind = operation.OperatorKind;
		var method = operation.OperatorMethod;

		if (method is not null)
		{
			return compilation.ExecuteMethod(loader, method, null, argument, left, right);
		}

		return ExecuteBinaryOperation(operatorKind, left, right);
	}

	public override object? VisitBlock(IBlockOperation operation, IDictionary<string, object?> argument)
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

	public override object? VisitCoalesceAssignment(ICoalesceAssignmentOperation operation, IDictionary<string, object?> argument)
	{
		var target = Visit(operation.Target, argument);

		if (target is not null)
		{
			return target;
		}

		return argument[GetVariableName(operation.Target)] = Visit(operation.Value, argument);
	}

	public override object? VisitCollectionExpression(ICollectionExpressionOperation operation, IDictionary<string, object?> argument)
	{
		if (operation.Type is IArrayTypeSymbol arrayType)
		{
			var elementType = loader.GetType(arrayType.ElementType);
			var data = Array.CreateInstance(elementType, operation.Elements.Length);

			for (var i = 0; i < operation.Elements.Length; i++)
			{
				data.SetValue(Visit(operation.Elements[i], argument), i);
			}

			return data;
		}

		if (operation.Type is not INamedTypeSymbol namedType)
			return null;

		var targetType = loader.GetType(operation.Type);

		if (namedType.Constructors.Any(c => c.Parameters.IsEmpty) && namedType.HasMethod("Add"))
		{
			var instance = Activator.CreateInstance(targetType);
			var addMethod = targetType.GetMethod("Add", [ loader.GetType(operation.Elements[0].Type) ]);

			foreach (var element in operation.Elements)
				addMethod?.Invoke(instance, [ Visit(element, argument) ]);

			return instance;
		}

		if (compilation.TryGetIEnumerableType(operation.Type, false, out var type))
		{
			var elementType = loader.GetType(type);
			var data = Array.CreateInstance(elementType, operation.Elements.Length);

			for (var i = 0; i < operation.Elements.Length; i++)
			{
				data.SetValue(Visit(operation.Elements[i], argument), i);
			}

			return data;
		}

		var elements = operation.Elements.Select(e => Visit(e, argument));

		if (namedType.TypeKind == TypeKind.Interface)
		{
			return namedType.MetadataName switch
			{
				"ICollection" or "ICollection`1" or "IList" or "IList`1" or "IReadOnlyCollection`1" or "IReadOnlyList`1" => CreateCollection(typeof(List<>), namedType, elements),
				"ISet`1" or "IReadOnlySet`1" => CreateCollection(typeof(HashSet<>), namedType, elements),
				_ => elements
			};
		}

		return Activator.CreateInstance(targetType, elements);

		object CreateCollection(Type genericType, INamedTypeSymbol namedType, IEnumerable<object?> elements)
		{
			var elementType = namedType.TypeArguments.Length > 0
				? loader.GetType(namedType.TypeArguments[0])
				: typeof(object);

			var concreteType = genericType.MakeGenericType(elementType);
			var instance = Activator.CreateInstance(concreteType);
			var addMethod = concreteType.GetMethod("Add");

			foreach (var element in elements)
			{
				addMethod?.Invoke(instance, [ element ]);
			}

			return instance;
		}
	}

	public override object? VisitConditional(IConditionalOperation operation, IDictionary<string, object?> argument)
	{
		return Visit(operation.Condition, argument) switch
		{
			true => Visit(operation.WhenTrue, argument),
			false => Visit(operation.WhenFalse, argument),
			_ => throw new InvalidOperationException("Invalid conditional operation."),
		};
	}

	public override object? VisitConditionalAccess(IConditionalAccessOperation operation, IDictionary<string, object?> argument)
	{
		return Visit(operation.Operation, argument) is not null
			? Visit(operation.WhenNotNull, argument)
			: null;
	}

	public override object? VisitConversion(IConversionOperation operation, IDictionary<string, object?> argument)
	{
		var operand = Visit(operation.Operand, argument);
		var conversion = operation.Type;

		if (operation.OperatorMethod is not null)
		{
			// If there's a conversion method, use it
			return compilation.ExecuteMethod(loader, operation.OperatorMethod, null, argument, operand);
		}

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
			SpecialType.System_Collections_IEnumerable => operand as IEnumerable,
			_ => operand,
		};
	}

	public override object? VisitInvocation(IInvocationOperation operation, IDictionary<string, object?> argument)
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

			var visitor = new ConstExprOperationVisitor(compilation, loader, exceptionHandler, token);
			visitor.VisitBlock(methodOperation.BlockBody, variables);

			return variables[RETURNVARIABLENAME];
		}

		return compilation.ExecuteMethod(loader, targetMethod, instance, argument, arguments);
	}

	public override object? VisitSwitch(ISwitchOperation operation, IDictionary<string, object?> argument)
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

	public override object? VisitVariableDeclaration(IVariableDeclarationOperation operation, IDictionary<string, object?> argument)
	{
		foreach (var variable in operation.Declarators)
		{
			VisitVariableDeclarator(variable, argument);
		}

		return null;
	}

	public override object? VisitVariableDeclarationGroup(IVariableDeclarationGroupOperation operation, IDictionary<string, object?> argument)
	{
		foreach (var variable in operation.Declarations)
		{
			VisitVariableDeclaration(variable, argument);
		}

		return null;
	}

	public override object? VisitVariableDeclarator(IVariableDeclaratorOperation operation, IDictionary<string, object?> argument)
	{
		argument[operation.Symbol.Name] = Visit(operation.Initializer?.Value, argument);

		return null;
	}

	public override object? VisitReturn(IReturnOperation operation, IDictionary<string, object?> argument)
	{
		switch (operation.Kind)
		{
			case OperationKind.Return:
				return argument[RETURNVARIABLENAME] = Visit(operation.ReturnedValue, argument);
			case OperationKind.YieldReturn:
				if (!argument.TryGetValue(RETURNVARIABLENAME, out var list) || list is not IList data)
				{
					data = new List<object?>();
					argument[RETURNVARIABLENAME] = data;
				}

				data.Add(Visit(operation.ReturnedValue, argument));
				return null;
			default:
				return null;
		}
	}

	public override object? VisitWhileLoop(IWhileLoopOperation operation, IDictionary<string, object?> argument)
	{
		while (Visit(operation.Condition, argument) is true)
		{
			Visit(operation.Body, argument);
		}

		return null;
	}

	public override object? VisitForLoop(IForLoopOperation operation, IDictionary<string, object?> argument)
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

	public override object? VisitForEachLoop(IForEachLoopOperation operation, IDictionary<string, object?> argument)
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

	public override object VisitSizeOf(ISizeOfOperation operation, IDictionary<string, object?> argument)
	{
		return compilation.GetByteSize(loader, operation.Type);
	}

	public override object VisitInterpolatedString(IInterpolatedStringOperation operation, IDictionary<string, object?> argument)
	{
		var builder = new StringBuilder();

		foreach (var part in operation.Parts)
		{
			switch (part)
			{
				case IInterpolatedStringTextOperation textPart:
					builder.Append(Visit(textPart.Text, argument) as string);
					break;
				case IInterpolationOperation interpolationPart:
					var value = Visit(interpolationPart.Expression, argument);
					var format = Visit(interpolationPart.FormatString, argument)?.ToString();

					var alignment = Visit(interpolationPart.Alignment, argument) switch
					{
						int a => a,
						IConvertible conv => conv.ToInt32(null),
						_ => 0
					};

					var formatted = !String.IsNullOrEmpty(format) && value is IFormattable formattable 
						? formattable.ToString(format, null) 
						: value?.ToString() ?? String.Empty;

					if (alignment != 0)
					{
						formatted = alignment > 0
							? formatted.PadLeft(alignment)
							: formatted.PadRight(-alignment);
					}

					builder.Append(formatted);
					break;
			}
		}

		return builder.ToString();
	}

	public override object? VisitTypeOf(ITypeOfOperation operation, IDictionary<string, object?> argument)
	{
		return loader.GetType(operation.Type);
	}

	public override object VisitArrayInitializer(IArrayInitializerOperation operation, IDictionary<string, object?> argument)
	{
		var data = Array.CreateInstance(loader.GetType(operation.Type)!, operation.ElementValues.Length);

		for (var i = 0; i < operation.ElementValues.Length; i++)
		{
			data.SetValue(Visit(operation.ElementValues[i], argument), i);
		}

		return data;
	}

	public override object? VisitUnaryOperator(IUnaryOperation operation, IDictionary<string, object?> argument)
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

	public override object? VisitIncrementOrDecrement(IIncrementOrDecrementOperation operation, IDictionary<string, object?> argument)
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

	public override object? VisitParenthesized(IParenthesizedOperation operation, IDictionary<string, object?> argument)
	{
		return Visit(operation.Operand, argument);
	}

	public override object? VisitObjectCreation(IObjectCreationOperation operation, IDictionary<string, object?> argument)
	{
		var arguments = operation.Arguments
			.Select(s => Visit(s.Value, argument))
			.ToArray();

		return Activator.CreateInstance(loader.GetType(operation.Type), arguments);
	}

	public override object? VisitAnonymousObjectCreation(IAnonymousObjectCreationOperation operation, IDictionary<string, object?> argument)
	{
		var arguments = operation.Initializers
			.Select(s => Visit(s, argument))
			.ToArray();

		return Activator.CreateInstance(loader.GetType(operation.Type), arguments);
	}

	public override object? VisitInstanceReference(IInstanceReferenceOperation operation, IDictionary<string, object?> argument)
	{

		return operation.ReferenceKind switch
		{
			InstanceReferenceKind.ContainingTypeInstance => argument["this"],
			InstanceReferenceKind.ImplicitReceiver => argument["this"],
			_ => null,
		};
	}

	public override object VisitUtf8String(IUtf8StringOperation operation, IDictionary<string, object?> argument)
	{
		return Encoding.UTF8.GetBytes(operation.Value);
	}

	public override object? VisitDefaultValue(IDefaultValueOperation operation, IDictionary<string, object?> argument)
	{

		switch (operation.Type?.SpecialType)
		{
			case SpecialType.System_Boolean:
				return false;
			case SpecialType.System_Byte:
				return (byte) 0;
			case SpecialType.System_Char:
				return (char) 0;
			case SpecialType.System_DateTime:
				return default(DateTime);
			case SpecialType.System_Decimal:
				return 0M;
			case SpecialType.System_Double:
				return 0D;
			case SpecialType.System_Int16:
				return (short) 0;
			case SpecialType.System_Int32:
				return 0;
			case SpecialType.System_Int64:
				return 0L;
			case SpecialType.System_SByte:
				return (sbyte) 0;
			case SpecialType.System_Single:
				return 0F;
			case SpecialType.System_String:
				return null;
			case SpecialType.System_UInt16:
				return (ushort) 0;
			case SpecialType.System_UInt32:
				return 0U;
			case SpecialType.System_UInt64:
				return 0UL;
			default:
			{
				var type = loader.GetType(operation.Type);

				if (type?.IsValueType is true)
				{
					return Activator.CreateInstance(type);
				}
				
				return null;
			}
		}
	}

	public override object? VisitLocalReference(ILocalReferenceOperation operation, IDictionary<string, object?> argument)
	{
		return argument[operation.Local.Name];
	}

	public override object? VisitParameterReference(IParameterReferenceOperation operation, IDictionary<string, object?> argument)
	{
		return argument[operation.Parameter.Name];
	}

	public override object? VisitFieldReference(IFieldReferenceOperation operation, IDictionary<string, object?> argument)
	{
		var type = loader.GetType(operation.Field.ContainingType);

		if (operation.Field.IsConst)
		{
			var value = operation.Field.ConstantValue;

			if (operation.Field.ContainingType?.TypeKind == TypeKind.Enum)
			{
				// For enum constants, return the value directly without conversion
				return value;
			}

			return value is not null ? Convert.ChangeType(value, type) : null;
		}

		return argument[operation.Field.Name];
	}

	public override object? VisitPropertyReference(IPropertyReferenceOperation operation, IDictionary<string, object?> argument)
	{
		var instance = Visit(operation.Instance, argument);
		var type = loader.GetType(operation.Property.ContainingType);

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

	public override object? VisitAwait(IAwaitOperation operation, IDictionary<string, object?> argument)
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

	public override object? VisitUsing(IUsingOperation operation, IDictionary<string, object?> argument)
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

	public override object? VisitNameOf(INameOfOperation operation, IDictionary<string, object?> argument)
	{
		return operation.ConstantValue.Value;
	}

	public override object? VisitLock(ILockOperation operation, IDictionary<string, object?> argument)
	{
		lock (Visit(operation.LockedValue, argument))
		{
			Visit(operation.Body, argument);
		}

		return null;
	}

	public override object? VisitDelegateCreation(IDelegateCreationOperation operation, IDictionary<string, object?> argument)
	{
		return Visit(operation.Target, argument);
	}

	public override object VisitAnonymousFunction(IAnonymousFunctionOperation operation, IDictionary<string, object?> argument)
	{
		var parameters = operation.Symbol.Parameters
			.Select(p => Expression.Parameter(loader.GetType(p.Type), p.Name))
			.ToArray();

		var body = new ExpressionVisitor(compilation, loader, parameters).VisitBlock(operation.Body, argument);
		var lambda = Expression.Lambda(body, parameters);

		// Compileer de lambda en retourneer de gedelegeerde
		return lambda.Compile();
	}

	public override object? VisitMethodReference(IMethodReferenceOperation operation, IDictionary<string, object?> argument)
	{
		var instance = Visit(operation.Instance, argument);
		var containingType = loader.GetType(operation.Method.ContainingType);
		var method = containingType.GetMethod(operation.Method.Name);

		if (method == null)
		{
			return null;
		}

		return method.CreateDelegate(typeof(Delegate), instance);
	}

	public override object VisitIsType(IIsTypeOperation operation, IDictionary<string, object?> argument)
	{
		var value = Visit(operation.ValueOperand, argument);

		if (value == null)
		{
			return false;
		}

		var targetType = loader.GetType(operation.TypeOperand);
		return targetType.IsInstanceOfType(value);
	}

	public override object? VisitDynamicMemberReference(IDynamicMemberReferenceOperation operation, IDictionary<string, object?> argument)
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

	public override object? VisitDynamicInvocation(IDynamicInvocationOperation operation, IDictionary<string, object?> argument)
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

	public override object? VisitDynamicObjectCreation(IDynamicObjectCreationOperation operation, IDictionary<string, object?> argument)
	{
		var arguments = operation.Arguments
			.Select(s => Visit(s, argument))
			.ToArray();

		if (operation.Type == null)
		{
			return null;
		}

		return Activator.CreateInstance(loader.GetType(operation.Type), arguments);
	}

	public override object? VisitTuple(ITupleOperation operation, IDictionary<string, object?> argument)
	{
		var elements = operation.Elements
			.Select(s => Visit(s, argument))
			.ToArray();

		return Activator.CreateInstance(loader.GetType(operation.Type), elements);
	}

	public override object? VisitDynamicIndexerAccess(IDynamicIndexerAccessOperation operation, IDictionary<string, object?> argument)
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

	public override object? VisitExpressionStatement(IExpressionStatementOperation operation, IDictionary<string, object?> argument)
	{
		return Visit(operation.Operation, argument);
	}

	public override object? VisitTry(ITryOperation operation, IDictionary<string, object?> argument)
	{
		try
		{
			Visit(operation.Body, argument);
		}
		catch (Exception e)
		{
			foreach (var exception in operation.Catches)
			{
				var type = loader.GetType(exception.ExceptionType);

				if (type != null && type.IsInstanceOfType(e) && exception.Filter is null || Visit(exception.Filter, argument) is true)
				{
					foreach (var variable in exception.Locals)
					{
						argument[variable.Name] = e;
					}

					Visit(exception.Handler, argument);
					return null;
				}
			}
		}
		finally
		{
			if (operation.Finally is not null)
			{
				Visit(operation.Finally, argument);
			}
		}

		return null;
	}

	public override object? VisitRangeOperation(IRangeOperation operation, IDictionary<string, object?> argument)
	{
		var left = Visit(operation.LeftOperand, argument);
		var right = Visit(operation.RightOperand, argument);
		var method = operation.Method;

		return compilation.ExecuteMethod(loader, method!, null, argument, left, right);
	}

	// Pattern matching support
	public override object VisitIsPattern(IIsPatternOperation operation, IDictionary<string, object?> argument)
	{
		var value = Visit(operation.Value, argument);
		var pattern = operation.Pattern;

		return MatchPattern(value, pattern, argument);
	}

	public override object? VisitSwitchExpression(ISwitchExpressionOperation operation, IDictionary<string, object?> argument)
	{
		var value = Visit(operation.Value, argument);

		foreach (var arm in operation.Arms)
		{
			if (MatchPattern(value, arm.Pattern, argument))
			{
				if (arm.Guard == null || Visit(arm.Guard, argument) is true)
				{
					return Visit(arm.Value, argument);
				}
			}
		}

		return null;
	}

	public override object? VisitWith(IWithOperation operation, IDictionary<string, object?> argument)
	{
		var receiver = Visit(operation.Operand, argument);

		if (receiver == null) return null;

		var type = receiver.GetType();
		var copyCtor = type.GetConstructor([ type ]);

		object clone;

		if (copyCtor != null)
		{
			clone = copyCtor.Invoke([ receiver ]);
		}
		else
		{
			var memberwiseClone = type.GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
			clone = memberwiseClone?.Invoke(receiver, null) ?? throw new InvalidOperationException("Cannot clone object for with-expression.");
		}

		foreach (var assignment in operation.Initializer.ChildOperations.OfType<ISimpleAssignmentOperation>())
		{
			var property = type.GetProperty(assignment.Target.ToString());

			if (property != null && property.CanWrite)
			{
				var value = Visit(assignment.Value, argument);
				property.SetValue(clone, value);
			}
		}

		return clone;
	}

	private bool MatchPattern(object? value, IPatternOperation pattern, IDictionary<string, object?> argument)
	{
		switch (pattern)
		{
			case IConstantPatternOperation constantPattern:
				return Equals(value, Visit(constantPattern.Value, argument));
			case IDeclarationPatternOperation declarationPattern:
				if (declarationPattern.MatchedType != null && value != null)
				{
					var matchedType = loader.GetType(declarationPattern.MatchedType);

					if (matchedType != null && !matchedType.IsInstanceOfType(value))
					{
						return false;
					}

					// If the pattern has a declaration, store the value in the argument dictionary
					if (declarationPattern.DeclaredSymbol is { } declaration)
					{
						argument[declaration.Name] = value;
					}
				}

				return value == null;
			case IDiscardPatternOperation:
				return true;
			case IRelationalPatternOperation relationalPattern:
				if (value is IComparable comparable && relationalPattern.Value.ConstantValue is { HasValue: true, Value: var relValue })
				{
					var cmp = comparable.CompareTo(relValue);

					return relationalPattern.OperatorKind switch
					{
						BinaryOperatorKind.LessThan => cmp < 0,
						BinaryOperatorKind.LessThanOrEqual => cmp <= 0,
						BinaryOperatorKind.GreaterThan => cmp > 0,
						BinaryOperatorKind.GreaterThanOrEqual => cmp >= 0,
						_ => false
					};
				}

				return false;
			case IBinaryPatternOperation binaryPattern:
				var left = MatchPattern(value, binaryPattern.LeftPattern, argument);
				var right = MatchPattern(value, binaryPattern.RightPattern, argument);

				return binaryPattern.OperatorKind switch
				{
					BinaryOperatorKind.And => left && right,
					BinaryOperatorKind.Or => left || right,
					_ => false
				};
			case INegatedPatternOperation negatedPattern:
				return !MatchPattern(value, negatedPattern.Pattern, argument);
			// Add more pattern types as needed (recursive, property, list, etc.)

			default:
				return false;
		}
	}
}