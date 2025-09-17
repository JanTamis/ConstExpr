using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ConstExpr.SourceGenerator.Helpers;

BenchmarkRunner.Run(new[] { typeof(MetadataLoaderBenchmarks), typeof(SemanticInfoBenchmarks) });

public class MetadataLoaderBenchmarks
{
    private Compilation _compilation = default!;

    [GlobalSetup]
    public void Setup()
    {
        var code = """
        using System;
        public static class A { public static int X => 42; }
        """;
        _compilation = CSharpCompilation.Create("bench",
            new[] { CSharpSyntaxTree.ParseText(code) },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    [Benchmark]
    public object CreateLoader()
    {
        return MetadataLoader.GetLoader(_compilation);
    }
}

public class SemanticInfoBenchmarks
{
    private Compilation _compilation = default!;
    private SemanticModel _model = default!;
    private Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax[] _calls = default!;

    [GlobalSetup]
    public void Setup()
    {
        var code = """
        using System; using System.Linq; 
        public static class G
        {
            [ConstExpr.Core.Attributes.ConstExpr]
            public static int F(int a, int b) => a + b;
            public static void Use()
            {
                _ = F(1,2); _ = F(2,3); _ = F(3,4); _ = F(4,5); _ = F(5,6);
                _ = F(6,7); _ = F(7,8); _ = F(8,9); _ = F(9,10); _ = F(10,11);
            }
        }
        """;
        _compilation = CSharpCompilation.Create("bench2",
            new[] { CSharpSyntaxTree.ParseText(code) },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location), MetadataReference.CreateFromFile(typeof(ConstExpr.SourceGenerator.ConstExprSourceGenerator).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        _model = _compilation.GetSemanticModel(_compilation.SyntaxTrees[0]);
        _calls = _compilation.SyntaxTrees[0].GetRoot()
            .DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>().ToArray();
    }

    [Benchmark]
    public int GetSymbolInfoCalls()
    {
        int found = 0;
        foreach (var call in _calls)
        {
            var info = Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetSymbolInfo(_model, call);
            if (info.Symbol != null) found++;
        }
        return found;
    }
}
