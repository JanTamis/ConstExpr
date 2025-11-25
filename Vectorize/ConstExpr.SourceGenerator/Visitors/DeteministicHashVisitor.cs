using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;

namespace ConstExpr.SourceGenerator.Visitors;

public class DeteministicHashVisitor : CSharpSyntaxVisitor<ulong>
{
  public static DeteministicHashVisitor Instance = new();

  // FNV-1a constants for 64-bit
  private const ulong FnvOffsetBasis = 14695981039346656037UL;
  private const ulong FnvPrime = 1099511628211UL;

  public override ulong Visit(SyntaxNode? node)
  {
    if (node is null)
    {
      return FnvOffsetBasis;
    }

    var hash = HashCombine(FnvOffsetBasis, (ulong)node.RawKind);

    // Recursively hash all children
    foreach (var child in node.ChildNodesAndTokens())
    {
      if (child.IsNode)
      {
        hash = HashCombine(hash, Visit(child.AsNode()));
      }
      else if (child.IsToken)
      {
        var token = child.AsToken();

        if (token.ValueText != null && token.ValueText.Length > 0)
        {
          hash = HashCombine(hash, HashString(token.ValueText));
        }
      }
    }

    return hash;
  }

  public override ulong VisitIdentifierName(IdentifierNameSyntax node)
  {
    // Include node RawKind to differentiate from pure string hashes
    return HashCombine((ulong)node.RawKind, HashString(node.Identifier.ValueText));
  }

  public override ulong VisitBlock(BlockSyntax node)
  {
    var hash = FnvOffsetBasis;

    foreach (var statement in node.Statements)
    {
      hash = HashCombine(hash, Visit(statement));
    }

    return hash;
  }

  public override ulong VisitLiteralExpression(LiteralExpressionSyntax node)
  {
    return HashString(node.Token.ValueText);
  }

  public override ulong VisitBinaryExpression(BinaryExpressionSyntax node)
  {
    return HashCombine(Visit(node.Left), Visit(node.Right));
  }

  public override ulong VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
  {
    return Visit(node.Operand);
  }

  public override ulong VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node)
  {
    return Visit(node.Operand);
  }

  public override ulong VisitInvocationExpression(InvocationExpressionSyntax node)
  {
    return node.ArgumentList != null
      ? HashCombine(Visit(node.Expression), Visit(node.ArgumentList))
      : Visit(node.Expression);
  }

  public override ulong VisitArgumentList(ArgumentListSyntax node)
  {
    var hash = FnvOffsetBasis;
    
    foreach (var argument in node.Arguments)
    {
      hash = HashCombine(hash, Visit(argument));
    }
    
    return hash;
  }

  public override ulong VisitArgument(ArgumentSyntax node)
  {
    return node.NameColon != null
      ? HashCombine(HashString(node.NameColon.Name.Identifier.ValueText), Visit(node.Expression))
      : Visit(node.Expression);
  }

  public override ulong VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
  {
    return HashCombine(Visit(node.Expression), Visit(node.Name));
  }

  public override ulong VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
  {
    return Visit(node.Declaration);
  }

  public override ulong VisitVariableDeclaration(VariableDeclarationSyntax node)
  {
    var hash = Visit(node.Type);
    
    foreach (var variable in node.Variables)
    {
      hash = HashCombine(hash, Visit(variable));
    }
    
    return hash;
  }

  public override ulong VisitVariableDeclarator(VariableDeclaratorSyntax node)
  {
    return node.Initializer != null
      ? HashCombine(HashString(node.Identifier.ValueText), Visit(node.Initializer))
      : HashString(node.Identifier.ValueText);
  }

  public override ulong VisitEqualsValueClause(EqualsValueClauseSyntax node)
  {
    return Visit(node.Value);
  }

  public override ulong VisitReturnStatement(ReturnStatementSyntax node)
  {
    return node.Expression != null
      ? Visit(node.Expression)
      : 0;
  }

  public override ulong VisitIfStatement(IfStatementSyntax node)
  {
    return node.Else != null
      ? HashCombine(Visit(node.Condition), Visit(node.Statement), Visit(node.Else))
      : HashCombine(Visit(node.Condition), Visit(node.Statement));
  }

  public override ulong VisitElseClause(ElseClauseSyntax node)
  {
    return Visit(node.Statement);
  }

  public override ulong VisitForStatement(ForStatementSyntax node)
  {
    var hash = FnvOffsetBasis;
    
    if (node.Declaration != null)
    {
      hash = HashCombine(hash, Visit(node.Declaration));
    }
    
    foreach (var initializer in node.Initializers)
    {
      hash = HashCombine(hash, Visit(initializer));
    }
    
    if (node.Condition != null)
    {
      hash = HashCombine(hash, Visit(node.Condition));
    }
    
    foreach (var incrementor in node.Incrementors)
    {
      hash = HashCombine(hash, Visit(incrementor));
    }
    
    hash = HashCombine(hash, Visit(node.Statement));
    return hash;
  }

  public override ulong VisitWhileStatement(WhileStatementSyntax node)
  {
    return HashCombine(Visit(node.Condition), Visit(node.Statement));
  }

  public override ulong VisitDoStatement(DoStatementSyntax node)
  {
    return HashCombine(Visit(node.Statement), Visit(node.Condition));
  }

  public override ulong VisitForEachStatement(ForEachStatementSyntax node)
  {
    return HashCombine(Visit(node.Type), HashString(node.Identifier.ValueText), 
      Visit(node.Expression), Visit(node.Statement));
  }

  public override ulong VisitSwitchStatement(SwitchStatementSyntax node)
  {
    var hash = Visit(node.Expression);
    
    foreach (var section in node.Sections)
    {
      hash = HashCombine(hash, Visit(section));
    }
    
    return hash;
  }

  public override ulong VisitSwitchSection(SwitchSectionSyntax node)
  {
    var hash = FnvOffsetBasis;
    
    foreach (var label in node.Labels)
    {
      hash = HashCombine(hash, Visit(label));
    }
    
    foreach (var statement in node.Statements)
    {
      hash = HashCombine(hash, Visit(statement));
    }
    
    return hash;
  }

  public override ulong VisitCaseSwitchLabel(CaseSwitchLabelSyntax node)
  {
    return Visit(node.Value);
  }

  public override ulong VisitDefaultSwitchLabel(DefaultSwitchLabelSyntax node)
  {
    return 0;
  }

  public override ulong VisitAssignmentExpression(AssignmentExpressionSyntax node)
  {
    return HashCombine(Visit(node.Left), Visit(node.Right));
  }

  public override ulong VisitParenthesizedExpression(ParenthesizedExpressionSyntax node)
  {
    return Visit(node.Expression);
  }

  public override ulong VisitCastExpression(CastExpressionSyntax node)
  {
    return HashCombine(Visit(node.Type), Visit(node.Expression));
  }

  public override ulong VisitConditionalExpression(ConditionalExpressionSyntax node)
  {
    return HashCombine(Visit(node.Condition), Visit(node.WhenTrue), Visit(node.WhenFalse));
  }

  public override ulong VisitArrayCreationExpression(ArrayCreationExpressionSyntax node)
  {
    return node.Initializer != null
      ? HashCombine(Visit(node.Type), Visit(node.Initializer))
      : Visit(node.Type);
  }

  public override ulong VisitInitializerExpression(InitializerExpressionSyntax node)
  {
    var hash = FnvOffsetBasis;
    
    foreach (var expression in node.Expressions)
    {
      hash = HashCombine(hash, Visit(expression));
    }
    
    return hash;
  }

  public override ulong VisitElementAccessExpression(ElementAccessExpressionSyntax node)
  {
    return HashCombine(Visit(node.Expression), Visit(node.ArgumentList));
  }

  public override ulong VisitBracketedArgumentList(BracketedArgumentListSyntax node)
  {
    var hash = FnvOffsetBasis;
    
    foreach (var argument in node.Arguments)
    {
      hash = HashCombine(hash, Visit(argument));
    }
    
    return hash;
  }

  public override ulong VisitPredefinedType(PredefinedTypeSyntax node)
  {
    return HashString(node.Keyword.ValueText);
  }

  public override ulong VisitArrayType(ArrayTypeSyntax node)
  {
    var hash = Visit(node.ElementType);
    
    foreach (var rankSpecifier in node.RankSpecifiers)
    {
      hash = HashCombine(hash, Visit(rankSpecifier));
    }
    
    return hash;
  }

  public override ulong VisitArrayRankSpecifier(ArrayRankSpecifierSyntax node)
  {
    var hash = HashCombine(FnvOffsetBasis, (ulong)node.Rank);
    
    foreach (var size in node.Sizes)
    {
      hash = HashCombine(hash, Visit(size));
    }
    
    return hash;
  }

  public override ulong VisitExpressionStatement(ExpressionStatementSyntax node)
  {
    return Visit(node.Expression);
  }

  public override ulong VisitBreakStatement(BreakStatementSyntax node)
  {
    return 0;
  }

  public override ulong VisitContinueStatement(ContinueStatementSyntax node)
  {
    return 0;
  }

  public override ulong VisitThrowStatement(ThrowStatementSyntax node)
  {
    return node.Expression != null
      ? Visit(node.Expression)
      : 0;
  }

  public override ulong VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
  {
    var hash = Visit(node.Type);
    
    if (node.ArgumentList != null)
    {
      hash = HashCombine(hash, Visit(node.ArgumentList));
    }
    
    if (node.Initializer != null)
    {
      hash = HashCombine(hash, Visit(node.Initializer));
    }
    
    return hash;
  }

  public override ulong VisitGenericName(GenericNameSyntax node)
  {
    return HashCombine(HashString(node.Identifier.ValueText), Visit(node.TypeArgumentList));
  }

  public override ulong VisitTypeArgumentList(TypeArgumentListSyntax node)
  {
    var hash = FnvOffsetBasis;
    
    foreach (var argument in node.Arguments)
    {
      hash = HashCombine(hash, Visit(argument));
    }
    
    return hash;
  }

  public override ulong DefaultVisit(SyntaxNode node)
  {
    return (ulong)node.RawKind;
  }

  public override ulong VisitQualifiedName(QualifiedNameSyntax node)
  {
    return HashCombine(Visit(node.Left), Visit(node.Right));
  }

  public override ulong VisitAliasQualifiedName(AliasQualifiedNameSyntax node)
  {
    return HashCombine(Visit(node.Alias), Visit(node.Name));
  }

  public override ulong VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
  {
    return HashCombine(HashString(node.Parameter.Identifier.ValueText), Visit(node.Body));
  }

  public override ulong VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
  {
    return HashCombine(Visit(node.ParameterList), Visit(node.Body));
  }

  public override ulong VisitParameterList(ParameterListSyntax node)
  {
    var hash = FnvOffsetBasis;
    
    foreach (var parameter in node.Parameters)
    {
      hash = HashCombine(hash, Visit(parameter));
    }
    
    return hash;
  }

  public override ulong VisitParameter(ParameterSyntax node)
  {
    var hash = node.Type != null
      ? HashCombine(Visit(node.Type), HashString(node.Identifier.ValueText))
      : HashString(node.Identifier.ValueText);
    
    if (node.Default != null)
    {
      hash = HashCombine(hash, Visit(node.Default));
    }
    
    return hash;
  }

  public override ulong VisitTryStatement(TryStatementSyntax node)
  {
    var hash = Visit(node.Block);
    
    foreach (var catchClause in node.Catches)
    {
      hash = HashCombine(hash, Visit(catchClause));
    }
    
    if (node.Finally != null)
    {
      hash = HashCombine(hash, Visit(node.Finally));
    }
    
    return hash;
  }

  public override ulong VisitCatchClause(CatchClauseSyntax node)
  {
    var hash = Visit(node.Block);
    
    if (node.Declaration != null)
    {
      hash = HashCombine(hash, Visit(node.Declaration));
    }
    
    if (node.Filter != null)
    {
      hash = HashCombine(hash, Visit(node.Filter));
    }
    
    return hash;
  }

  public override ulong VisitCatchDeclaration(CatchDeclarationSyntax node)
  {
    return HashCombine(Visit(node.Type), HashString(node.Identifier.ValueText));
  }

  public override ulong VisitCatchFilterClause(CatchFilterClauseSyntax node)
  {
    return Visit(node.FilterExpression);
  }

  public override ulong VisitFinallyClause(FinallyClauseSyntax node)
  {
    return Visit(node.Block);
  }

  public override ulong VisitUsingStatement(UsingStatementSyntax node)
  {
    var hash = Visit(node.Statement);
    
    if (node.Declaration != null)
    {
      hash = HashCombine(hash, Visit(node.Declaration));
    }
    
    if (node.Expression != null)
    {
      hash = HashCombine(hash, Visit(node.Expression));
    }
    
    return hash;
  }

  public override ulong VisitLockStatement(LockStatementSyntax node)
  {
    return HashCombine(Visit(node.Expression), Visit(node.Statement));
  }

  public override ulong VisitYieldStatement(YieldStatementSyntax node)
  {
    return node.Expression != null
      ? Visit(node.Expression)
      : 0;
  }

  public override ulong VisitAwaitExpression(AwaitExpressionSyntax node)
  {
    return Visit(node.Expression);
  }

  public override ulong VisitAnonymousObjectCreationExpression(AnonymousObjectCreationExpressionSyntax node)
  {
    var hash = FnvOffsetBasis;
    
    foreach (var initializer in node.Initializers)
    {
      hash = HashCombine(hash, Visit(initializer));
    }
    
    return hash;
  }

  public override ulong VisitAnonymousObjectMemberDeclarator(AnonymousObjectMemberDeclaratorSyntax node)
  {
    return node.NameEquals != null
      ? HashCombine(Visit(node.NameEquals), Visit(node.Expression))
      : Visit(node.Expression);
  }

  public override ulong VisitNameEquals(NameEqualsSyntax node)
  {
    return Visit(node.Name);
  }

  public override ulong VisitThisExpression(ThisExpressionSyntax node)
  {
    return 0;
  }

  public override ulong VisitBaseExpression(BaseExpressionSyntax node)
  {
    return 0;
  }

  public override ulong VisitDefaultExpression(DefaultExpressionSyntax node)
  {
    return node.Type != null
      ? Visit(node.Type)
      : 0;
  }

  public override ulong VisitTypeOfExpression(TypeOfExpressionSyntax node)
  {
    return Visit(node.Type);
  }

  public override ulong VisitSizeOfExpression(SizeOfExpressionSyntax node)
  {
    return Visit(node.Type);
  }

  public override ulong VisitCheckedExpression(CheckedExpressionSyntax node)
  {
    return Visit(node.Expression);
  }

  public override ulong VisitCheckedStatement(CheckedStatementSyntax node)
  {
    return Visit(node.Block);
  }

  public override ulong VisitIsPatternExpression(IsPatternExpressionSyntax node)
  {
    return HashCombine(Visit(node.Expression), Visit(node.Pattern));
  }

  public override ulong VisitConstantPattern(ConstantPatternSyntax node)
  {
    return Visit(node.Expression);
  }

  public override ulong VisitDeclarationPattern(DeclarationPatternSyntax node)
  {
    return HashCombine(Visit(node.Type), Visit(node.Designation));
  }

  public override ulong VisitSingleVariableDesignation(SingleVariableDesignationSyntax node)
  {
    return HashString(node.Identifier.ValueText);
  }

  public override ulong VisitDiscardDesignation(DiscardDesignationSyntax node)
  {
    return 0;
  }

  public override ulong VisitTupleExpression(TupleExpressionSyntax node)
  {
    var hash = FnvOffsetBasis;
    
    foreach (var argument in node.Arguments)
    {
      hash = HashCombine(hash, Visit(argument));
    }
    
    return hash;
  }

  public override ulong VisitTupleType(TupleTypeSyntax node)
  {
    var hash = FnvOffsetBasis;
    
    foreach (var element in node.Elements)
    {
      hash = HashCombine(hash, Visit(element));
    }
    
    return hash;
  }

  public override ulong VisitTupleElement(TupleElementSyntax node)
  {
    return HashCombine(Visit(node.Type), HashString(node.Identifier.ValueText));
  }

  public override ulong VisitInterpolatedStringExpression(InterpolatedStringExpressionSyntax node)
  {
    var hash = FnvOffsetBasis;
    
    foreach (var content in node.Contents)
    {
      hash = HashCombine(hash, Visit(content));
    }
    
    return hash;
  }

  public override ulong VisitInterpolatedStringText(InterpolatedStringTextSyntax node)
  {
    return HashString(node.TextToken.ValueText);
  }

  public override ulong VisitInterpolation(InterpolationSyntax node)
  {
    var hash = Visit(node.Expression);
    
    if (node.AlignmentClause != null)
    {
      hash = HashCombine(hash, Visit(node.AlignmentClause));
    }
    
    if (node.FormatClause != null)
    {
      hash = HashCombine(hash, Visit(node.FormatClause));
    }
    
    return hash;
  }

  public override ulong VisitInterpolationAlignmentClause(InterpolationAlignmentClauseSyntax node)
  {
    return Visit(node.Value);
  }

  public override ulong VisitInterpolationFormatClause(InterpolationFormatClauseSyntax node)
  {
    return HashString(node.FormatStringToken.ValueText);
  }

  public override ulong VisitRefExpression(RefExpressionSyntax node)
  {
    return Visit(node.Expression);
  }

  public override ulong VisitRefType(RefTypeSyntax node)
  {
    return Visit(node.Type);
  }

  public override ulong VisitNullableType(NullableTypeSyntax node)
  {
    return Visit(node.ElementType);
  }

  public override ulong VisitPointerType(PointerTypeSyntax node)
  {
    return Visit(node.ElementType);
  }

  public override ulong VisitOmittedArraySizeExpression(OmittedArraySizeExpressionSyntax node)
  {
    return 0;
  }

  public override ulong VisitStackAllocArrayCreationExpression(StackAllocArrayCreationExpressionSyntax node)
  {
    return node.Initializer != null
      ? HashCombine(Visit(node.Type), Visit(node.Initializer))
      : Visit(node.Type);
  }

  public override ulong VisitImplicitArrayCreationExpression(ImplicitArrayCreationExpressionSyntax node)
  {
    return Visit(node.Initializer);
  }

  public override ulong VisitQueryExpression(QueryExpressionSyntax node)
  {
    return HashCombine(Visit(node.FromClause), Visit(node.Body));
  }

  public override ulong VisitFromClause(FromClauseSyntax node)
  {
    var hash = HashCombine(HashString(node.Identifier.ValueText), Visit(node.Expression));
    
    if (node.Type != null)
    {
      hash = HashCombine(hash, Visit(node.Type));
    }
    
    return hash;
  }

  public override ulong VisitQueryBody(QueryBodySyntax node)
  {
    var hash = FnvOffsetBasis;
    
    foreach (var clause in node.Clauses)
    {
      hash = HashCombine(hash, Visit(clause));
    }
    
    hash = HashCombine(hash, Visit(node.SelectOrGroup));
    
    if (node.Continuation != null)
    {
      hash = HashCombine(hash, Visit(node.Continuation));
    }
    
    return hash;
  }

  public override ulong VisitSelectClause(SelectClauseSyntax node)
  {
    return Visit(node.Expression);
  }

  public override ulong VisitWhereClause(WhereClauseSyntax node)
  {
    return Visit(node.Condition);
  }

  public override ulong VisitOrderByClause(OrderByClauseSyntax node)
  {
    var hash = FnvOffsetBasis;
    
    foreach (var ordering in node.Orderings)
    {
      hash = HashCombine(hash, Visit(ordering));
    }
    
    return hash;
  }

  public override ulong VisitOrdering(OrderingSyntax node)
  {
    return Visit(node.Expression);
  }

  public override ulong VisitJoinClause(JoinClauseSyntax node)
  {
    var hash = HashCombine(HashString(node.Identifier.ValueText), 
      Visit(node.InExpression), Visit(node.LeftExpression), Visit(node.RightExpression));
    
    if (node.Type != null)
    {
      hash = HashCombine(hash, Visit(node.Type));
    }
    
    if (node.Into != null)
    {
      hash = HashCombine(hash, Visit(node.Into));
    }
    
    return hash;
  }

  public override ulong VisitGroupClause(GroupClauseSyntax node)
  {
    return HashCombine(Visit(node.GroupExpression), Visit(node.ByExpression));
  }

  public override ulong VisitLetClause(LetClauseSyntax node)
  {
    return HashCombine(HashString(node.Identifier.ValueText), Visit(node.Expression));
  }

  public override ulong VisitSwitchExpression(SwitchExpressionSyntax node)
  {
    var hash = Visit(node.GoverningExpression);
    
    foreach (var arm in node.Arms)
    {
      hash = HashCombine(hash, Visit(arm));
    }
    
    return hash;
  }

  public override ulong VisitSwitchExpressionArm(SwitchExpressionArmSyntax node)
  {
    var hash = HashCombine(Visit(node.Pattern), Visit(node.Expression));
    
    if (node.WhenClause != null)
    {
      hash = HashCombine(hash, Visit(node.WhenClause));
    }
    
    return hash;
  }

  public override ulong VisitWhenClause(WhenClauseSyntax node)
  {
    return Visit(node.Condition);
  }

  public override ulong VisitRangeExpression(RangeExpressionSyntax node)
  {
    var hash = FnvOffsetBasis;
    
    if (node.LeftOperand != null)
    {
      hash = HashCombine(hash, Visit(node.LeftOperand));
    }
    
    if (node.RightOperand != null)
    {
      hash = HashCombine(hash, Visit(node.RightOperand));
    }
    
    return hash;
  }

  public override ulong VisitImplicitObjectCreationExpression(ImplicitObjectCreationExpressionSyntax node)
  {
    return node.Initializer != null
      ? HashCombine(Visit(node.ArgumentList), Visit(node.Initializer))
      : Visit(node.ArgumentList);
  }

  public override ulong VisitWithExpression(WithExpressionSyntax node)
  {
    return HashCombine(Visit(node.Expression), Visit(node.Initializer));
  }

  public override ulong VisitGotoStatement(GotoStatementSyntax node)
  {
    return node.Expression != null
      ? Visit(node.Expression)
      : 0;
  }

  public override ulong VisitLabeledStatement(LabeledStatementSyntax node)
  {
    return HashCombine(HashString(node.Identifier.ValueText), Visit(node.Statement));
  }

  public override ulong VisitEmptyStatement(EmptyStatementSyntax node)
  {
    return 0;
  }

  public override ulong VisitUnsafeStatement(UnsafeStatementSyntax node)
  {
    return Visit(node.Block);
  }

  public override ulong VisitFixedStatement(FixedStatementSyntax node)
  {
    return HashCombine(Visit(node.Declaration), Visit(node.Statement));
  }

  public override ulong VisitMakeRefExpression(MakeRefExpressionSyntax node)
  {
    return Visit(node.Expression);
  }

  public override ulong VisitRefTypeExpression(RefTypeExpressionSyntax node)
  {
    return Visit(node.Expression);
  }

  public override ulong VisitRefValueExpression(RefValueExpressionSyntax node)
  {
    return HashCombine(Visit(node.Expression), Visit(node.Type));
  }

  public override ulong VisitImplicitElementAccess(ImplicitElementAccessSyntax node)
  {
    return Visit(node.ArgumentList);
  }

  public override ulong VisitBinaryPattern(BinaryPatternSyntax node)
  {
    return HashCombine(Visit(node.Left), Visit(node.Right));
  }

  public override ulong VisitUnaryPattern(UnaryPatternSyntax node)
  {
    return Visit(node.Pattern);
  }

  public override ulong VisitRelationalPattern(RelationalPatternSyntax node)
  {
    return HashCombine(HashString(node.OperatorToken.ValueText), Visit(node.Expression));
  }

  public override ulong VisitTypePattern(TypePatternSyntax node)
  {
    return Visit(node.Type);
  }

  public override ulong VisitParenthesizedPattern(ParenthesizedPatternSyntax node)
  {
    return Visit(node.Pattern);
  }

  public override ulong VisitDiscardPattern(DiscardPatternSyntax node)
  {
    return 0;
  }

  public override ulong VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
  {
    return node.ParameterList != null
      ? HashCombine(Visit(node.ParameterList), Visit(node.Block))
      : Visit(node.Block);
  }

  public override ulong VisitConditionalAccessExpression(ConditionalAccessExpressionSyntax node)
  {
    return HashCombine(Visit(node.Expression), Visit(node.WhenNotNull));
  }

  public override ulong VisitMemberBindingExpression(MemberBindingExpressionSyntax node)
  {
    return Visit(node.Name);
  }

  public override ulong VisitElementBindingExpression(ElementBindingExpressionSyntax node)
  {
    return Visit(node.ArgumentList);
  }

  public override ulong VisitDeclarationExpression(DeclarationExpressionSyntax node)
  {
    return HashCombine(Visit(node.Type), Visit(node.Designation));
  }

  public override ulong VisitParenthesizedVariableDesignation(ParenthesizedVariableDesignationSyntax node)
  {
    var hash = FnvOffsetBasis;
    
    foreach (var variable in node.Variables)
    {
      hash = HashCombine(hash, Visit(variable));
    }
    
    return hash;
  }

  public override ulong VisitThrowExpression(ThrowExpressionSyntax node)
  {
    return Visit(node.Expression);
  }

  public override ulong VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
  {
    var hash = HashCombine(Visit(node.ReturnType),
			HashString(node.Identifier.ValueText), Visit(node.ParameterList));
    
    if (node.Body != null)
    {
      hash = HashCombine(hash, Visit(node.Body));
    }
    
    if (node.ExpressionBody != null)
    {
      hash = HashCombine(hash, Visit(node.ExpressionBody));
    }
    
    return hash;
  }

  public override ulong VisitArrowExpressionClause(ArrowExpressionClauseSyntax node)
  {
    return Visit(node.Expression);
  }

  public override ulong VisitQueryContinuation(QueryContinuationSyntax node)
  {
    return HashCombine(HashString(node.Identifier.ValueText), Visit(node.Body));
  }

  public override ulong VisitJoinIntoClause(JoinIntoClauseSyntax node)
  {
    return HashString(node.Identifier.ValueText);
  }

  public override ulong VisitOmittedTypeArgument(OmittedTypeArgumentSyntax node)
  {
    return 0;
  }

  public override ulong VisitVarPattern(VarPatternSyntax node)
  {
    return Visit(node.Designation);
  }

  public override ulong VisitRecursivePattern(RecursivePatternSyntax node)
  {
    var hash = FnvOffsetBasis;
    
    if (node.Type != null)
    {
      hash = HashCombine(hash, Visit(node.Type));
    }
    
    if (node.PositionalPatternClause != null)
    {
      hash = HashCombine(hash, Visit(node.PositionalPatternClause));
    }
    
    if (node.PropertyPatternClause != null)
    {
      hash = HashCombine(hash, Visit(node.PropertyPatternClause));
    }
    
    if (node.Designation != null)
    {
      hash = HashCombine(hash, Visit(node.Designation));
    }
    
    return hash;
  }

  public override ulong VisitPositionalPatternClause(PositionalPatternClauseSyntax node)
  {
    var hash = FnvOffsetBasis;
    
    foreach (var subpattern in node.Subpatterns)
    {
      hash = HashCombine(hash, Visit(subpattern));
    }
    
    return hash;
  }

  public override ulong VisitPropertyPatternClause(PropertyPatternClauseSyntax node)
  {
    var hash = FnvOffsetBasis;
    
    foreach (var subpattern in node.Subpatterns)
    {
      hash = HashCombine(hash, Visit(subpattern));
    }
    
    return hash;
  }

  public override ulong VisitSubpattern(SubpatternSyntax node)
  {
    return node.NameColon != null
      ? HashCombine(Visit(node.NameColon), Visit(node.Pattern))
      : Visit(node.Pattern);
  }

  public override ulong VisitNameColon(NameColonSyntax node)
  {
    return Visit(node.Name);
  }

  public override ulong VisitCasePatternSwitchLabel(CasePatternSwitchLabelSyntax node)
  {
    return node.WhenClause != null
      ? HashCombine(Visit(node.Pattern), Visit(node.WhenClause))
      : Visit(node.Pattern);
  }

  public override ulong VisitFunctionPointerType(FunctionPointerTypeSyntax node)
  {
    return HashCombine(Visit(node.CallingConvention), Visit(node.ParameterList));
  }

  public override ulong VisitFunctionPointerParameterList(FunctionPointerParameterListSyntax node)
  {
    var hash = FnvOffsetBasis;
    
    foreach (var parameter in node.Parameters)
    {
      hash = HashCombine(hash, Visit(parameter));
    }
    
    return hash;
  }

  public override ulong VisitFunctionPointerParameter(FunctionPointerParameterSyntax node)
  {
    return Visit(node.Type);
  }

  public override ulong VisitFunctionPointerCallingConvention(FunctionPointerCallingConventionSyntax node)
  {
    return node.UnmanagedCallingConventionList != null
      ? Visit(node.UnmanagedCallingConventionList)
      : 0;
  }

  public override ulong VisitFunctionPointerUnmanagedCallingConventionList(FunctionPointerUnmanagedCallingConventionListSyntax node)
  {
    var hash = FnvOffsetBasis;
    
    foreach (var convention in node.CallingConventions)
    {
      hash = HashCombine(hash, Visit(convention));
    }
    
    return hash;
  }

  public override ulong VisitFunctionPointerUnmanagedCallingConvention(FunctionPointerUnmanagedCallingConventionSyntax node)
  {
    return HashString(node.Name.ValueText);
  }

  public override ulong VisitScopedType(ScopedTypeSyntax node)
  {
    return Visit(node.Type);
  }

  public override ulong VisitLineSpanDirectiveTrivia(LineSpanDirectiveTriviaSyntax node)
  {
    return HashCombine(Visit(node.Start), Visit(node.End));
  }

  public override ulong VisitLineDirectivePosition(LineDirectivePositionSyntax node)
  {
    return HashCombine(HashString(node.Line.ValueText), HashString(node.Character.ValueText));
  }

  public override ulong VisitCollectionExpression(CollectionExpressionSyntax node)
  {
    var hash = FnvOffsetBasis;
    
    foreach (var element in node.Elements)
    {
      hash = HashCombine(hash, Visit(element));
    }
    
    return hash;
  }

  public override ulong VisitExpressionElement(ExpressionElementSyntax node)
  {
    return Visit(node.Expression);
  }

  public override ulong VisitSpreadElement(SpreadElementSyntax node)
  {
    return Visit(node.Expression);
  }

  public override ulong VisitImplicitStackAllocArrayCreationExpression(ImplicitStackAllocArrayCreationExpressionSyntax node)
  {
    return Visit(node.Initializer);
  }

  public override ulong VisitListPattern(ListPatternSyntax node)
  {
    var hash = FnvOffsetBasis;
    
    foreach (var pattern in node.Patterns)
    {
      hash = HashCombine(hash, Visit(pattern));
    }
    
    if (node.Designation != null)
    {
      hash = HashCombine(hash, Visit(node.Designation));
    }
    
    return hash;
  }

  public override ulong VisitSlicePattern(SlicePatternSyntax node)
  {
    return node.Pattern != null
      ? Visit(node.Pattern)
      : 0;
  }

  public override ulong VisitPrimaryConstructorBaseType(PrimaryConstructorBaseTypeSyntax node)
  {
    return HashCombine(Visit(node.Type), Visit(node.ArgumentList));
  }

  public override ulong VisitConstructorInitializer(ConstructorInitializerSyntax node)
  {
    return Visit(node.ArgumentList);
  }

  public override ulong VisitAttribute(AttributeSyntax node)
  {
    return node.ArgumentList != null
      ? HashCombine(Visit(node.Name), Visit(node.ArgumentList))
      : Visit(node.Name);
  }

  public override ulong VisitAttributeList(AttributeListSyntax node)
  {
    var hash = FnvOffsetBasis;
    
    foreach (var attribute in node.Attributes)
    {
      hash = HashCombine(hash, Visit(attribute));
    }
    
    return hash;
  }

  public override ulong VisitAttributeArgument(AttributeArgumentSyntax node)
  {
    var hash = Visit(node.Expression);
    
    if (node.NameEquals != null)
    {
      hash = HashCombine(hash, Visit(node.NameEquals));
    }
    
    if (node.NameColon != null)
    {
      hash = HashCombine(hash, Visit(node.NameColon));
    }
    
    return hash;
  }

  public override ulong VisitAttributeArgumentList(AttributeArgumentListSyntax node)
  {
    var hash = FnvOffsetBasis;
    
    foreach (var argument in node.Arguments)
    {
      hash = HashCombine(hash, Visit(argument));
    }
    
    return hash;
  }

  public override ulong VisitNullableDirectiveTrivia(NullableDirectiveTriviaSyntax node)
  {
    return HashString(node.SettingToken.ValueText);
  }

  private ulong HashString(string value)
  {
    var hash = FnvOffsetBasis;

    foreach (var ch in value)
    {
      hash ^= ch;
      hash *= FnvPrime;
		}

    return hash;
	}

  private ulong HashCombine(ulong hash1, ulong hash2)
  {
    hash1 ^= hash2;
    hash1 *= FnvPrime;
    return hash1;
  }

  private ulong HashCombine(ulong hash1, ulong hash2, ulong hash3)
  {
    hash1 = HashCombine(hash1, hash2);
    return HashCombine(hash1, hash3);
  }

  private ulong HashCombine(ulong hash1, ulong hash2, ulong hash3, ulong hash4)
  {
    hash1 = HashCombine(hash1, hash2);
    hash1 = HashCombine(hash1, hash3);
    return HashCombine(hash1, hash4);
  }
}