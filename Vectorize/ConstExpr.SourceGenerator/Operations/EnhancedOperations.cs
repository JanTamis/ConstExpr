using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ConstExpr.SourceGenerator.Operations;

// Enhanced Pattern Matching Operations
public interface IListPatternOperation : IPatternOperation
{
    IPatternOperation[] Patterns { get; }
}

public interface IPropertyPatternOperation : IPatternOperation
{
    ISymbol Member { get; }
    IPatternOperation Pattern { get; }
}

public interface ITuplePatternOperation : IPatternOperation
{
    IPatternOperation[] Patterns { get; }
}

public interface IRecursivePatternOperation : IPatternOperation
{
    ITypeSymbol? MatchedType { get; }
    IPropertyPatternOperation[]? PropertySubpatterns { get; }
    ISymbol? DeclaredSymbol { get; }
}

// Advanced LINQ Operations
public interface IGroupByOperation : IOperation
{
    IOperation Source { get; }
    IOperation KeySelector { get; }
    IOperation? ElementSelector { get; }
}

public interface IJoinOperation : IOperation
{
    IOperation Outer { get; }
    IOperation Inner { get; }
    IOperation OuterKeySelector { get; }
    IOperation InnerKeySelector { get; }
    IOperation ResultSelector { get; }
}

// Mathematical Extensions
public interface IComplexNumberOperation : IOperation
{
    IOperation Real { get; }
    IOperation Imaginary { get; }
}

public interface IStatisticalOperation : IOperation
{
    IOperation Source { get; }
    StatisticalFunction Function { get; }
    double? Percentile { get; }
}

public enum StatisticalFunction
{
    Median,
    Mode,
    StandardDeviation,
    Variance,
    Percentile
}

// Advanced Collection Operations
public interface IDictionaryOperation : IOperation
{
    IOperation Dictionary { get; }
    DictionaryOperationType OperationType { get; }
    IOperation? Key { get; }
}

public enum DictionaryOperationType
{
    Keys,
    Values,
    Count,
    ContainsKey
}

// Advanced String Processing
public interface IRegexOperation : IOperation
{
    IOperation Input { get; }
    IOperation Pattern { get; }
    RegexOperationType OperationType { get; }
    RegexOptions Options { get; }
    string? Replacement { get; }
}

public enum RegexOperationType
{
    IsMatch,
    Match,
    Matches,
    Replace,
    Split
}

public interface ICultureStringOperation : IOperation
{
    IOperation Input { get; }
    CultureStringOperationType OperationType { get; }
    CultureInfo? Culture { get; }
    string? CompareWith { get; }
    CompareOptions CompareOptions { get; }
}

public enum CultureStringOperationType
{
    ToUpper,
    ToLower,
    Compare,
    StartsWith,
    EndsWith
}

// Performance Optimization
public interface IVectorizedOperation : IOperation
{
    IOperation Source { get; }
    VectorType VectorType { get; }
    VectorOperation VectorOperation { get; }
}

public enum VectorType
{
    Vector64,
    Vector128,
    Vector256
}

public enum VectorOperation
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Dot,
    CrossProduct,
    Magnitude
}