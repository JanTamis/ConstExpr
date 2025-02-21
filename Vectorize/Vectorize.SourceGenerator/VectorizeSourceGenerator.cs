using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis.Operations;
using Vectorize.Visitors;
using SGF;
using static Vectorize.Helpers.SyntaxHelpers;
using System.Diagnostics;

namespace Vectorize
{
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
						(node, token) => node is InvocationExpressionSyntax,
						GenerateSource)
					.Where(result => result != null);

				context.RegisterSourceOutput(invocations.Collect(), (spc, models) =>
				{
					var groups = models.GroupBy(model => model.Method, SyntaxNodeComparer<MethodDeclarationSyntax>.Instance);

					foreach (var group in groups)
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
							foreach (var item in valueGroup)
							{
								code.AppendLine($"\t\t[InterceptsLocation({item.Location.Version}, \"{item.Location.Data}\")]");
							}

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
				});

				spc.AddSource("ConstExprAttribute.g", """
					using System;

					namespace ConstantExpression
					{
						[AttributeUsage(AttributeTargets.Method, Inherited = false)]
						public sealed class ConstExprAttribute : Attribute
						{
						}
					}
					""");
			});
		}

		private InvocationModel? GenerateSource(GeneratorSyntaxContext context, CancellationToken token)
		{
			if (context.Node is not InvocationExpressionSyntax invocation 
			    || context.SemanticModel.GetSymbolInfo(invocation, token).Symbol is not IMethodSymbol { IsStatic: true } method)
			{
				return null;
			}

			foreach (var attr in method.GetAttributes())
			{
				if (IsConstExprAttribute(attr))
				{
					return GenerateExpression(context.SemanticModel.Compilation, invocation, method, token);
				}
			}

			return null;
		}

		private InvocationModel? GenerateExpression(Compilation compilation,
		                                            InvocationExpressionSyntax invocation,
		                                            IMethodSymbol methodSymbol,
		                                            CancellationToken token)
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

			if (TryGetOperation<IMethodBodyOperation>(compilation, methodDecl, out var blockOperation))
			{
				try
				{
					var timer = Stopwatch.StartNew();

					var visitor = new ConstExprOperationVisitor(compilation, Logger);
					visitor.VisitBlock(blockOperation.BlockBody!, variables);

					timer.Stop();
					Logger.Information($"{timer.Elapsed}: {invocation}");
										
					return new InvocationModel
					{
						Usings = GetUsings(methodSymbol),
						Method = methodDecl,
						Invocation = invocation,
						Value = variables[ConstExprOperationVisitor.ReturnVariableName],
						Location = GetSemanticModel(compilation, invocation).GetInterceptableLocation(invocation, token)
					};
				}
				catch (Exception)
				{
					return null;
				}
			}

			return null;
		}

		private MethodDeclarationSyntax? GetMethodSyntaxNode(IMethodSymbol methodSymbol)
		{
			return methodSymbol.DeclaringSyntaxReferences
				.Select(s => s.GetSyntax())
				.OfType<MethodDeclarationSyntax>()
				.FirstOrDefault();
		}

		public static HashSet<string> GetUsings(IMethodSymbol methodSymbol)
		{
			var usings = new HashSet<string>
			{
				"using System.Diagnostics;",
				"using System;",
				"using System.Runtime.CompilerServices;"
			};

			if (methodSymbol.ContainingNamespace != null)
			{
				usings.Add($"using {methodSymbol.ContainingNamespace};");
			}

			var tree = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.SyntaxTree;

			if (tree != null)
			{
				var root = tree.GetRoot();
				
				foreach (var u in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
				{
					usings.Add(u.ToString());
				}
			}

			return usings;
		}
	}
#pragma warning restore RSEXPERIMENTAL00"2
}