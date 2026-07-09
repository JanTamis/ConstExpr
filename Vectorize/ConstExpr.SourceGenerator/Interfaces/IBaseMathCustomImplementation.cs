using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Interfaces;

public interface IBaseMathCustomImplementation
{
	string GenerateCustomImplementation(FunctionOptimizerContext context, ITypeSymbol paramType);
}