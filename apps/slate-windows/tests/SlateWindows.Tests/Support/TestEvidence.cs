// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace SlateWindows.Tests;

/// <summary>Evidence must bind to runnable standard xUnit declarations and source
/// calls reachable from them. This is a static potential-execution check,
/// not proof that a runtime branch or an external callback scheduler runs.
/// Direct helpers, local functions, and callbacks invoked by source helpers
/// are followed, as are callbacks of locally constructed, explicitly started
/// Threads. Overridable dispatch and unused delegate bodies fail closed.
/// Custom discoverers/attribute constructors are not modeled. The repository
/// currently uses the standard Fact/Theory attributes.</summary>
internal sealed class TestEvidence
{
    private readonly CSharpCompilation _compilation;
    private readonly IAssemblySymbol _xunit;
    private readonly IMethodSymbol[] _tests;
    private readonly Lazy<HashSet<string>> _axeLabels;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, HashSet<string>> _patternEvidence = new(StringComparer.Ordinal);

    internal TestEvidence(CSharpCompilation compilation)
    {
        _compilation = compilation;
        IAssemblySymbol? xunit = compilation.SourceModule.ReferencedAssemblySymbols.SingleOrDefault(assembly => assembly.Name == "xunit.core");
        Assert.NotNull(xunit);
        _xunit = xunit;
        INamedTypeSymbol fact = _xunit.GetTypeByMetadataName("Xunit.FactAttribute")!;
        _tests = [.. compilation.SyntaxTrees.SelectMany(tree => tree.GetRoot().DescendantNodes()
                .OfType<MethodDeclarationSyntax>())
            .Select(method => compilation.GetSemanticModel(method.SyntaxTree).GetDeclaredSymbol(method))
            .OfType<IMethodSymbol>().Where(method => IsTest(method, fact))];
        _axeLabels = new(FindAxeLabels);
    }

    internal bool HasTestEvidence(string name) => _tests.Any(method => Matches(method, name));

    private static bool Matches(IMethodSymbol method, string name) =>
        method.Name == name || method.ContainingType.Name == name
        || method.DeclaringSyntaxReferences.Any(reference =>
            reference.SyntaxTree.FilePath.Replace('\\', '/').EndsWith("/" + name + ".cs", StringComparison.Ordinal));

    internal bool HasPatternEvidence(string name, string pattern)
    {
        HashSet<string> observations = _patternEvidence.GetOrAdd(name, key =>
        {
            var found = new HashSet<string>(StringComparer.Ordinal);
            foreach (IMethodSymbol test in _tests.Where(method => Matches(method, key)))
            {
                VisitMethod(test, new Context(), new HashSet<ISymbol>(SymbolEqualityComparer.Default), null, found);
            }
            return found;
        });
        return observations.Contains("pattern:" + pattern);
    }

    internal bool HasAxeLabel(string label) => _axeLabels.Value.Contains(label);

    internal static bool HasFixture(string root, string name) =>
        Directory.Exists(root) && Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Any(path => string.Equals(Path.GetFileNameWithoutExtension(path), name, StringComparison.Ordinal));

    private bool IsTest(IMethodSymbol method, INamedTypeSymbol fact)
    {
        if (method.DeclaredAccessibility != Microsoft.CodeAnalysis.Accessibility.Public || method.IsAbstract || method.IsGenericMethod
            || method.MethodKind != MethodKind.Ordinary
            || !method.DeclaringSyntaxReferences.Any(reference => Body(reference.GetSyntax()) is not null))
        {
            return false;
        }
        for (INamedTypeSymbol? type = method.ContainingType; type is not null; type = type.ContainingType)
        {
            if (type.DeclaredAccessibility != Microsoft.CodeAnalysis.Accessibility.Public || (type.IsAbstract && !type.IsStatic) || type.IsGenericType) { return false; }
        }
        if (!method.ContainingType.IsStatic && method.ContainingType.InstanceConstructors.Count(constructor =>
                constructor.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public) != 1) { return false; }
        if (!method.ReturnsVoid && !Inherits(method.ReturnType as INamedTypeSymbol,
                _compilation.GetTypeByMetadataName("System.Threading.Tasks.Task"))) { return false; }
        INamedTypeSymbol? theoryType = _xunit.GetTypeByMetadataName("Xunit.TheoryAttribute");
        AttributeData[] facts = [.. method.GetAttributes().Where(attribute =>
            SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, fact)
            || SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, theoryType))];
        if (facts.Length != 1 || facts[0].NamedArguments.Any(argument => argument.Key == "Skip" && argument.Value.Value is not null))
        {
            return false;
        }
        bool theory = SymbolEqualityComparer.Default.Equals(facts[0].AttributeClass, theoryType);
        return theory ? HasInlineRow(method) || HasDataProvider(method) : method.Parameters.Length == 0;
    }

    private bool HasInlineRow(IMethodSymbol method)
    {
        INamedTypeSymbol? inlineData = _xunit.GetTypeByMetadataName("Xunit.InlineDataAttribute");
        foreach (AttributeData attribute in method.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, inlineData)
                || attribute.ConstructorArguments is not [{ Kind: TypedConstantKind.Array } row]
                || row.IsNull || row.Values.Length != method.Parameters.Length) { continue; }
            bool viable = row.Values.Zip(method.Parameters).All(pair => pair.Second.RefKind == RefKind.None
                && (pair.First.IsNull ? pair.Second.Type.IsReferenceType
                    : pair.First.Type is { } type && _compilation.ClassifyConversion(type, pair.Second.Type).IsImplicit));
            if (viable) { return true; }
        }
        return false;
    }

    private bool HasDataProvider(IMethodSymbol method)
    {
        foreach (AttributeData attribute in method.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass,
                    _xunit.GetTypeByMetadataName("Xunit.MemberDataAttribute"))
                && attribute.ConstructorArguments.FirstOrDefault().Value is string memberName)
            {
                INamedTypeSymbol owner = attribute.NamedArguments.FirstOrDefault(argument => argument.Key == "MemberType").Value.Value
                    as INamedTypeSymbol ?? method.ContainingType;
                for (INamedTypeSymbol? type = owner; type is not null; type = type.BaseType)
                {
                    foreach (ISymbol member in type.GetMembers(memberName).Where(member =>
                        member.IsStatic && member.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public))
                    {
                        TypedConstant[] arguments = attribute.ConstructorArguments.Length > 1
                            && attribute.ConstructorArguments[1] is { Kind: TypedConstantKind.Array, IsNull: false } supplied
                            ? [.. supplied.Values] : [];
                        ITypeSymbol? result = member switch
                        {
                            IPropertySymbol property when property.Parameters.Length == 0
                                && property.GetMethod?.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public => property.Type,
                            IFieldSymbol field => field.Type,
                            IMethodSymbol provider when !provider.IsGenericMethod && provider.Parameters.Length == arguments.Length
                                && arguments.Zip(provider.Parameters).All(pair => ProviderArgumentFits(pair.First, pair.Second)) => provider.ReturnType,
                            _ => null,
                        };
                        if (HasProviderRows(result, allowUntypedRows: true)) { return true; }
                    }
                }
            }
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass,
                    _xunit.GetTypeByMetadataName("Xunit.ClassDataAttribute"))
                && attribute.ConstructorArguments is [{ Value: INamedTypeSymbol data }]
                && !data.IsAbstract && !data.IsGenericType && HasProviderRows(data, allowUntypedRows: false)
                && data.InstanceConstructors.Any(constructor => constructor.Parameters.Length == 0
                    && constructor.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public)) { return true; }
        }
        // The standard provider must bind. Its runtime data/side effects, like
        // runtime branch outcomes, remain the test runner's responsibility.
        return false;
    }

    private bool ProviderArgumentFits(TypedConstant argument, IParameterSymbol parameter)
    {
        if (parameter.RefKind != RefKind.None) { return false; }
        // xUnit's MemberData lookup uses reflection IsAssignableFrom, not
        // C# numeric/user-defined conversions. Null accepts any parameter.
        if (argument.IsNull) { return true; }
        if (argument.Type is not { } type) { return false; }
        Conversion conversion = _compilation.ClassifyConversion(type, parameter.Type);
        return conversion.IsIdentity || conversion.IsBoxing || (conversion.IsImplicit && conversion.IsReference);
    }

    private bool HasProviderRows(ITypeSymbol? type, bool allowUntypedRows)
    {
        if (type is null) { return false; }
        INamedTypeSymbol[] contracts = type is INamedTypeSymbol named ? [named, .. type.AllInterfaces] : [.. type.AllInterfaces];
        if (contracts.Any(contract => SymbolEqualityComparer.Default.Equals(contract,
                _xunit.GetTypeByMetadataName("Xunit.Sdk.ITheoryData")))) { return true; }
        IArrayTypeSymbol row = _compilation.CreateArrayTypeSymbol(_compilation.GetSpecialType(SpecialType.System_Object));
        ITypeSymbol[] elements = [.. contracts.Where(contract => contract.OriginalDefinition.SpecialType
                == SpecialType.System_Collections_Generic_IEnumerable_T).Select(contract => contract.TypeArguments[0])];
        bool Assignable(ITypeSymbol from, ITypeSymbol to) => _compilation.ClassifyConversion(from, to)
            is { IsIdentity: true } or { IsReference: true, IsImplicit: true };
        if (elements.Any(element => Assignable(element, row))) { return true; }
        // MemberData also permits non-generic IEnumerable. Unknown object rows
        // are runtime data; known impossible rows (e.g. IEnumerable<int>) are not.
        return allowUntypedRows && contracts.Any(contract => contract.SpecialType == SpecialType.System_Collections_IEnumerable)
            && (elements.Length == 0 || elements.Any(element => Assignable(row, element)));
    }

    private static bool Inherits(INamedTypeSymbol? type, INamedTypeSymbol? ancestor)
    {
        if (ancestor is null) { return false; }
        for (; type is not null; type = type.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, ancestor)) { return true; }
        }
        return false;
    }

    private HashSet<string> FindAxeLabels()
    {
        var labels = new HashSet<string>(StringComparer.Ordinal);
        IMethodSymbol? axe = _compilation.GetTypeByMetadataName("SlateWindows.AccessibilityTests.ShellAccessibilityTests")?
            .GetMembers("AssertAxeClean").OfType<IMethodSymbol>().SingleOrDefault(method =>
                method.IsStatic && method.ReturnsVoid && method.Parameters.Length == 2
                && method.Parameters[0].Type.ToDisplayString() == "System.Diagnostics.Process"
                && method.Parameters[1].Type.SpecialType == SpecialType.System_String);
        if (axe is null) { return labels; }
        foreach (IMethodSymbol test in _tests)
        {
            VisitMethod(test, new Context(), new HashSet<ISymbol>(SymbolEqualityComparer.Default), axe, labels);
        }
        return labels;
    }

    private sealed class Context
    {
        internal Dictionary<ISymbol, object?> Constants { get; } = new(SymbolEqualityComparer.Default);
        internal Dictionary<ISymbol, Callback> Callbacks { get; } = new(SymbolEqualityComparer.Default);
    }

    private sealed record Callback(SyntaxNode? Body, IMethodSymbol Method, Context Captured);

    private void VisitMethod(IMethodSymbol method, Context context, HashSet<ISymbol> active,
        IMethodSymbol? axe, HashSet<string> labels)
    {
        method = method.OriginalDefinition;
        if ((method.IsVirtual || method.IsOverride || method.IsAbstract)
            && !method.IsSealed && !method.ContainingType.IsSealed) { return; }
        if (!active.Add(method)) { return; }
        try
        {
            foreach (SyntaxReference reference in method.DeclaringSyntaxReferences)
            {
                if (Body(reference.GetSyntax()) is { } body) { VisitBody(body, context, active, axe, labels); }
            }
        }
        finally { active.Remove(method); }
    }

    private void VisitBody(SyntaxNode body, Context context, HashSet<ISymbol> active,
        IMethodSymbol? axe, HashSet<string> labels)
    {
        SemanticModel model = _compilation.GetSemanticModel(body.SyntaxTree);
        // A passed constant/delegate is useful evidence only while this body
        // does not replace it (including ref/out writes). Otherwise fail
        // closed instead of interpreting subsequent assignments as a runtime.
        if (model.AnalyzeDataFlow(body) is { Succeeded: true } flow)
        {
            var stable = new Context();
            foreach (var constant in context.Constants)
            {
                if (!flow.WrittenInside.Contains(constant.Key, SymbolEqualityComparer.Default)) { stable.Constants.Add(constant.Key, constant.Value); }
            }
            foreach (var callback in context.Callbacks)
            {
                if (!flow.WrittenInside.Contains(callback.Key, SymbolEqualityComparer.Default)) { stable.Callbacks.Add(callback.Key, callback.Value); }
            }
            context = stable;
        }
        foreach (MemberAccessExpressionSyntax member in body.DescendantNodesAndSelf(node =>
            node is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax).OfType<MemberAccessExpressionSyntax>())
        {
            if (!IsLive(member, body, model, context)) { continue; }
            // A positive assertion or a real pattern invocation is a witness;
            // merely mentioning a pattern, or asserting it is absent, is not.
            bool positive = member.Ancestors().OfType<InvocationExpressionSyntax>().Any(invocation =>
                model.GetSymbolInfo(invocation).Symbol is IMethodSymbol assertion
                && assertion.ContainingType.ToDisplayString() == "Xunit.Assert"
                && assertion.Name is "True" or "NotNull" or "IsAssignableFrom" or "IsType");
            if (member.Expression is MemberAccessExpressionSyntax pattern
                && pattern.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Patterns" }
                && model.GetSymbolInfo(pattern).Symbol is IPropertySymbol property
                && property.ContainingNamespace.ToDisplayString().StartsWith("FlaUI.", StringComparison.Ordinal)
                && (member.Name.Identifier.ValueText == "Pattern" || (positive && member.Name.Identifier.ValueText == "IsSupported")))
            {
                labels.Add("pattern:" + pattern.Name.Identifier.ValueText);
            }
            if (positive && model.GetSymbolInfo(member).Symbol is IFieldSymbol field
                && field.ContainingType.ToDisplayString() == "System.Windows.Automation.Peers.PatternInterface")
            {
                labels.Add("pattern:" + field.Name);
            }
        }
        foreach (InvocationExpressionSyntax invocation in body.DescendantNodesAndSelf(node =>
            node is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax)
            .OfType<InvocationExpressionSyntax>())
        {
            if (!IsLive(invocation, body, model, context)
                || model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol
                || model.GetOperation(invocation) is not IInvocationOperation call) { continue; }
            IMethodSymbol target = call.TargetMethod;
            if (StartedThreadCallback(call, body, model, context) is { } threadCallback)
            {
                if (threadCallback.Body is { } threadBody) { VisitBody(threadBody, threadCallback.Captured, active, axe, labels); }
                else { VisitMethod(threadCallback.Method, threadCallback.Captured, active, axe, labels); }
                continue;
            }
            if (SymbolEqualityComparer.Default.Equals(target.OriginalDefinition, axe))
            {
                IArgumentOperation? label = call.Arguments.SingleOrDefault(argument => argument.Parameter?.Ordinal == 1);
                if (label is not null && Constant(label.Value, model, context) is { HasValue: true, Value: string value })
                {
                    labels.Add(value);
                }
                continue;
            }
            if (target.MethodKind == MethodKind.DelegateInvoke)
            {
                if (ResolveCallback(call.Instance, context) is { } callback)
                {
                    if (callback.Method.Parameters.Length != target.Parameters.Length) { continue; }
                    Context invoked = BindArguments(call, model, context, callback.Captured, callback.Method);
                    if (callback.Body is { } callbackBody) { VisitBody(callbackBody, invoked, active, axe, labels); }
                    else { VisitMethod(callback.Method, invoked, active, axe, labels); }
                }
                continue;
            }
            VisitMethod(target, BindArguments(call, model, context, context, target), active, axe, labels);
        }
    }

    private Callback? StartedThreadCallback(IInvocationOperation call, SyntaxNode body, SemanticModel model, Context context)
    {
        INamedTypeSymbol? thread = _compilation.GetTypeByMetadataName("System.Threading.Thread");
        if (call.TargetMethod.Name != "Start" || call.Arguments.Length != 0
            || !SymbolEqualityComparer.Default.Equals(call.TargetMethod.ContainingType, thread)
            || call.Instance is not ILocalReferenceOperation local
            || local.Local.DeclaringSyntaxReferences.SingleOrDefault()?.GetSyntax() is not VariableDeclaratorSyntax declaration
            || declaration.Initializer?.Value is not { } initializer
            || declaration.SpanStart >= call.Syntax.SpanStart || !body.Span.Contains(declaration.Span)
            || !IsLive(declaration, body, model, context)
            || model.GetOperation(initializer) is not IObjectCreationOperation creation
            || !SymbolEqualityComparer.Default.Equals(creation.Type, thread)
            || creation.Arguments is not [{ Value: var start }]
            || ResolveCallback(start, context) is not { Method.Parameters.Length: 0 } callback)
        {
            return null;
        }
        // An alias, reassignment or ref/out escape is not a proof of which
        // thread Start invokes. Fail closed instead of interpreting it.
        foreach (IdentifierNameSyntax reference in body.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Where(node => SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(node).Symbol, local.Local)))
        {
            if (reference.Parent is not MemberAccessExpressionSyntax member || member.Expression != reference
                || member.Parent is not InvocationExpressionSyntax invocation
                || model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method
                || !SymbolEqualityComparer.Default.Equals(method.ContainingType, thread)) { return null; }
        }
        return callback;
    }

    private static Context BindArguments(IInvocationOperation call, SemanticModel model, Context caller, Context captured, IMethodSymbol target)
    {
        var nested = new Context();
        foreach (var constant in captured.Constants) { nested.Constants.Add(constant.Key, constant.Value); }
        foreach (var callback in captured.Callbacks) { nested.Callbacks.Add(callback.Key, callback.Value); }
        foreach (IArgumentOperation argument in call.Arguments)
        {
            if (argument.Parameter is not { } parameter) { continue; }
            IParameterSymbol original = target.Parameters[parameter.Ordinal].OriginalDefinition;
            nested.Constants.Remove(original);
            nested.Callbacks.Remove(original);
            Optional<object?> constant = Constant(argument.Value, model, caller);
            if (constant.HasValue) { nested.Constants[original] = constant.Value; }
            if (ResolveCallback(argument.Value, caller) is { } callback) { nested.Callbacks[original] = callback; }
        }
        return nested;
    }

    private static Callback? ResolveCallback(IOperation? value, Context context) => value switch
    {
        IConversionOperation conversion => ResolveCallback(conversion.Operand, context),
        IDelegateCreationOperation creation => ResolveCallback(creation.Target, context),
        IAnonymousFunctionOperation lambda => new(Body(lambda.Syntax), lambda.Symbol, context),
        IMethodReferenceOperation method when method.Method.ReducedFrom is null => new(null, method.Method, context),
        IParameterReferenceOperation parameter => context.Callbacks.GetValueOrDefault(parameter.Parameter),
        _ => null,
    };

    private static SyntaxNode? Body(SyntaxNode declaration) => declaration switch
    {
        MethodDeclarationSyntax method => (SyntaxNode?)method.Body ?? method.ExpressionBody?.Expression,
        LocalFunctionStatementSyntax local => (SyntaxNode?)local.Body ?? local.ExpressionBody?.Expression,
        LambdaExpressionSyntax lambda => lambda.Body,
        AnonymousMethodExpressionSyntax anonymous => anonymous.Block,
        _ => null,
    };

    private static Optional<object?> Constant(IOperation value, SemanticModel model, Context context)
    {
        if (value.ConstantValue.HasValue) { return value.ConstantValue; }
        if (value is IConversionOperation conversion) { return Constant(conversion.Operand, model, context); }
        if (value is IParameterReferenceOperation parameter && context.Constants.TryGetValue(parameter.Parameter, out object? constant))
        {
            return new Optional<object?>(constant);
        }
        if (value is IUnaryOperation { OperatorKind: UnaryOperatorKind.Not, OperatorMethod: null } unary
            && Constant(unary.Operand, model, context) is { HasValue: true, Value: bool operand }) { return new Optional<object?>(!operand); }
        if (value is IBinaryOperation { OperatorMethod: null } binary)
        {
            Optional<object?> left = Constant(binary.LeftOperand, model, context);
            Optional<object?> right = Constant(binary.RightOperand, model, context);
            if (left is { HasValue: true, Value: bool a } && right is { HasValue: true, Value: bool b })
            {
                return binary.OperatorKind switch
                {
                    BinaryOperatorKind.Equals => new Optional<object?>(a == b),
                    BinaryOperatorKind.NotEquals => new Optional<object?>(a != b),
                    BinaryOperatorKind.ConditionalAnd or BinaryOperatorKind.And => new Optional<object?>(a && b),
                    BinaryOperatorKind.ConditionalOr or BinaryOperatorKind.Or => new Optional<object?>(a || b),
                    BinaryOperatorKind.ExclusiveOr => new Optional<object?>(a ^ b),
                    _ => default,
                };
            }
            if (binary.OperatorKind == BinaryOperatorKind.ConditionalAnd && left is { HasValue: true, Value: false }) { return left; }
            if (binary.OperatorKind == BinaryOperatorKind.ConditionalOr && left is { HasValue: true, Value: true }) { return left; }
        }
        return default;
    }

    private static bool? Boolean(ExpressionSyntax expression, SemanticModel model, Context context)
    {
        if (model.GetOperation(expression) is { } operation && Constant(operation, model, context) is { HasValue: true, Value: bool value })
        {
            return value;
        }
        return expression is PrefixUnaryExpressionSyntax prefix && prefix.IsKind(SyntaxKind.LogicalNotExpression)
            ? Boolean(prefix.Operand, model, context) is { } operand ? !operand : null
            : null;
    }

    private static bool IsLive(SyntaxNode invocation, SyntaxNode body, SemanticModel model, Context context)
    {
        foreach (InvocationExpressionSyntax call in invocation.AncestorsAndSelf().OfType<InvocationExpressionSyntax>())
        {
            if (model.GetSymbolInfo(call).Symbol is not IMethodSymbol target) { continue; }
            AttributeData[] conditional = [.. target.GetAttributes().Where(attribute =>
                attribute.AttributeClass?.ToDisplayString() == "System.Diagnostics.ConditionalAttribute")];
            if (conditional.Length > 0 && !conditional.Any(attribute => attribute.ConstructorArguments is [{ Value: string symbol }]
                && ((CSharpParseOptions)call.SyntaxTree.Options).PreprocessorSymbolNames.Contains(symbol))) { return false; }
        }
        StatementSyntax? statement = invocation.Ancestors().OfType<StatementSyntax>()
            .FirstOrDefault(candidate => body.FullSpan.Contains(candidate.Span));
        if (statement is not null && model.AnalyzeControlFlow(statement) is { Succeeded: true, StartPointIsReachable: false })
        {
            return false;
        }
        for (SyntaxNode? child = invocation, parent = invocation.Parent;
            parent is not null && body.FullSpan.Contains(parent.Span); child = parent, parent = parent.Parent)
        {
            if (parent is BlockSyntax block && child is StatementSyntax current
                && block.Statements.TakeWhile(statement => !ReferenceEquals(statement, current))
                    .Any(statement => Stops(statement, model, context))) { return false; }
            if (parent is IfStatementSyntax conditional && Boolean(conditional.Condition, model, context) is { } condition
                && ((ReferenceEquals(child, conditional.Statement) && !condition)
                    || (ReferenceEquals(child, conditional.Else) && condition))) { return false; }
            if (parent is WhileStatementSyntax loop && ReferenceEquals(child, loop.Statement)
                && Boolean(loop.Condition, model, context) == false) { return false; }
            if (parent is ForStatementSyntax forLoop && ReferenceEquals(child, forLoop.Statement)
                && forLoop.Condition is { } test && Boolean(test, model, context) == false) { return false; }
            if (parent is ConditionalExpressionSyntax choice && Boolean(choice.Condition, model, context) is { } chosen
                && ((ReferenceEquals(child, choice.WhenTrue) && !chosen) || (ReferenceEquals(child, choice.WhenFalse) && chosen))) { return false; }
            if (parent is BinaryExpressionSyntax binary && ReferenceEquals(child, binary.Right)
                && ((binary.IsKind(SyntaxKind.LogicalAndExpression) && Boolean(binary.Left, model, context) == false)
                    || (binary.IsKind(SyntaxKind.LogicalOrExpression) && Boolean(binary.Left, model, context) == true))) { return false; }
        }
        return true;
    }

    private static bool Stops(StatementSyntax statement, SemanticModel model, Context context) => statement switch
    {
        ReturnStatementSyntax or ThrowStatementSyntax or BreakStatementSyntax or ContinueStatementSyntax => true,
        BlockSyntax block => block.Statements.Any(child => Stops(child, model, context)),
        IfStatementSyntax conditional when Boolean(conditional.Condition, model, context) is { } condition =>
            condition ? Stops(conditional.Statement, model, context)
                : conditional.Else is { } otherwise && Stops(otherwise.Statement, model, context),
        IfStatementSyntax conditional => Stops(conditional.Statement, model, context)
            && conditional.Else is { } otherwise && Stops(otherwise.Statement, model, context),
        _ => false,
    };
}
