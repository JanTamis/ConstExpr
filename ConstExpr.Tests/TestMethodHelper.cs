using System.Runtime.CompilerServices;

namespace ConstExpr.Tests;

/// <summary>
///   Helper to convert lambda expressions to C# source code for TestMethod.
///   Uses reflection to extract method signature and body.
/// </summary>
public static class TestMethodHelper
{
	/// <summary>
	///   Converts a delegate to its C# source representation.
	///   Note: This extracts the signature but cannot decompile the body at runtime.
	///   The body must be provided separately or use CallerArgumentExpression.
	/// </summary>
	public static string ToMethodSource<TDelegate>(
		TDelegate method,
		[CallerArgumentExpression(nameof(method))]
		string? lambdaSource = null)
		where TDelegate : Delegate
	{
		var returnType = GetTypeName(method.Method.ReturnType);
		var parameters = method.Method.GetParameters();
		var paramList = System.String.Join(", ", parameters.Select(p => $"{GetTypeName(p.ParameterType)} {p.Name}"));

		// Try to extract body from CallerArgumentExpression
		var body = ExtractLambdaBody(lambdaSource);

		return $"""
			{returnType} TestMethod({paramList})
			{body}
			""";
	}

	public static string ExtractLambdaBody(string? lambdaSource)
	{
		if (System.String.IsNullOrWhiteSpace(lambdaSource))
		{
			return "{\n\tthrow new NotImplementedException();\n}";
		}

		var arrowIndex = FindArrowIndex(lambdaSource);

		if (arrowIndex < 0)
		{
			return "{\n\tthrow new NotImplementedException();\n}";
		}

		var body = lambdaSource.Substring(arrowIndex + 2).Trim();

		return body.StartsWith('{') ? body : "{\n\treturn " + body + ";\n}";
	}

	public static string ExtractLambda(string? lambdaSource)
	{
		if (System.String.IsNullOrWhiteSpace(lambdaSource))
		{
			return "throw new NotImplementedException();";
		}

		var arrowIndex = FindArrowIndex(lambdaSource);

		if (arrowIndex < 0)
		{
			return "throw new NotImplementedException();";
		}

		var body = lambdaSource.Substring(arrowIndex + 2).Trim();

		if (body.StartsWith('{'))
		{
			return body.Trim('{', '}').Trim();
		}

		return "return " + body + ";";
	}

	/// <summary>
	///   Finds the index of the lambda arrow <c>=&gt;</c> while skipping <c>&gt;=</c> and <c>&lt;=</c> operators.
	/// </summary>
	private static int FindArrowIndex(string source)
	{
		for (var i = 0; i < source.Length - 1; i++)
		{
			if (source[i] == '=' && source[i + 1] == '>')
			{
				// Make sure this is not >=  (previous char is >)
				if (i > 0 && source[i - 1] == '>')
				{
					continue;
				}

				return i;
			}
		}

		return -1;
	}

	public static string GetTypeName(Type type)
	{
		if (type == typeof(void)) return "void";
		if (type == typeof(int)) return "int";
		if (type == typeof(uint)) return "uint";
		if (type == typeof(long)) return "long";
		if (type == typeof(ulong)) return "ulong";
		if (type == typeof(short)) return "short";
		if (type == typeof(ushort)) return "ushort";
		if (type == typeof(byte)) return "byte";
		if (type == typeof(sbyte)) return "sbyte";
		if (type == typeof(string)) return "string";
		if (type == typeof(bool)) return "bool";
		if (type == typeof(double)) return "double";
		if (type == typeof(float)) return "float";
		if (type == typeof(char)) return "char";
		if (type == typeof(decimal)) return "decimal";
		if (type == typeof(object)) return "object";
		if (type == typeof(nint)) return "nint";
		if (type == typeof(nuint)) return "nuint";

		// Handle nullable value types
		var underlying = Nullable.GetUnderlyingType(type);

		if (underlying != null)
		{
			return $"{GetTypeName(underlying)}?";
		}

		// Handle tuples
		if (type.IsGenericType && type.FullName?.StartsWith("System.ValueTuple") == true)
		{
			var args = type.GetGenericArguments();

			if (args.Length == 1)
			{
				return $"{System.String.Join(", ", args.Select(GetTypeName))}";
			}

			return $"({System.String.Join(", ", args.Select(GetTypeName))})";
		}

		// Handle generic types
		if (type.IsGenericType)
		{
			var genericName = type.Name.Substring(0, type.Name.IndexOf('`'));
			var genericArgs = System.String.Join(", ", type.GetGenericArguments().Select(GetTypeName));
			return $"{genericName}<{genericArgs}>";
		}

		// Handle arrays
		if (type.IsArray)
		{
			var elementType = type.GetElementType()!;
			var rank = type.GetArrayRank();
			var brackets = rank == 1 ? "[]" : $"[{new string(',', rank - 1)}]";
			return $"{GetTypeName(elementType)}{brackets}";
		}

		return type.Name;
	}
}