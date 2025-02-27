using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using SGF;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Vectorize.Visitors;
using static Vectorize.Helpers.SyntaxHelpers;

namespace Vectorize;

#pragma warning disable RSEXPERIMENTAL002

[IncrementalGenerator]
public class VectorizeSourceGenerator() : IncrementalGenerator("Vectorize")
{
	public override void OnInitialize(SgfInitializationContext context)
	{
		context.RegisterPostInitializationOutput(spc =>
		{
			var invocations = context.SyntaxProvider
				.CreateSyntaxProvider(
					predicate: (node, _) => node is InvocationExpressionSyntax,
					transform: GenerateSource)
				.Where(result => result != null);

			context.RegisterSourceOutput(invocations.Collect(), (spc, models) =>
			{
				foreach (var group in models.GroupBy(model => model.Method, SyntaxNodeComparer<MethodDeclarationSyntax>.Instance))
				{
					GenerateMethodImplementations(spc, group);
				}
			});

			spc.AddSource("ConstExprAttribute.g", """
				using System;

				namespace ConstantExpression
				{
				    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
				    public sealed class ConstExprAttribute : Attribute
				    {
				    }
				}
				""");
		});
	}

	private void GenerateMethodImplementations(SgfSourceProductionContext spc, IGrouping<MethodDeclarationSyntax, InvocationModel> group)
	{
		var code = new StringBuilder();
		var usings = group.SelectMany(item => item.Usings).Distinct().OrderBy(s => s);

		foreach (var u in usings)
		{
			code.AppendLine(u);
		}

		code.AppendLine();
		code.AppendLine("namespace ConstantExpression.Generated");
		code.AppendLine("{");
		code.AppendLine("\tfile static class GeneratedMethods");
		code.AppendLine("\t{");

		foreach (var valueGroup in group.GroupBy(m => m.Value))
		{
			// Add interceptor attributes
			foreach (var item in valueGroup)
			{
				code.AppendLine($"\t\t[InterceptsLocation({item.Location.Version}, \"{item.Location.Data}\")]");
			}

			// Generate the method implementation
			var first = valueGroup.First();
			var methodCode = first.Method
				.WithIdentifier(SyntaxFactory.Identifier($"{first.Method.Identifier}_{first.Invocation.GetHashCode()}"))
				.WithAttributeLists(SyntaxFactory.List<AttributeListSyntax>())
				.WithExpressionBody(SyntaxFactory.ArrowExpressionClause(CreateLiteral(first.Value)))
				.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
				.WithBody(null)
				.NormalizeWhitespace("\t")
				.ToString()
				.Replace("\n", "\n\t\t");

			code.AppendLine("\t\t[MethodImpl(MethodImplOptions.AggressiveInlining)]");
			code.AppendLine(methodCode.Insert(0, "\t\t"));
		}

		code.AppendLine("\t}");
		code.AppendLine("}");
		code.AppendLine();

		// Add InterceptsLocationAttribute definition
		code.AppendLine("""
			namespace System.Runtime.CompilerServices
			{
			    [Conditional("DEBUG")]
			    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
			    file sealed class InterceptsLocationAttribute : Attribute
			    {
			        public InterceptsLocationAttribute(int version, string data)
			        {
			            _ = version;
			            _ = data;
			        }
			    }
			}
			""");

		if (group.Key.Parent is TypeDeclarationSyntax type)
		{
			spc.AddSource($"{type.Identifier}_{group.Key.Identifier}.g.cs", code.ToString());
		}
	}

	private InvocationModel? GenerateSource(GeneratorSyntaxContext context, CancellationToken token)
	{
		if (context.Node is not InvocationExpressionSyntax invocation ||
		    context.SemanticModel.GetSymbolInfo(invocation, token).Symbol is not IMethodSymbol { IsStatic: true } method)
		{
			return null;
		}

		// Check for ConstExprAttribute on type or method
		if ((method.ContainingType is ITypeSymbol type && type.GetAttributes().Any(IsConstExprAttribute)) ||
		    method.GetAttributes().Any(IsConstExprAttribute))
		{
			return GenerateExpression(context.SemanticModel.Compilation, invocation, method, token);
		}

		return null;
	}

	private InvocationModel? GenerateExpression(Compilation compilation, InvocationExpressionSyntax invocation,
	                                            IMethodSymbol methodSymbol, CancellationToken token)
	{
		if (IsInConstExprBody(invocation))
		{
			return null;
		}

		var methodDecl = GetMethodSyntaxNode(methodSymbol);

		if (methodDecl == null)
		{
			return null;
		}

		var variables = ProcessArguments(compilation, invocation, methodSymbol, token);

		if (variables == null)
		{
			return null;
		}

		if (TryGetOperation<IMethodBodyOperation>(compilation, methodDecl, out var blockOperation) &&
		    TryGetSemanticModel(compilation, invocation, out var model))
		{
			try
			{
				var timer = Stopwatch.StartNew();
				var visitor = new ConstExprOperationVisitor(compilation, token);
				visitor.VisitBlock(blockOperation.BlockBody!, variables);
				timer.Stop();

				Logger.Information($"{timer.Elapsed}: {invocation}");

				return new InvocationModel
				{
					Usings = GetUsings(methodSymbol),
					Method = methodDecl,
					Invocation = invocation,
					Value = variables[ConstExprOperationVisitor.ReturnVariableName],
					Location = model.GetInterceptableLocation(invocation, token)
				};
			}
			catch
			{
				return null;
			}
		}

		return null;
	}

	private Dictionary<string, object?>? ProcessArguments(Compilation compilation, InvocationExpressionSyntax invocation,
	                                                      IMethodSymbol methodSymbol, CancellationToken token)
	{
		var variables = new Dictionary<string, object?>();

		for (var i = 0; i < invocation.ArgumentList.Arguments.Count; i++)
		{
			var paramName = methodSymbol.Parameters[i].Name;

			if (methodSymbol.Parameters[i].IsParams)
			{
				var values = invocation.ArgumentList.Arguments
					.Skip(i)
					.Select(arg => GetConstantValue(compilation, arg.Expression, token))
					.ToArray();

				if (methodSymbol.Parameters[i].IsParamsArray)
				{
					var array = Array.CreateInstance(values[0].GetType(), values.Length);

					for (var j = 0; j < values.Length; j++)
					{
						array.SetValue(values[i], j);
					}
					variables[paramName] = array;
				}
				else
				{
					var listType = typeof(List<>).MakeGenericType(values[0].GetType());
					var list = Activator.CreateInstance(listType);

					if (list is IList listInstance)
					{
						foreach (var item in values)
						{
							listInstance.Add(item);
						}
					}
					variables[paramName] = list;
				}
				break;
			}

			var arg = invocation.ArgumentList.Arguments[i];

			if (!TryGetConstantValue(compilation, arg.Expression, token, out var value))
			{
				return null;
			}
			variables[paramName] = value;
		}

		return variables;
	}

	private static MethodDeclarationSyntax? GetMethodSyntaxNode(IMethodSymbol methodSymbol)
	{
		return methodSymbol.DeclaringSyntaxReferences
			.Select(s => s.GetSyntax())
			.OfType<MethodDeclarationSyntax>()
			.FirstOrDefault();
	}

	private static HashSet<string> GetUsings(IMethodSymbol methodSymbol)
	{
		var usings = new HashSet<string>
		{
			"using System.Diagnostics;",
			"using System.Runtime.CompilerServices;",
			$"using {methodSymbol.ReturnType.ContainingNamespace};"
		};

		foreach (var p in methodSymbol.Parameters)
		{
			usings.Add($"using {p.Type.ContainingNamespace};");
		}

		return usings;
	}
}
#pragma warning restore RSEXPERIMENTAL002