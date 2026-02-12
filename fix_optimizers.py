#!/usr/bin/env python3
import re
import os
from pathlib import Path

# List of files that need to be updated based on build errors
files_to_update = [
    "LastFunctionOptimizer.cs",
    "LastOrDefaultFunctionOptimizer.cs",
    "MinByFunctionOptimizer.cs",
    "FirstOrDefaultFunctionOptimizer.cs",
    "MinFunctionOptimizer.cs",
    "DistinctByFunctionOptimizer.cs",
    "LongCountFunctionOptimizer.cs",
    "OrderByFunctionOptimizer.cs",
    "ReverseFunctionOptimizer.cs",
    "OrderDescendingFunctionOptimizer.cs",
    "OfTypeFunctionOptimizer.cs",
    "MaxByFunctionOptimizer.cs",
    "SelectFunctionOptimizer.cs",
    "OrderFunctionOptimizer.cs",
    "OrderByDescendingFunctionOptimizer.cs",
    "MaxFunctionOptimizer.cs",
    "PrependFunctionOptimizer.cs",
    "SingleOrDefaultFunctionOptimizer.cs",
    "SkipLastFunctionOptimizer.cs",
    "SelectManyFunctionOptimizer.cs",
    "TakeLastFunctionOptimizer.cs",
    "SkipFunctionOptimizer.cs",
    "SkipWhileFunctionOptimizer.cs",
    "SequenceEqualFunctionOptimizer.cs",
    "TakeWhileFunctionOptimizer.cs",
    "ToArrayFunctionOptimizer.cs",
    "SumFunctionOptimizer.cs",
    "ShuffleFunctionOptimizer.cs",
    "ThenByDescendingFunctionOptimizer.cs",
    "UnionFunctionOptimizer.cs",
    "TakeFunctionOptimizer.cs",
    "ToHashSetFunctionOptimizer.cs",
    "ThenByFunctionOptimizer.cs",
    "SingleFunctionOptimizer.cs",
    "WhereFunctionOptimizer.cs",
    "ToListFunctionOptimizer.cs",
    "ZipFunctionOptimizer.cs",
    "UnionByFunctionOptimizer.cs",
    "BitDecrementFunctionOptimizer.cs",
    "BitIncrementFunctionOptimizer.cs",
    "CastFunctionOptimizer.cs",
    "ChunkFunctionOptimizer.cs",
]

base_path = Path("/Users/jantamiskossen/RiderProjects/Vectorize/Vectorize/ConstExpr.SourceGenerator/Optimizers/FunctionOptimizers")

# Old signature pattern
old_signature_pattern = r'public override bool TryOptimize\(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, Func<SyntaxNode, ExpressionSyntax\?> visit, Func<LambdaExpressionSyntax, LambdaExpression\?> getLambda, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode\? result\)'

# New signature
new_signature = 'public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)'

# Mapping of old parameters to new context properties
replacements = [
    (r'\bmodel\b', 'context.Model'),
    (r'\bmethod\b', 'context.Method'),
    (r'\binvocation\b', 'context.Invocation'),
    (r'\bparameters\b', 'context.Parameters'),
    (r'\bvisit\b', 'context.Visit'),
    (r'\bgetLambda\b', 'context.GetLambda'),
    (r'\badditionalMethods\b', 'context.AdditionalMethods'),
]

def update_file(file_path):
    """Update a single file with the new signature."""
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Check if file needs updating
    if 'public override bool TryOptimize(FunctionOptimizerContext context' in content:
        print(f"Skipping {file_path.name} - already updated")
        return False
    
    if 'public override bool TryOptimize(SemanticModel' not in content:
        print(f"Skipping {file_path.name} - no matching signature found")
        return False
    
    # Replace the method signature
    content = re.sub(old_signature_pattern, new_signature, content)
    
    # Replace parameter references with context properties
    for old_param, new_param in replacements:
        content = re.sub(old_param, new_param, content)
    
    # Write back
    with open(file_path, 'w', encoding='utf-8') as f:
        f.write(content)
    
    print(f"Updated {file_path.name}")
    return True

def main():
    updated_count = 0
    
    for filename in files_to_update:
        # Search in both LinqOptimizers and MathOptimizers directories
        for subdir in ['LinqOptimizers', 'MathOptimizers']:
            file_path = base_path / subdir / filename
            if file_path.exists():
                if update_file(file_path):
                    updated_count += 1
                break
        else:
            print(f"Warning: Could not find {filename}")
    
    print(f"\nTotal files updated: {updated_count}")

if __name__ == "__main__":
    main()

