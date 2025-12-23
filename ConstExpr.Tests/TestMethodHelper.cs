using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace ConstExpr.Tests;

/// <summary>
/// Helper to convert lambda expressions to C# source code for TestMethod.
/// Uses reflection to extract method signature and body.
/// </summary>
public static class TestMethodHelper
{
	/// <summary>
	/// Converts a delegate to its C# source representation.
	/// Note: This extracts the signature but cannot decompile the body at runtime.
	/// The body must be provided separately or use CallerArgumentExpression.
	/// </summary>
	public static string ToMethodSource<TDelegate>(
		TDelegate method,
		[CallerArgumentExpression(nameof(method))]
		string? lambdaSource = null)
		where TDelegate : Delegate
	{
		var returnType = GetTypeName(method.Method.ReturnType);
		var parameters = method.Method.GetParameters();
		var paramList = string.Join(", ", parameters.Select(p => $"{GetTypeName(p.ParameterType)} {p.Name}"));

		// Try to extract body from CallerArgumentExpression
		var body = ExtractLambdaBody(lambdaSource);

		return $"""
			{returnType} TestMethod({paramList})
			{body}
			""";
	}

	public static string ExtractLambdaBody(string? lambdaSource)
	{
		if (string.IsNullOrWhiteSpace(lambdaSource))
		{
			return "{\n\tthrow new NotImplementedException();\n}";
		}

		// Find the lambda body (after =>)
		var arrowIndex = lambdaSource.IndexOf("=>", StringComparison.Ordinal);

		if (arrowIndex < 0)
		{
			return "{\n\tthrow new NotImplementedException();\n}";
		}

		var body = lambdaSource.Substring(arrowIndex + 2).Trim();

		// If body starts with {, it's a statement lambda - use as-is
		if (body.StartsWith('{'))
		{
			return body;
		}

		// Expression lambda - wrap in return statement
		return "{\n\treturn " + body + ";\n}";
	}

	public static string GetTypeName(Type type)
	{
		if (type == typeof(void)) return "void";
		if (type == typeof(int)) return "int";
		if (type == typeof(string)) return "string";
		if (type == typeof(bool)) return "bool";
		if (type == typeof(double)) return "double";
		if (type == typeof(float)) return "float";
		if (type == typeof(long)) return "long";
		if (type == typeof(short)) return "short";
		if (type == typeof(byte)) return "byte";
		if (type == typeof(char)) return "char";
		if (type == typeof(decimal)) return "decimal";
		if (type == typeof(object)) return "object";

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
				return $"{string.Join(", ", args.Select(GetTypeName))}";
			}
			
			return $"({string.Join(", ", args.Select(GetTypeName))})";
		}

		// Handle generic types
		if (type.IsGenericType)
		{
			var genericName = type.Name.Substring(0, type.Name.IndexOf('`'));
			var genericArgs = string.Join(", ", type.GetGenericArguments().Select(GetTypeName));
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