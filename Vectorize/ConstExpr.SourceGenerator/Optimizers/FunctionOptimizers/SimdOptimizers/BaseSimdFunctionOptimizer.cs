using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.SimdOptimizers;

public abstract class BaseSimdFunctionOptimizer(string typeName, string platform = "X86") : BaseFunctionOptimizer
{
	public abstract bool TryOptimizeSimd(FunctionOptimizerContext context, INamedTypeSymbol vectorType, [NotNullWhen(true)] out SyntaxNode? result);

	public override bool TryOptimize(FunctionOptimizerContext context, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var returnType = context.Method.ReturnType;

		if (context.Method.ContainingType?.ToString() != $"System.Runtime.Intrinsics.{platform}.{typeName}"
		    || returnType is not INamedTypeSymbol { Arity: 1 } namedReturnType)
		{
			result = null;
			return false;
		}

		if (TryOptimizeSimd(context, namedReturnType, out result))
		{
			return true;
		}

		var methodSymbol = context.Model.Compilation
			.GetTypeByMetadataName($"System.Runtime.Intrinsics.{returnType.Name}")?
			.GetMembers(context.Method.Name)
			.OfType<IMethodSymbol>()
			.Where(m => m.Arity == context.Method.Arity && m.Parameters.Length == context.Method.Parameters.Length)
			.Select(s => s.Construct(namedReturnType.TypeArguments[0]))
			.FirstOrDefault(f => SymbolEqualityComparer.Default.Equals(f.ReturnType, namedReturnType));

		if (methodSymbol == null)
		{
			result = null;
			return false;
		}

		var invocation = CreateInvocation(methodSymbol.ContainingType, methodSymbol.Name, context.VisitedParameters)
			.WithMethodSymbolAnnotation(methodSymbol, context.SymbolStore);

		result = invocation;
		return true;
	}
	
	protected InvocationExpressionSyntax CreateSimdInvocation(FunctionOptimizerContext context, INamedTypeSymbol vectorType, string methodName, params IEnumerable<ExpressionSyntax> arguments)
	{
		var staticVectorType = context.Model.Compilation.GetTypeByMetadataName($"System.Runtime.Intrinsics.{vectorType.Name}");
		
		var methodSymbol = staticVectorType?
			.GetMembers(context.Method.Name)
			.OfType<IMethodSymbol>()
			.Where(m => m.Arity == context.Method.Arity && m.Parameters.Length == context.Method.Parameters.Length)
			.Select(s => s.Construct(vectorType.TypeArguments[0]))
			.FirstOrDefault(f => SymbolEqualityComparer.Default.Equals(f.ReturnType, vectorType));
		
		return CreateInvocation(staticVectorType, methodName, arguments)
			.WithMethodSymbolAnnotation(methodSymbol, context.SymbolStore);
	}
}