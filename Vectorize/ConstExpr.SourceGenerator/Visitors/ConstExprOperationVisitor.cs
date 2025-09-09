using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConstExpr.SourceGenerator.Visitors;

public partial class ConstExprOperationVisitor(Compilation compilation, MetadataLoader loader, Action<IOperation?, Exception> exceptionHandler, CancellationToken token) : OperationVisitor<IDictionary<string, object?>, object?>
{
	public const string RETURNVARIABLENAME = "$return$";

	private bool isYield = false;

	private static readonly object BreakSentinel = new();
	private static readonly object ContinueSentinel = new();

	public bool ShouldThrow { get; set; } = true;

	public override object? DefaultVisit(IOperation operation, IDictionary<string, object?> argument)
	{
		if (operation.ConstantValue is { HasValue: true, Value: var value })
		{
			return value;
		}

		exceptionHandler(operation, new NotImplementedException($"Operation of type {operation.Kind} is not supported."));

		return null;
	}

	public override object? Visit(IOperation? operation, IDictionary<string, object?> argument)
	{
		if (operation is null || token.IsCancellationRequested || !isYield && argument.ContainsKey(RETURNVARIABLENAME))
		{
			return null;
		}

		if (operation.ConstantValue is { HasValue: true, Value: var value })
		{
			return value;
		}

		try
		{
			return base.Visit(operation, argument);
		}
		catch (Exception ex)
		{
			if (operation is not IThrowOperation and not IBlockOperation)
			{
				exceptionHandler(operation, ex);
			}

			if (ShouldThrow)
			{
				throw;
			}

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
			SetValues(data, [], 0);
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
		if (array.GetType().IsArray && array is Array arr)
		{
			if (indexers.All(i => i is int))
			{
				return arr.GetValue(indexers.Cast<int>().ToArray());
			}

			if (indexers.All(i => i is long))
			{
				return arr.GetValue(indexers.Cast<long>().ToArray());
			}

			var rangeType = loader.GetType("System.Range");

			if (indexers.All(i => i.GetType() == rangeType))
			{
				var range = (ValueTuple<int, int>)rangeType.GetMethod("GetOffsetAndLength").Invoke(indexers[0], [arr.Length]);
				var result = Array.CreateInstance(arr.GetType().GetElementType(), range.Item2);

				Array.Copy(arr, range.Item1, result, 0, range.Item2);
				return result;
			}

			return null;
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

		return argument[GetVariableName(operation.Target)] = ObjectExtensions.ExecuteBinaryOperation(operation.OperatorKind, target, value);
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
		var value = Visit(operation.Value, argument);

		if (operation.Target is IArrayElementReferenceOperation arrayElement)
		{
			var array = Visit(arrayElement.ArrayReference, argument);

			var indices = arrayElement.Indices
				.Select(s => Visit(s, argument))
				.ToArray();

			if (array is Array arr)
			{
				if (indices.All(a => a is int))
				{
					arr.SetValue(value, indices.Cast<int>().ToArray());
				}
				else if (indices.All(a => a is long))
				{
					arr.SetValue(value, indices.Cast<long>().ToArray());
				}

				return value;
			}
		}
		else if (operation.Target is IPropertyReferenceOperation propertyReference)
		{
			var instance = Visit(propertyReference.Instance, argument);
			var type = instance?.GetType() ?? loader.GetType(propertyReference.Property.ContainingType);

			if (propertyReference.Arguments.Length > 0)
			{
				var propertyInfo = type
														 .GetProperties()
														 .FirstOrDefault(f => f.GetIndexParameters().Length == propertyReference.Arguments.Length)
													 ?? throw new InvalidOperationException("Indexer property info could not be retrieved.");

				var indices = propertyReference.Arguments.Select(a => Visit(a.Value, argument)).ToArray();

				propertyInfo.SetValue(instance, value, indices);

				return value;
			}
			else
			{
				var name = propertyReference.Property.Name;

				var propertyInfo = type
														 .GetProperties()
														 .FirstOrDefault(f => f.Name == name && f.GetMethod.IsStatic == propertyReference.Property.IsStatic)
													 ?? throw new InvalidOperationException("Property info could not be retrieved.");

				if (propertyReference.Property.IsStatic)
				{
					propertyInfo.SetValue(null, value);
					return value;
				}

				propertyInfo.SetValue(instance, value);
				return value;
			}
		}

		return argument[GetVariableName(operation.Target)] = value;
	}

	public override object? VisitBinaryOperator(IBinaryOperation operation, IDictionary<string, object?> argument)
	{
		var left = Visit(operation.LeftOperand, argument);
		var right = Visit(operation.RightOperand, argument);

		var operatorKind = operation.OperatorKind;
		var method = operation.OperatorMethod;

		if (loader.TryExecuteMethod(method, null, argument, [left, right], out var value))
		{
			return value;
		}

		return ObjectExtensions.ExecuteBinaryOperation(operatorKind, left, right);
	}

	public override object? VisitBlock(IBlockOperation operation, IDictionary<string, object?> argument)
	{
		foreach (var currentOperation in operation.Operations)
		{
			var result = Visit(currentOperation, argument);

			if (ReferenceEquals(result, ContinueSentinel))
			{
				break;
			}

			if (ReferenceEquals(result, BreakSentinel))
			{
				return result;
			}
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
		{
			return null;
		}

		var targetType = loader.GetType(operation.Type);

		if (namedType.Constructors.Any(c => c.Parameters.IsEmpty) && namedType.HasMethod("Add"))
		{
			var instance = Activator.CreateInstance(targetType);
			var addMethod = targetType.GetMethod("Add", [loader.GetType(operation.Elements[0].Type)]);

			foreach (var element in operation.Elements)
			{
				addMethod?.Invoke(instance, [Visit(element, argument)]);
			}

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
				addMethod?.Invoke(instance, [element]);
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

		if (loader.TryExecuteMethod(operation.OperatorMethod, null, argument, [operand], out var value))
		{
			// If there's a conversion method, use it
			return value;
		}

		if (operand is null)
		{
			return null;
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

		if (SyntaxHelpers.IsInConstExprBody(targetMethod))
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

					variables.Add(parameterName, arguments[i]);
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

				return variables[RETURNVARIABLENAME];
			}
		}
		
		

		loader.TryExecuteMethod(targetMethod, instance, argument, arguments, out var value);
		return value;
	}

	public override object? VisitSwitch(ISwitchOperation operation, IDictionary<string, object?> argument)
	{
		var value = Visit(operation.Value, argument);

		// Exact match
		foreach (var caseClause in operation.Cases)
		{
			var clauses = caseClause.Clauses
				.Where(w => w.CaseKind != CaseKind.Default)
				.ToArray();

			var isValid = false;

			foreach (var clause in clauses)
			{
				if (isValid) break;

				switch (clause)
				{
					case ISingleValueCaseClauseOperation singleValue:
						{
							var caseValue = Visit(singleValue.Value, argument);

							if (Equals(value, caseValue))
							{
								isValid = true;
							}
							break;
						}
					case IRangeCaseClauseOperation rangeClause:
						{
							var min = Visit(rangeClause.MinimumValue, argument);
							var max = Visit(rangeClause.MaximumValue, argument);

							if (value is IComparable cmp
									&& min is IComparable minC && max is IComparable maxC
									&& cmp.CompareTo(minC) >= 0 && cmp.CompareTo(maxC) <= 0)
							{
								isValid = true;
							}
							break;
						}
					case IRelationalCaseClauseOperation relationalClause:
						{
							var relVal = Visit(relationalClause.Value, argument);

							if (value is IComparable relCmp && relVal != null)
							{
								var res = relCmp.CompareTo(relVal);
								isValid = relationalClause.Relation switch
								{
									BinaryOperatorKind.LessThan => res < 0,
									BinaryOperatorKind.LessThanOrEqual => res <= 0,
									BinaryOperatorKind.GreaterThan => res > 0,
									BinaryOperatorKind.GreaterThanOrEqual => res >= 0,
									BinaryOperatorKind.Equals => res == 0,
									BinaryOperatorKind.NotEquals => res != 0,
									_ => false
								};
							}
							break;
						}
					case IPatternCaseClauseOperation patternClause:
						{
							if (MatchPattern(value, patternClause.Pattern, argument))
							{
								if (patternClause.Guard is null || Visit(patternClause.Guard, argument) is true)
								{
									isValid = true;
								}
							}
							break;
						}
				}
			}

			if (isValid)
			{
				VisitList(caseClause.Body, argument);
				return null;
			}
		}

		// Default case
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
		return argument[operation.Symbol.Name] = Visit(operation.Initializer?.Value, argument);
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

					isYield = true;
				}

				data.Add(Visit(operation.ReturnedValue, argument));
				return null;
			default:
				return null;
		}
	}

	public override object? VisitBranch(IBranchOperation operation, IDictionary<string, object?> argument)
	{
		return operation.BranchKind switch
		{
			BranchKind.Break => BreakSentinel,
			BranchKind.Continue => ContinueSentinel,
			_ => null
		};
	}

	public override object? VisitWhileLoop(IWhileLoopOperation operation, IDictionary<string, object?> argument)
	{
		while (Visit(operation.Condition, argument) is true)
		{
			var loopResult = Visit(operation.Body, argument);
			if (ReferenceEquals(loopResult, BreakSentinel)) break;
			if (ReferenceEquals(loopResult, ContinueSentinel)) continue;
		}

		return null;
	}

	public override object? VisitForLoop(IForLoopOperation operation, IDictionary<string, object?> argument)
	{
		for (VisitList(operation.Before, argument); Visit(operation.Condition, argument) is true; VisitList(operation.AtLoopBottom, argument))
		{
			var loopResult = Visit(operation.Body, argument);
			if (ReferenceEquals(loopResult, BreakSentinel)) break;
			if (ReferenceEquals(loopResult, ContinueSentinel)) continue;
		}

		return null;
	}

	public override object? VisitForEachLoop(IForEachLoopOperation operation, IDictionary<string, object?> argument)
	{
		var itemName = GetVariableName(operation.LoopControlVariable);
		var collection = Visit(operation.Collection, argument);

		foreach (var item in collection as IEnumerable)
		{
			argument[itemName] = item;

			var loopResult = Visit(operation.Body, argument);

			if (ReferenceEquals(loopResult, BreakSentinel)) break;
			if (ReferenceEquals(loopResult, ContinueSentinel)) continue;
		}

		return null;
	}

	public override object? VisitInterpolation(IInterpolationOperation operation, IDictionary<string, object?> argument)
	{
		var value = Visit(operation.Expression, argument);

		if (value is IFormattable formattable)
		{
			return formattable.ToString(Visit(operation.FormatString, argument) as string, CultureInfo.InvariantCulture);
		}

		return value?.ToString();
	}

	public override object? VisitInterpolatedStringText(IInterpolatedStringTextOperation operation, IDictionary<string, object?> argument)
	{
		return operation.Text;
	}

	public override object? VisitSizeOf(ISizeOfOperation operation, IDictionary<string, object?> argument)
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
		return loader.GetType(operation.TypeOperand);
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
			UnaryOperatorKind.Minus => 0.Subtract(operand),
			UnaryOperatorKind.BitwiseNegation => operand.BitwiseNot(),
			UnaryOperatorKind.Not => operand.LogicalNot(),
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
			OperationKind.Increment => target.Add(1),
			OperationKind.Decrement => target.Add(-1),
			_ => target,
		};
	}

	public override object? VisitParenthesized(IParenthesizedOperation operation, IDictionary<string, object?> argument)
	{
		return Visit(operation.Operand, argument);
	}

	public override object? VisitObjectCreation(IObjectCreationOperation operation, IDictionary<string, object?> argument)
	{
		if (operation is { Type.SpecialType: SpecialType.System_Object })
		{
			return new object();
		}

		var arguments = operation.Arguments
			.Select(s => Visit(s.Value, argument))
			.ToArray();

		var result = Activator.CreateInstance(loader.GetType(operation.Type), arguments);

		if (operation.Initializer?.Initializers.Length > 0)
		{
			foreach (var initializer in operation.Initializer.Initializers)
			{
				switch (initializer)
				{
					case IInvocationOperation invocation:
						{
							var method = result?.GetType().GetMethod(invocation.TargetMethod.Name, invocation.Arguments.Select(a => loader.GetType(a.Value.Type)).ToArray());

							method?.Invoke(result, invocation.Arguments.Select(a => Visit(a.Value, argument)).ToArray());
							break;
						}
					case ISimpleAssignmentOperation assignment:
						{
							var name = assignment.Target switch
							{
								IPropertyReferenceOperation propRef => propRef.Property.Name,
								IFieldReferenceOperation fieldRef => fieldRef.Field.Name,
								_ => null
							};

							if (name is not null)
							{
								var propertyInfo = result?.GetType()
									.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
									.FirstOrDefault(f => f.Name == name);

								if (propertyInfo != null && propertyInfo.CanWrite)
								{
									propertyInfo.SetValue(result, Visit(assignment.Value, argument));
								}
								else
								{
									var fieldInfo = result?.GetType()
										.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
										.FirstOrDefault(f => f.Name == name);

									if (fieldInfo != null)
									{
										fieldInfo.SetValue(result, Visit(assignment.Value, argument));
									}
								}
							}
							break;
						}
				}
			}
		}

		return result;
	}

	public override object? VisitAnonymousObjectCreation(IAnonymousObjectCreationOperation operation, IDictionary<string, object?> argument)
	{
		// Collect property names and values
		var properties = new Dictionary<string, object?>();

		foreach (var initializer in operation.Initializers)
		{
			if (initializer is ISimpleAssignmentOperation assignment)
			{
				var name = assignment.Target switch
				{
					IPropertyReferenceOperation propRef => propRef.Property.Name,
					IFieldReferenceOperation fieldRef => fieldRef.Field.Name,
					_ => null
				};

				if (name is not null)
				{
					properties[name] = Visit(assignment.Value, argument);
				}
			}
		}

		// Dynamically create a type with the required properties
		var type = CreateAnonymousType(properties);
		var instance = Activator.CreateInstance(type);

		// Set property values
		foreach (var kvp in properties)
		{
			var prop = type?.GetProperty(kvp.Key);

			if (prop != null && prop.CanWrite)
			{
				prop.SetValue(instance, kvp.Value);
			}
		}

		return instance;
	}

	private static Type? CreateAnonymousType(IDictionary<string, object?> properties)
	{
		var asmName = new AssemblyName("DynamicAnonymousTypes");
		var asmBuilder = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);
		var modBuilder = asmBuilder.DefineDynamicModule("MainModule");
		var typeBuilder = modBuilder.DefineType($"AnonType_{Guid.NewGuid():N}", TypeAttributes.Public);

		foreach (var kvp in properties)
		{
			var fieldBuilder = typeBuilder.DefineField($"_{kvp.Key}", kvp.Value?.GetType() ?? typeof(object), FieldAttributes.Private);
			var propBuilder = typeBuilder.DefineProperty(kvp.Key, PropertyAttributes.HasDefault, kvp.Value?.GetType() ?? typeof(object), null);

			// Getter
			var getter = typeBuilder.DefineMethod($"get_{kvp.Key}", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, kvp.Value?.GetType() ?? typeof(object), Type.EmptyTypes);
			var ilGet = getter.GetILGenerator();
			ilGet.Emit(OpCodes.Ldarg_0);
			ilGet.Emit(OpCodes.Ldfld, fieldBuilder);
			ilGet.Emit(OpCodes.Ret);
			propBuilder.SetGetMethod(getter);

			// Setter
			var setter = typeBuilder.DefineMethod($"set_{kvp.Key}", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, null, [kvp.Value?.GetType() ?? typeof(object)]);
			var ilSet = setter.GetILGenerator();
			ilSet.Emit(OpCodes.Ldarg_0);
			ilSet.Emit(OpCodes.Ldarg_1);
			ilSet.Emit(OpCodes.Stfld, fieldBuilder);
			ilSet.Emit(OpCodes.Ret);
			propBuilder.SetSetMethod(setter);
		}

		return typeBuilder.CreateTypeInfo()?.AsType();
	}

	// public override object? VisitInstanceReference(IInstanceReferenceOperation operation, IDictionary<string, object?> argument)
	// {
	// 	return operation.ReferenceKind switch
	// 	{
	// 		InstanceReferenceKind.ContainingTypeInstance => argument["this"],
	// 		InstanceReferenceKind.ImplicitReceiver => argument["this"],
	// 		_ => null,
	// 	};
	// }

	public override object VisitUtf8String(IUtf8StringOperation operation, IDictionary<string, object?> argument)
	{
		return Encoding.UTF8.GetBytes(operation.Value);
	}

	public override object? VisitDefaultValue(IDefaultValueOperation operation, IDictionary<string, object?> argument)
	{
		if (operation.Type is null or { IsReferenceType: true })
		{
			return null;
		}

		return operation.Type?.SpecialType switch
		{
			SpecialType.System_Boolean => false,
			SpecialType.System_Byte => (byte)0,
			SpecialType.System_Char => (char)0,
			SpecialType.System_DateTime => default(DateTime),
			SpecialType.System_Decimal => 0M,
			SpecialType.System_Double => 0D,
			SpecialType.System_Int16 => (short)0,
			SpecialType.System_Int32 => 0,
			SpecialType.System_Int64 => 0L,
			SpecialType.System_SByte => (sbyte)0,
			SpecialType.System_Single => 0F,
			SpecialType.System_UInt16 => (ushort)0,
			SpecialType.System_UInt32 => 0U,
			SpecialType.System_UInt64 => 0UL,
			_ => Activator.CreateInstance(loader.GetType(operation.Type)),
		};
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

		if (operation.Instance is not null)
		{
			var instance = Visit(operation.Instance, argument);

			var fieldInfo = type
				.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
				.FirstOrDefault(f => f.Name == operation.Field.Name);

			if (fieldInfo == null)
			{
				throw new InvalidOperationException("Field info could not be retrieved.");
			}

			if (operation.Field.IsStatic)
			{
				return fieldInfo.GetValue(null);
			}

			if (instance is IConvertible)
			{
				instance = Convert.ChangeType(instance, fieldInfo.DeclaringType);
			}

			return fieldInfo.GetValue(instance);
		}

		if (operation.Field.IsStatic)
		{
			var fieldInfo = type
				.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
				.FirstOrDefault(f => f.Name == operation.Field.Name);

			if (fieldInfo == null)
			{
				throw new InvalidOperationException("Static field info could not be retrieved.");
			}

			return fieldInfo.GetValue(null);
		}

		return argument[operation.Field.Name];
	}

	public override object? VisitPropertyReference(IPropertyReferenceOperation operation, IDictionary<string, object?> argument)
	{
		var instance = Visit(operation.Instance, argument);
		var type = loader.GetType(operation.Property.ContainingType) ?? instance?.GetType();

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
					return array.GetValue(indices.Cast<int>().ToArray());
				}

				if (indices.All(a => a is long))
				{
					return array.GetValue(indices.Cast<long>().ToArray());
				}
			}

			return propertyInfo.GetValue(instance, indices);
		}
		else
		{
			var name = operation.Property.Name;

			var propertyInfo = type
				.GetProperties()
				.FirstOrDefault(f => f.Name == name && f.GetMethod.IsStatic == operation.Property.IsStatic);

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
		var containingType = instance?.GetType() ?? loader.GetType(operation.Method.ContainingType);
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

	public override object? VisitThrow(IThrowOperation operation, IDictionary<string, object?> argument)
	{
		throw Visit(operation.Exception, argument) as Exception;
	}

	public override object? VisitRangeOperation(IRangeOperation operation, IDictionary<string, object?> argument)
	{
		// Use reflection to construct System.Range / System.Index at runtime (generator targets netstandard2.0)
		var indexType = loader.GetType("System.Index");
		var rangeType = loader.GetType("System.Range");

		if (indexType == null || rangeType == null)
		{
			return null; // Environment without Index/Range support
		}

		var startIndexObj = CreateIndex(operation.LeftOperand);
		var endIndexObj = CreateIndex(operation.RightOperand);

		if (startIndexObj == null && endIndexObj == null)
		{
			return rangeType.GetProperty("All", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
		}

		if (startIndexObj == null && endIndexObj != null)
		{
			var endAt = rangeType.GetMethod("EndAt", BindingFlags.Public | BindingFlags.Static, null, [indexType], null);
			return endAt?.Invoke(null, [endIndexObj]);
		}

		if (startIndexObj != null && endIndexObj == null)
		{
			var startAt = rangeType.GetMethod("StartAt", BindingFlags.Public | BindingFlags.Static, null, [indexType], null);
			return startAt?.Invoke(null, [startIndexObj]);
		}

		var ctorRange = rangeType.GetConstructor([indexType, indexType]);
		return ctorRange?.Invoke([startIndexObj, endIndexObj]);

		object? CreateIndex(IOperation? op)
		{
			if (op is null)
			{
				return null;
			}

			var fromEnd = false;
			object? valueObj;

			if (op is IUnaryOperation u)
			{
				// Detect '^' from-end syntax via textual representation
				var text = u.Syntax.ToString().TrimStart();

				if (text.StartsWith("^"))
				{
					fromEnd = true;
				}

				valueObj = Visit(u.Operand, argument);
			}
			else
			{
				valueObj = Visit(op, argument);
			}

			if (valueObj is IConvertible conv)
			{
				var intValue = Convert.ToInt32(conv, CultureInfo.InvariantCulture);
				var ctor = indexType.GetConstructor([typeof(int), typeof(bool)]);
				return ctor?.Invoke([intValue, fromEnd]);
			}

			return valueObj;
		}
	}

	public override object VisitIsPattern(IIsPatternOperation operation, IDictionary<string, object?> argument)
	{
		var value = Visit(operation.Value, argument);
		return MatchPattern(value, operation.Pattern, argument);
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

	public override object? VisitLocalFunction(ILocalFunctionOperation operation, IDictionary<string, object?> argument) => null; // skip

	public override object? VisitWith(IWithOperation operation, IDictionary<string, object?> argument)
	{
		var receiver = Visit(operation.Operand, argument);
		if (receiver == null) return null;

		var type = receiver.GetType();
		var copyCtor = type.GetConstructor([type]);
		object clone;

		if (copyCtor != null)
		{
			clone = copyCtor.Invoke([receiver]);
		}
		else
		{
			var memberwiseClone = type.GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
			clone = memberwiseClone?.Invoke(receiver, null) ?? throw new InvalidOperationException("Cannot clone object for with-expression.");
		}

		foreach (var assignment in operation.Initializer.ChildOperations.OfType<ISimpleAssignmentOperation>())
		{
			var propName = assignment.Target.ToString();
			var prop = type.GetProperty(propName);

			if (prop != null && prop.CanWrite)
			{
				prop.SetValue(clone, Visit(assignment.Value, argument));
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
					if (matchedType != null && !matchedType.IsInstanceOfType(value)) return false;

					if (declarationPattern.DeclaredSymbol is { } decl)
					{
						argument[decl.Name] = value;
					}
				}
				return value == null;
			case IDiscardPatternOperation:
				return true;
			case IRelationalPatternOperation relationalPattern:
				if (value is IComparable cmp && relationalPattern.Value.ConstantValue is { HasValue: true, Value: var relVal })
				{
					var res = cmp.CompareTo(relVal);
					return relationalPattern.OperatorKind switch
					{
						BinaryOperatorKind.LessThan => res < 0,
						BinaryOperatorKind.LessThanOrEqual => res <= 0,
						BinaryOperatorKind.GreaterThan => res > 0,
						BinaryOperatorKind.GreaterThanOrEqual => res >= 0,
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
			case IListPatternOperation listPattern:
				if (value is not IEnumerable enumerable) return false;
				var elements = enumerable.Cast<object?>().ToList();
				if (elements.Count != listPattern.ChildOperations.Count) return false;

				foreach (var (index, child) in listPattern.ChildOperations.Index())
				{
					if (!MatchPattern(elements[index], child as IPatternOperation, argument)) return false;
				}
				return true;
			default:
				return false;
		}
	}

	private string? GetVariableName(IOperation operation)
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

	private void VisitList(ImmutableArray<IOperation> operations, IDictionary<string, object?> argument)
	{
		foreach (var operation in operations)
		{
			var loopResult = Visit(operation, argument);

			if (ReferenceEquals(loopResult, BreakSentinel)) break;
		}
	}
}