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
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Visitors;
using static ConstExpr.SourceGenerator.Helpers.SyntaxHelpers;

[assembly: InternalsVisibleTo("ConstExpr.Tests")]

namespace ConstExpr.SourceGenerator;

#pragma warning disable RSEXPERIMENTAL002

[IncrementalGenerator]
public partial class ConstExprSourceGenerator() : IncrementalGenerator("ConstExpr")
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
			
			context.RegisterSourceOutput(invocations.Collect().Combine(context.CompilationProvider), (spc, modelAndCompilation) =>
			{
				foreach (var group in modelAndCompilation.Left.GroupBy(model => model.Method, SyntaxNodeComparer<MethodDeclarationSyntax>.Instance))
				{
					GenerateMethodImplementations(spc, modelAndCompilation.Right, group);
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

			spc.AddSource("InterceptsLocationAttribute.g", """
				using System;
				using System.Diagnostics;

				namespace System.Runtime.CompilerServices
				{
					[Conditional("DEBUG")]
					[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
					internal sealed class InterceptsLocationAttribute : Attribute
					{
						public InterceptsLocationAttribute(int version, string data)
						{
							_ = version;
							_ = data;
						}
					}
				}
				""");
		});
	}

	private void GenerateMethodImplementations(SgfSourceProductionContext spc, Compilation compilation, IGrouping<MethodDeclarationSyntax, InvocationModel> group)
	{
		var code = new IndentedStringBuilder();
		var usings = group.SelectMany(item => item.Usings).Distinct().OrderBy(s => s);

		foreach (var u in usings)
		{
			code.AppendLine(u);
		}

		code.AppendLine();
		code.AppendLine("namespace ConstantExpression.Generated;");
		code.AppendLine();
		// code.AppendLine("{");
		code.AppendLine("file static class GeneratedMethods");
		code.AppendLine("{");

		foreach (var valueGroup in group.GroupBy(m => m.Value))
		{
			// Add interceptor attributes
			foreach (var item in valueGroup)
			{
				code.AppendLine($"\t[InterceptsLocation({item.Location.Version}, \"{item.Location.Data}\")]");
			}

			// Generate the method implementation
			var first = valueGroup.First();

			var method = first.Method
				.WithIdentifier(SyntaxFactory.Identifier($"{first.Method.Identifier}_{first.Invocation.GetHashCode()}"))
				.WithAttributeLists(SyntaxFactory.List<AttributeListSyntax>());

			if (IsIEnumerable(compilation, first.Method.ReturnType) && first.Value is IEnumerable enumerable)
			{
				var body = SyntaxFactory.Block(
					SyntaxFactory.ReturnStatement(
						SyntaxFactory.ObjectCreationExpression(
							SyntaxFactory.ParseTypeName($"Enumerable_{enumerable.GetHashCode()}"), 
							SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList<ArgumentSyntax>([SyntaxFactory.Argument(CreateLiteral(-2))])), null)));
					
				method = method
					.WithBody(body)
					.WithExpressionBody(null);
			}
			else
			{
				method = method
					.WithBody(SyntaxFactory.Block(SyntaxFactory.ReturnStatement(CreateLiteral(first.Value))))
					.WithExpressionBody(null);
			}
			
			var methodCode = method
				.NormalizeWhitespace("\t")
				.ToString()
				.Replace("\n", "\n\t");

			code.AppendLine("\t[MethodImpl(MethodImplOptions.AggressiveInlining)]");
			code.AppendLine(methodCode.Insert(0, "\t"));
		}

		code.AppendLine("}");

		foreach (var invocation in group.Distinct())
		{
			if (IsIEnumerable(compilation, invocation.Method.ReturnType) && invocation.Value is IEnumerable enumerable)
			{
				SyntaxHelpers.BuildEnumerable(enumerable, compilation.GetSemanticModel(invocation.Method.SyntaxTree).GetTypeInfo(invocation.Method.ReturnType).Type, code);	
			}
		}

		if (group.Key.Parent is TypeDeclarationSyntax type)
		{
			spc.AddSource($"{type.Identifier}_{group.Key.Identifier}.g.cs", code.ToString());
		}
	}

	private InvocationModel? GenerateSource(GeneratorSyntaxContext context, CancellationToken token)
	{
		if (context.Node is not InvocationExpressionSyntax invocation 
		    || !TryGetSymbol(context.SemanticModel, invocation, token, out var method))
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
			catch (Exception e)
			{
				Logger.Error(e, $"Error processing {invocation}: {e.Message}");
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
						array.SetValue(values[j], j);
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
			"using System.Collections.Generic;",
			"using System.Collections;",
			"using System;",
			$"using {methodSymbol.ReturnType.ContainingNamespace};"
		};

		foreach (var p in methodSymbol.Parameters)
		{
			usings.Add($"using {p.Type.ContainingNamespace};");
		}

		return usings;
	}

	private static bool IsIEnumerable(Compilation compilation, TypeSyntax typeSymbol)
	{
		var type = compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1");

		if (type == null)
		{
			return false;
		}

		return typeSymbol is GenericNameSyntax genericName &&
		       genericName.Identifier.Text == type.Name &&
		       genericName.TypeArgumentList.Arguments.Count == 1;
	}

	private static bool TryGetSymbol(SemanticModel semanticModel, InvocationExpressionSyntax invocation, CancellationToken token, out IMethodSymbol symbol)
	{
		if (semanticModel.GetSymbolInfo(invocation, token).Symbol is IMethodSymbol s)
		{
			symbol = s;
			return true;
		}

		var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
		var symbols = semanticModel.LookupSymbols(invocation.SpanStart, semanticModel.GetEnclosingSymbol(invocation.SpanStart)?.ContainingType);
		
		foreach (var item in symbols)
		{
			if (item is IMethodSymbol { IsStatic: true } methodSymbol && methodSymbol.Name == memberAccess?.Name.ToString())
			{
				symbol = methodSymbol;
				return true;
			}
		}
		
		symbol = null;
		return false;
	}
}
#pragma warning restore RSEXPERIMENTAL002