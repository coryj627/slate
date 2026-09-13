// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace SlateWindows.Tests;

/// <summary>
/// A bounded source-to-sink census for the graph config path, not a general
/// filesystem verifier. It follows locals, fields, source getters/returns and
/// call arguments using Roslyn symbols. A helper is analyzed separately for
/// each graph-path input and writer context, so sharing SafeFile.TryDelete
/// never grants an unrelated caller the store's authority.
/// </summary>
internal sealed class GraphConfigMutationCensus
{
    private const string Store = "SlateWindows.Graph.GraphConfigStore";
    private const int MaximumContexts = 20000;
    private readonly CSharpCompilation _compilation;
    private readonly SyntaxTree[] _trees;
    private readonly Dictionary<IMethodSymbol, Callable> _methods = new(SymbolEqualityComparer.Default);
    private readonly HashSet<ISymbol> _graphFields = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<Context, Result> _cache = [];
    private readonly HashSet<Context> _active = [];
    private readonly SortedSet<string> _violations = new(StringComparer.Ordinal);
    private readonly SortedSet<string> _mutations = new(StringComparer.Ordinal);
    private int _contexts;
    private int _fileNames;
    private readonly HashSet<IMethodSymbol> _called = new(SymbolEqualityComparer.Default);

    private sealed record Callable(IMethodSymbol Symbol, IOperation Body, bool ExpressionBody);
    private sealed record Context(IMethodSymbol Method, string Inputs, bool Receiver, bool Authorized);
    private readonly record struct Result(bool Returned, bool Receiver, bool[] Outputs);

    private sealed class State(IMethodSymbol? method, bool receiver, bool authorized, bool graphInputs)
    {
        internal IMethodSymbol? Method { get; } = method;
        internal bool Receiver { get; set; } = receiver;
        internal bool Authorized { get; } = authorized;
        internal bool GraphInputs { get; } = graphInputs;
        internal bool Returned { get; set; }
        internal bool ConditionalReceiver { get; set; }
        internal bool InitializerReceiver { get; set; }
        internal Dictionary<ISymbol, bool> Values { get; } = new(SymbolEqualityComparer.Default);
        internal Dictionary<ISymbol, HashSet<ISymbol>> Aliases { get; } = new(SymbolEqualityComparer.Default);
    }

    private GraphConfigMutationCensus(CSharpCompilation compilation, IEnumerable<SyntaxTree> trees)
    {
        _compilation = compilation;
        _trees = [.. trees];
    }

    internal sealed record Report(IReadOnlyList<string> Violations, IReadOnlyList<string> Mutations);

    internal static Report Inspect(CSharpCompilation compilation, IEnumerable<SyntaxTree> trees)
    {
        var census = new GraphConfigMutationCensus(compilation, trees);
        census.Collect();
        census.Analyze();
        return new([.. census._violations], [.. census._mutations]);
    }

    private void Collect()
    {
        foreach (SyntaxTree tree in _trees)
        {
            SemanticModel model = _compilation.GetSemanticModel(tree);
            foreach (SyntaxNode node in tree.GetRoot().DescendantNodes())
            {
                // References to FileName are allowed. A second literal (also
                // a folded literal concatenation or an embedded full path) is not.
                if (node is LiteralExpressionSyntax or BinaryExpressionSyntax or InterpolatedStringExpressionSyntax
                    && !node.DescendantNodes().OfType<IdentifierNameSyntax>().Any()
                    && model.GetConstantValue(node) is { HasValue: true, Value: string text }
                    && NamesGraph(text)
                    && !IsFileNameInitializer(model, node))
                {
                    _violations.Add($"{Location(node)}: graph.json is named outside GraphConfigStore.FileName");
                }
                if (node is VariableDeclaratorSyntax variable && model.GetDeclaredSymbol(variable) is IFieldSymbol field
                    && field.Name == "FileName" && field.ContainingType.ToDisplayString() == Store)
                {
                    _fileNames++;
                    if (!field.IsConst || field.ConstantValue is not "graph.json")
                    {
                        _violations.Add($"{Location(node)}: GraphConfigStore.FileName must remain the constant graph.json");
                    }
                }
                switch (node)
                {
                    case MethodDeclarationSyntax method:
                        Add(model.GetDeclaredSymbol(method), method.Body, method.ExpressionBody?.Expression, model);
                        break;
                    case ConstructorDeclarationSyntax constructor:
                        AddConstructor(model.GetDeclaredSymbol(constructor), model.GetOperation(constructor));
                        break;
                    case TypeDeclarationSyntax { ParameterList: not null } type:
                        if (model.GetDeclaredSymbol(type) is INamedTypeSymbol declared)
                        {
                            IMethodSymbol? primary = declared.InstanceConstructors.FirstOrDefault(candidate =>
                                candidate.DeclaringSyntaxReferences.Any(reference => reference.Span == type.Span));
                            AddConstructor(primary, model.GetOperation(type));
                        }
                        break;
                    case LocalFunctionStatementSyntax local:
                        Add(model.GetDeclaredSymbol(local), local.Body, local.ExpressionBody?.Expression, model);
                        break;
                    case AccessorDeclarationSyntax accessor:
                        Add(model.GetDeclaredSymbol(accessor), accessor.Body, accessor.ExpressionBody?.Expression, model);
                        break;
                    case PropertyDeclarationSyntax property when property.ExpressionBody is not null:
                        Add(model.GetDeclaredSymbol(property)?.GetMethod, null, property.ExpressionBody.Expression, model);
                        break;
                }
            }
        }
    }

    private static IEnumerable<IOperation> Operations(IOperation root)
    {
        yield return root;
        foreach (IOperation child in root.ChildOperations)
        {
            foreach (IOperation nested in Operations(child))
            {
                yield return nested;
            }
        }
    }

    private void Add(IMethodSymbol? symbol, BlockSyntax? block, ExpressionSyntax? expression, SemanticModel model)
    {
        SyntaxNode? body = (SyntaxNode?)block ?? expression;
        if (symbol is not null && body is not null && model.GetOperation(body) is { } operation)
        {
            _methods[symbol.OriginalDefinition] = new(symbol.OriginalDefinition, operation, expression is not null);
        }
    }

    private void AddConstructor(IMethodSymbol? symbol, IOperation? operation)
    {
        if (symbol is not null && operation is not null)
        {
            // The complete operation includes base/this initializers, whose
            // stream-open effects occur before the constructor's own body.
            _methods[symbol.OriginalDefinition] = new(symbol.OriginalDefinition, operation, false);
        }
    }

    private void Analyze()
    {
        if (_fileNames != 1)
        {
            _violations.Add($"Expected exactly one GraphConfigStore.FileName declaration; found {_fileNames}.");
        }
        foreach (Callable callable in _methods.Values)
        {
            foreach (IOperation operation in Operations(callable.Body))
            {
                IMethodSymbol? target = operation switch
                {
                    IInvocationOperation call => call.TargetMethod,
                    IObjectCreationOperation creation => creation.Constructor,
                    IMethodReferenceOperation reference => reference.Method,
                    IPropertyReferenceOperation property => property.Property.GetMethod,
                    _ => null,
                };
                if (target is not null)
                {
                    _called.Add(target.OriginalDefinition);
                }
            }
        }
        // Fields populated by a source constructor/getter are discovered to a
        // fixed point. Parameter-dependent instance fields stay in the call
        // context rather than tainting every unrelated instance in the shell.
        int prior;
        do
        {
            prior = _graphFields.Count;
            _cache.Clear();
            _contexts = 0;
            foreach (SyntaxTree tree in _trees)
            {
                SemanticModel model = _compilation.GetSemanticModel(tree);
                foreach (VariableDeclaratorSyntax variable in tree.GetRoot().DescendantNodes().OfType<VariableDeclaratorSyntax>())
                {
                    if (model.GetDeclaredSymbol(variable) is IFieldSymbol field && variable.Initializer is { } initializer)
                    {
                        var state = new State(null, false, false, false);
                        if (Eval(model.GetOperation(initializer.Value), state))
                        {
                            _graphFields.Add(field);
                        }
                    }
                }
            }
            foreach (SyntaxTree tree in _trees)
            {
                SemanticModel model = _compilation.GetSemanticModel(tree);
                foreach (PropertyDeclarationSyntax property in tree.GetRoot().DescendantNodes().OfType<PropertyDeclarationSyntax>())
                {
                    if (property.Initializer is { } initializer && model.GetDeclaredSymbol(property) is { } symbol
                        && Eval(model.GetOperation(initializer.Value), new State(null, false, false, false)))
                    {
                        _graphFields.Add(symbol);
                    }
                }
            }
            foreach (Callable callable in _methods.Values)
            {
                // A called private helper inherits its real callers' authority;
                // inventing an independent root would reject Write-only helpers
                // that read the store's field instead of taking a path parameter.
                if (callable.Symbol.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Private && _called.Contains(callable.Symbol))
                {
                    continue;
                }
                Invoke(callable.Symbol, new bool[callable.Symbol.Parameters.Length], false, false);
            }
        }
        while (prior != _graphFields.Count);
    }

    private Result Invoke(IMethodSymbol method, bool[] arguments, bool receiver, bool authorized)
    {
        method = method.OriginalDefinition;
        if (!_methods.TryGetValue(method, out Callable? callable))
        {
            return new(arguments.Any(value => value) || receiver, receiver, arguments);
        }
        authorized |= IsWriter(method);
        var context = new Context(method, string.Concat(arguments.Select(value => value ? '1' : '0')), receiver, authorized);
        if (_cache.TryGetValue(context, out Result cached))
        {
            return cached;
        }
        if (!_active.Add(context))
        {
            if (receiver || arguments.Any(value => value))
            {
                _violations.Add($"{Location(callable.Body.Syntax)}: recursive graph-path flow requires an explicit census model");
            }
            return new(receiver || arguments.Any(value => value), receiver, arguments);
        }
        Assert.True(++_contexts <= MaximumContexts, "Graph config census exceeded its context bound; analysis must not truncate silently.");
        var state = new State(method, receiver, authorized, receiver || arguments.Any(value => value));
        for (int index = 0; index < method.Parameters.Length; index++)
        {
            state.Values[method.Parameters[index]] = arguments[index];
        }
        // Monotone local joins also cover loop-carried and branch assignments.
        // They may conservatively reject a path overwritten before a write.
        int known;
        do
        {
            known = state.Values.Count(pair => pair.Value);
            bool value = Eval(callable.Body, state);
            state.Returned |= callable.ExpressionBody && value;
        }
        while (known != state.Values.Count(pair => pair.Value));
        _active.Remove(context);
        // Intrinsic graph fields have exact global member entries. Promoting
        // those again to an opaque receiver would taint unrelated members of
        // every object that contains the store. Only input-dependent effects
        // need the conservative receiver summary.
        var result = new Result(state.Returned, state.GraphInputs && state.Receiver,
            [.. method.Parameters.Select(parameter => state.Values.GetValueOrDefault(parameter))]);
        _cache[context] = result;
        return result;
    }

    private bool Eval(IOperation? operation, State state)
    {
        if (operation is null)
        {
            return false;
        }
        switch (operation)
        {
            case ILiteralOperation literal:
                return literal.ConstantValue is { HasValue: true, Value: string text } && NamesGraph(text);
            case ILocalReferenceOperation local:
                return state.Values.GetValueOrDefault(local.Local);
            case IParameterReferenceOperation parameter:
                return state.Values.GetValueOrDefault(parameter.Parameter);
            case IInstanceReferenceOperation instance:
                return instance.ReferenceKind == InstanceReferenceKind.ImplicitReceiver ? state.InitializerReceiver : state.Receiver;
            case IConditionalAccessInstanceOperation:
                return state.ConditionalReceiver;
            case IConditionalAccessOperation access:
                {
                    bool outer = state.ConditionalReceiver;
                    state.ConditionalReceiver = Eval(access.Operation, state);
                    bool value = Eval(access.WhenNotNull, state);
                    state.ConditionalReceiver = outer;
                    return value;
                }
            case IFieldReferenceOperation field:
                return _graphFields.Contains(field.Field)
                    || state.Values.GetValueOrDefault(field.Field)
                    || (CarriesPath(field.Type) && Eval(field.Instance, state));
            case IPropertyReferenceOperation property:
                {
                    if (_graphFields.Contains(property.Property) || state.Values.GetValueOrDefault(property.Property))
                    {
                        return true;
                    }
                    bool receiver = Eval(property.Instance, state);
                    bool[] arguments = property.Arguments.Select(argument => Eval(argument.Value, state)).ToArray();
                    if (property.Property.GetMethod is { } getter && _methods.ContainsKey(getter.OriginalDefinition))
                    {
                        return Invoke(getter, arguments, receiver, state.Authorized).Returned;
                    }
                    return CarriesPath(property.Type) && (receiver || arguments.Any(value => value));
                }
            case IVariableDeclaratorOperation variable:
                {
                    bool value = Eval(variable.Initializer?.Value, state);
                    Alias(variable.Symbol, variable.Initializer?.Value, state);
                    SetValue(variable.Symbol, value, state);
                    return value;
                }
            case ISimpleAssignmentOperation assignment:
                {
                    bool value = Eval(assignment.Value, state);
                    if (ReferencedSymbol(assignment.Target) is { } target)
                    {
                        Alias(target, assignment.Value, state);
                    }
                    Assign(assignment.Target, value, state);
                    return value;
                }
            case ICompoundAssignmentOperation assignment:
                {
                    bool value = Eval(assignment.Target, state) | Eval(assignment.Value, state);
                    Assign(assignment.Target, value, state);
                    return value;
                }
            case IReturnOperation returned:
                {
                    bool value = Eval(returned.ReturnedValue, state);
                    state.Returned |= value;
                    return value;
                }
            case IMethodReferenceOperation reference:
                return Invoke(reference.Method, new bool[reference.Method.Parameters.Length], Eval(reference.Instance, state), state.Authorized).Returned;
            case IInvocationOperation invocation:
                return Call(invocation.TargetMethod, invocation.Arguments, invocation.Instance, invocation.Syntax, state,
                    invocation.TargetMethod.MethodKind == MethodKind.Constructor);
            case IObjectCreationOperation creation when creation.Constructor is { } constructor:
                {
                    bool value = Call(constructor, creation.Arguments, null, creation.Syntax, state, true);
                    bool outer = state.InitializerReceiver;
                    state.InitializerReceiver = value;
                    bool initialized = Eval(creation.Initializer, state);
                    value |= initialized || state.InitializerReceiver;
                    state.InitializerReceiver = outer;
                    return value;
                }
            case IConversionOperation conversion:
                return Eval(conversion.Operand, state);
            case IInterpolationOperation interpolation:
                return Eval(interpolation.Expression, state);
            case IArrayInitializerOperation initializer:
                return initializer.ElementValues.Select(element => Eval(element, state)).ToArray().Any(value => value);
            case ITupleOperation tuple:
                return tuple.Elements.Select(element => Eval(element, state)).ToArray().Any(value => value);
            case IDeconstructionAssignmentOperation assignment:
                {
                    bool value = Eval(assignment.Value, state);
                    Assign(assignment.Target, value, state);
                    return value;
                }
            case IForEachLoopOperation loop:
                {
                    bool value = Eval(loop.Collection, state);
                    Assign(loop.LoopControlVariable, value, state);
                    Eval(loop.Body, state);
                    return false;
                }
            case IDelegateCreationOperation creation:
                return Eval(creation.Target, state);
            case IAnonymousFunctionOperation function:
                {
                    // Captured locals keep the originating writer context. Merely
                    // introducing a lambda cannot hide a mutation or returned path.
                    bool outerReturn = state.Returned;
                    state.Returned = false;
                    Eval(function.Body, state);
                    bool returned = state.Returned;
                    state.Returned = outerReturn;
                    return returned;
                }
            default:
                {
                    bool value = false;
                    foreach (IOperation child in operation.ChildOperations)
                    {
                        value |= Eval(child, state);
                    }
                    if (operation is IInvalidOperation && value
                        && operation.Syntax is InvocationExpressionSyntax or BaseObjectCreationExpressionSyntax)
                    {
                        _violations.Add($"{Location(operation.Syntax)}: graph-path call did not bind; census cannot classify its effects");
                    }
                    return CarriesPath(operation.Type) && value;
                }
        }
    }

    private static ISymbol? ReferencedSymbol(IOperation operation) => operation switch
    {
        ILocalReferenceOperation local => local.Local,
        IParameterReferenceOperation parameter => parameter.Parameter,
        IFieldReferenceOperation field => field.Field,
        IPropertyReferenceOperation property => property.Property,
        IConversionOperation conversion => ReferencedSymbol(conversion.Operand),
        _ => null,
    };

    private static bool MutableCarrier(ITypeSymbol? type) => CarriesPath(type)
        && type is { IsReferenceType: true, SpecialType: not SpecialType.System_String };

    private static void Alias(ISymbol target, IOperation? value, State state)
    {
        if (value is null || !MutableCarrier(value.Type) || ReferencedSymbol(value) is not { } source)
        {
            return;
        }
        if (!state.Aliases.TryGetValue(target, out HashSet<ISymbol>? targets))
        {
            state.Aliases[target] = targets = new(SymbolEqualityComparer.Default);
        }
        if (!state.Aliases.TryGetValue(source, out HashSet<ISymbol>? sources))
        {
            state.Aliases[source] = sources = new(SymbolEqualityComparer.Default);
        }
        targets.Add(source);
        sources.Add(target);
        SetValue(target, state.Values.GetValueOrDefault(source) || state.Values.GetValueOrDefault(target), state);
    }

    private static void SetValue(ISymbol symbol, bool value, State state)
    {
        if (!value)
        {
            state.Values.TryAdd(symbol, false);
            return;
        }
        var pending = new Queue<ISymbol>();
        pending.Enqueue(symbol);
        var visited = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        while (pending.TryDequeue(out ISymbol? current))
        {
            if (!visited.Add(current))
            {
                continue;
            }
            state.Values[current] = true;
            if (state.Aliases.TryGetValue(current, out HashSet<ISymbol>? aliases))
            {
                foreach (ISymbol alias in aliases)
                {
                    pending.Enqueue(alias);
                }
            }
        }
    }

    private void Assign(IOperation target, bool value, State state)
    {
        if (target is IArrayElementReferenceOperation arrayElement)
        {
            Assign(arrayElement.ArrayReference, value, state);
            return;
        }
        if (target is IInstanceReferenceOperation instance)
        {
            if (instance.ReferenceKind == InstanceReferenceKind.ImplicitReceiver)
            {
                state.InitializerReceiver |= value;
            }
            else
            {
                state.Receiver |= value;
            }
            return;
        }
        if (target is IDeclarationExpressionOperation declaration)
        {
            Assign(declaration.Expression, value, state);
            return;
        }
        if (target is ITupleOperation tuple)
        {
            foreach (IOperation element in tuple.Elements)
            {
                Assign(element, value, state);
            }
            return;
        }
        ISymbol? symbol = target switch
        {
            IVariableDeclaratorOperation variable => variable.Symbol,
            ILocalReferenceOperation local => local.Local,
            IParameterReferenceOperation parameter => parameter.Parameter,
            IFieldReferenceOperation field => field.Field,
            IPropertyReferenceOperation property => property.Property,
            _ => null,
        };
        if (symbol is not null)
        {
            SetValue(symbol, value, state);
        }
        if (target is IFieldReferenceOperation fieldTarget && value)
        {
            if (fieldTarget.Instance is not null && NeedsCarrierEffect(fieldTarget.Instance, state))
            {
                Assign(fieldTarget.Instance, true, state);
            }
            if (fieldTarget.Field.IsStatic || !state.GraphInputs)
            {
                _graphFields.Add(fieldTarget.Field);
            }
        }
        if (target is IPropertyReferenceOperation propertyTarget)
        {
            if (value && propertyTarget.Instance is not null && NeedsCarrierEffect(propertyTarget.Instance, state))
            {
                Assign(propertyTarget.Instance, true, state);
            }
            if (value && (propertyTarget.Property.IsStatic || !state.GraphInputs)
                && propertyTarget.Property.Locations.Any(location => location.IsInSource))
            {
                _graphFields.Add(propertyTarget.Property);
            }
            bool receiver = Eval(propertyTarget.Instance, state);
            if (propertyTarget.Property.SetMethod is { } setter && _methods.ContainsKey(setter.OriginalDefinition))
            {
                Invoke(setter, [value], receiver, state.Authorized);
            }
            else if (receiver && propertyTarget.Property.ContainingType.ToDisplayString() == "System.IO.FileInfo")
            {
                Mutation(target.Syntax, $"FileInfo.{propertyTarget.Property.Name}=", state);
            }
        }
    }

    // Intrinsic assignments to this already have an exact member entry. Other
    // receivers (parameters, locals, object initializers) need a carrier effect
    // so a later call through that object cannot discard the assigned path.
    private static bool NeedsCarrierEffect(IOperation instance, State state) => state.GraphInputs
        || instance is not IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ContainingTypeInstance };

    private bool Call(IMethodSymbol method, System.Collections.Immutable.ImmutableArray<IArgumentOperation> arguments,
        IOperation? instance, SyntaxNode syntax, State state, bool construction)
    {
        bool receiver = Eval(instance, state);
        var values = new bool[method.Parameters.Length];
        foreach (IArgumentOperation argument in arguments)
        {
            bool value = Eval(argument.Value, state);
            if (argument.Parameter is { } parameter)
            {
                values[parameter.Ordinal] |= value;
            }
        }
        string type = method.ContainingType.ToDisplayString();
        bool any = values.Any(value => value);
        if (_methods.ContainsKey(method.OriginalDefinition))
        {
            Result result = Invoke(method, values, receiver, state.Authorized);
            if (result.Receiver && instance is not null)
            {
                Assign(instance, true, state);
            }
            foreach (IArgumentOperation argument in arguments)
            {
                if (argument.Parameter is { } parameter
                    && (parameter.RefKind != RefKind.None || MutableCarrier(parameter.Type)))
                {
                    Assign(argument.Value, result.Outputs[parameter.Ordinal], state);
                }
            }
            return construction ? result.Receiver || any : result.Returned;
        }
        if (method.MethodKind == MethodKind.DelegateInvoke && any && !state.Authorized)
        {
            _violations.Add($"{Location(syntax)}: graph-path delegate call requires an explicit census model");
            return CarriesPath(method.ReturnType);
        }
        if (type == "System.IO.Path")
        {
            // A parent directory is not the config file (nor its temporary
            // replacement); writing another named child is unrelated I/O.
            return method.Name != "GetDirectoryName" && any;
        }
        if (type == "System.IO.FileInfo" && construction)
        {
            return any;
        }
        if (type is "System.IO.File" or "System.IO.FileInfo")
        {
            bool relevant = type == "System.IO.FileInfo" ? receiver : values.FirstOrDefault();
            string name = method.Name;
            if (name is "Copy" or "CopyTo")
            {
                relevant = values.Length > (type == "System.IO.File" ? 1 : 0)
                    && values[type == "System.IO.File" ? 1 : 0];
            }
            else if (name is "Move" or "MoveTo" or "Replace")
            {
                relevant |= any;
            }
            if (name.StartsWith("Read", StringComparison.Ordinal) || name.StartsWith("Get", StringComparison.Ordinal)
                || name is "Exists" or "OpenRead" or "OpenText" or "Refresh")
            {
                return (name is "OpenRead" or "OpenText") && relevant;
            }
            if (name is "Open" or "OpenHandle")
            {
                if (relevant && !ReadOnlyOpen(method, arguments))
                {
                    Mutation(syntax, $"{method.ContainingType.Name}.{name}", state);
                }
                return relevant;
            }
            if (name.StartsWith("Write", StringComparison.Ordinal) || name.StartsWith("Append", StringComparison.Ordinal)
                || name.StartsWith("Set", StringComparison.Ordinal) || name is "Create" or "CreateText" or "OpenWrite" or "Delete" or "Move" or "MoveTo" or "Replace" or "Copy" or "CopyTo" or "Encrypt" or "Decrypt" or "CreateSymbolicLink")
            {
                if (relevant)
                {
                    Mutation(syntax, $"{method.ContainingType.Name}.{name}", state);
                }
                return CarriesPath(method.ReturnType) && relevant;
            }
            if (relevant || any)
            {
                _violations.Add($"{Location(syntax)}: unclassified graph-path API {type}.{name}");
            }
            return false;
        }
        if (IsStream(type))
        {
            if (construction)
            {
                if (any && type != "System.IO.StreamReader" && (type != "System.IO.FileStream" || !ReadOnlyOpen(method, arguments)))
                {
                    Mutation(syntax, $"new {method.ContainingType.Name}", state);
                }
                return any;
            }
            if (method.Name.StartsWith("Read", StringComparison.Ordinal))
            {
                return false;
            }
            if (receiver && (method.Name.StartsWith("Write", StringComparison.Ordinal) || method.Name == "SetLength"))
            {
                Mutation(syntax, $"{method.ContainingType.Name}.{method.Name}", state);
            }
            return false;
        }
        if (type.StartsWith("System.Collections", StringComparison.Ordinal) && any && instance is not null)
        {
            if (IsFrameworkType(method.ContainingType, typeof(List<>)) && method.Name is "Contains" or "IndexOf")
            {
                return false; // Searching for a path does not store it in the list.
            }
            // Collection writes retain the union of their elements. Indexers
            // and enumeration read that same conservative carrier value.
            Assign(instance, true, state);
            return CarriesPath(method.ReturnType);
        }
        if (type == "System.IO.RandomAccess")
        {
            if (values.FirstOrDefault() && (method.Name.StartsWith("Write", StringComparison.Ordinal) || method.Name is "SetLength" or "FlushToDisk"))
            {
                Mutation(syntax, $"RandomAccess.{method.Name}", state);
            }
            else if (any && !method.Name.StartsWith("Read", StringComparison.Ordinal) && method.Name != "GetLength")
            {
                _violations.Add($"{Location(syntax)}: unclassified graph-path API {type}.{method.Name}");
            }
            return false;
        }
        if (type.StartsWith("System.IO.", StringComparison.Ordinal) && (any || receiver))
        {
            if (IsFrameworkType(method.ContainingType, typeof(Directory)) && method.Name == "Exists")
            {
                return false;
            }
            _violations.Add($"{Location(syntax)}: unclassified graph-path API {type}.{method.Name}");
            return CarriesPath(method.ReturnType);
        }
        // Framework string transforms retain provenance. Unknown external
        // string-returning helpers are conservative; file reads above are the
        // deliberate exception because their returned contents are not paths.
        return CarriesPath(construction ? method.ContainingType : method.ReturnType) && (receiver || any);
    }

    private static bool IsFrameworkType(INamedTypeSymbol symbol, Type frameworkType) =>
        symbol.MetadataName == frameworkType.Name && symbol.ContainingNamespace.ToDisplayString() == frameworkType.Namespace
        && symbol.ContainingAssembly.Identity.Name == frameworkType.Assembly.GetName().Name;

    private static bool ReadOnlyOpen(IMethodSymbol method, System.Collections.Immutable.ImmutableArray<IArgumentOperation> arguments)
    {
        int? mode = null;
        int? access = null;
        int options = 0;
        foreach (IArgumentOperation argument in arguments)
        {
            string? type = argument.Parameter?.Type.ToDisplayString();
            if (type is "System.IO.FileMode" or "System.IO.FileAccess" or "System.IO.FileOptions")
            {
                if (argument.Value.ConstantValue is not { HasValue: true, Value: int value })
                {
                    return false;
                }
                switch (type)
                {
                    case "System.IO.FileMode": mode = value; break;
                    case "System.IO.FileAccess": access = value; break;
                    case "System.IO.FileOptions": options = value; break;
                }
            }
            if (type == "System.IO.FileStreamOptions")
            {
                return false; // Options objects require an explicit model before admission.
            }
        }
        // Open(path, mode) and FileStream(path, mode) default to ReadWrite.
        // Handle-backed FileStream constructors have no FileMode argument.
        return access == (int)FileAccess.Read
            && (mode == (int)FileMode.Open || (mode is null && method.ContainingType.Name == "FileStream"))
            && (options & (int)FileOptions.DeleteOnClose) == 0;
    }

    private void Mutation(SyntaxNode syntax, string api, State state)
    {
        string mutation = $"{Location(syntax)}: {api}";
        _mutations.Add(mutation);
        if (!state.Authorized)
        {
            _violations.Add($"{mutation} mutates a graph config path outside GraphConfigStore.Write's call context");
        }
    }

    private static bool IsStream(string type) => type is "System.IO.FileStream" or "System.IO.StreamWriter"
        or "System.IO.StreamReader" or "System.IO.Stream" or "System.IO.TextWriter" or "System.IO.TextReader";

    private static bool CarriesPath(ITypeSymbol? type) => type is not null
        && (type.SpecialType is SpecialType.System_String or SpecialType.System_Object
            || IsStream(type.ToDisplayString()) || type.ToDisplayString() is "System.IO.FileInfo" or "Microsoft.Win32.SafeHandles.SafeFileHandle"
            || type is IArrayTypeSymbol array && CarriesPath(array.ElementType)
            || type is INamedTypeSymbol { IsTupleType: true }
            || type is INamedTypeSymbol { Name: "Task" or "ValueTask" } task && task.TypeArguments.Any(CarriesPath)
            || type is INamedTypeSymbol collection && collection.ContainingNamespace.ToDisplayString().StartsWith("System.Collections", StringComparison.Ordinal)
                && collection.TypeArguments.Any(CarriesPath)
            || type.TypeKind is TypeKind.Class or TypeKind.Struct && type.ContainingAssembly.Name == "shell-census");

    private static bool NamesGraph(string text) => text.Replace('\\', '/').Split('/').Any(component =>
        component.Equals("graph.json", StringComparison.OrdinalIgnoreCase)
        || component.StartsWith("graph.json.", StringComparison.OrdinalIgnoreCase));

    private static bool IsWriter(IMethodSymbol method) => method.Name == "Write" && method.ContainingType.ToDisplayString() == Store;

    private static bool IsFileNameInitializer(SemanticModel model, SyntaxNode node) =>
        node.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault() is { } variable
        && model.GetDeclaredSymbol(variable) is IFieldSymbol { Name: "FileName" } field
        && field.ContainingType.ToDisplayString() == Store;

    private static string Location(SyntaxNode node)
    {
        string path = node.SyntaxTree.FilePath.Replace('\\', '/');
        const string marker = "/src/SlateWindows/";
        int root = path.LastIndexOf(marker, StringComparison.Ordinal);
        string relative = root >= 0 ? path[(root + marker.Length)..] : path;
        string owner = node.AncestorsAndSelf().FirstOrDefault(ancestor => ancestor is MethodDeclarationSyntax or ConstructorDeclarationSyntax or PropertyDeclarationSyntax or LocalFunctionStatementSyntax) switch
        {
            MethodDeclarationSyntax method => method.Identifier.ValueText,
            ConstructorDeclarationSyntax => "<ctor>",
            PropertyDeclarationSyntax property => property.Identifier.ValueText,
            LocalFunctionStatementSyntax local => local.Identifier.ValueText,
            _ => "<initializer>",
        };
        return $"{relative}:{owner}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}";
    }
}
