#!/bin/bash

# Script to update all optimizer files to use FunctionOptimizerContext

# Find all optimizer files that still have the old signature
grep -r "TryOptimize(SemanticModel model, IMethodSymbol method" \
  Vectorize/ConstExpr.SourceGenerator/Optimizers/FunctionOptimizers \
  --include="*.cs" \
  -l

