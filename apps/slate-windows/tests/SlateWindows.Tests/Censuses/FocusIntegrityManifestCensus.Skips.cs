// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

// Plan 47 CB-17b (CB-17a's merge judge, m1; the red team's F7 on PR 2's design): a
// keys-first or stays-on witness whose fact can skip at run time witnesses nothing on a
// desktop where it skips. Such a witness is refused, unless its row is one of the menu's
// seventeen, written with the marker, whose facts skip where a desktop refuses a menu the
// keys (#1680). "Can skip" is read in this project's source, from the fact, by following
// calls by name through every helper at any depth.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

public sealed partial class FocusIntegrityManifestCensus
{
    /// <summary>A fact whose witness can skip, planted in every way the reading follows: in
    /// its own code, two helpers down, through a method group, a local function or a
    /// constructor, through a data provider's row, and a skipped or explicit inline row.
    /// Each is refused. The marker is taken only for the menu's seventeen rows, and each of
    /// them takes it.</summary>
    [Theory]
    [InlineData("keys-first: PlantedTests.SkipsItself", "Pressed", "can skip", "Assert.Skip")]
    [InlineData("keys-first: PlantedTests.SkipsTwoHelpersDown", "Pressed", "can skip", "Desk.Check")]
    [InlineData("keys-first: PlantedTests.SkipsThroughAMethodGroup", "Pressed", "can skip", "PlantedTests.Refuse")]
    [InlineData("keys-first: PlantedTests.SkipsThroughALocalFunction", "Pressed", "can skip", "PlantedTests.SkipsThroughALocalFunction.Steady")]
    [InlineData("keys-first: PlantedTests.SkipsThroughAConstructor", "Pressed", "can skip", "new Bench")]
    [InlineData("keys-first: PlantedTests.SkipsThroughAProvidersRow", "Pressed", "can skip", "PlantedTests.Rows")]
    [InlineData("keys-first: PlantedTests.HasASkippedRow", "Pressed", "skipped or explicit row", "InlineData")]
    [InlineData("keys-first: PlantedTests.HasAnExplicitRow", "Pressed", "skipped or explicit row", "InlineData")]
    [InlineData("stays-on: PlantedTests.StaysOnButSkips", "Pressed", "can skip", "Desk.Check")]
    [InlineData(MenuMarked + "PlantedTests.SkipsItself", "Pressed", "only the menu's seventeen", MenuMarked)]
    [InlineData("keys-first: PlantedTests.TheMenuOnAnyDesktop", "Palette", "one of the menu's seventeen", MenuMarked)]
    [InlineData("keys-first: PlantedTests.TheMenu", "Palette", "one of the menu's seventeen", MenuMarked)]
    [InlineData("exempt: not focusable", "Palette", "one of the menu's seventeen", MenuMarked)]
    [InlineData(MenuMarked + "PlantedTests.TheMenuWithASkippedRow", "Palette", "skipped or explicit row", "InlineData")]
    public void AWitnessThatCanSkipIsRefused(string witness, string site, string refusal, string named)
    {
        string? verdict = PlantedSkipJudge().Judge(site == "Palette" ? PlantedMenuSite : PlantedPressedSite, witness);

        Assert.True(
            verdict is not null && verdict.Contains(refusal, StringComparison.Ordinal) && verdict.Contains(named, StringComparison.Ordinal),
            $"'{witness}' on {site}: {verdict ?? "taken as a witness"}");
    }

    /// <summary>A fact that cannot skip witnesses as before, and a fact that skips where a
    /// desktop refuses a menu the keys witnesses a row of the menu's seventeen written with
    /// the marker.</summary>
    [Fact]
    public void TheMenusSeventeenMayLeanOnAFactThatSkipsWhereADesktopRefusesAMenu()
    {
        WitnessJudge judge = PlantedSkipJudge();

        Assert.Null(judge.Judge(PlantedPressedSite, "keys-first: PlantedTests.Holds"));
        Assert.Null(judge.Judge(PlantedMenuSite, MenuMarked + "PlantedTests.TheMenu"));
        Assert.Null(judge.Judge(PlantedMenuSite, MenuMarked + "PlantedTests.TheMenuOnAnyDesktop"));
    }

    /// <summary>The reading measured on this project (the PR 2 delta's m1): of every
    /// keys-first and stays-on witness the manifest names, the two that take the keys into
    /// the menu can skip, through KeysFirstRecorder's skip, and no other can.</summary>
    [Fact]
    public void OnlyTheMenusTwoWitnessesCanSkip()
    {
        var reach = new SkipReach(TestSources.Value);
        string[] witnesses =
        [
            .. Manifest().Values
                .Select(witness => new[] { "keys-first: ", "stays-on: ", MenuMarked }
                    .FirstOrDefault(kind => witness.StartsWith(kind, StringComparison.Ordinal)) is { } kind
                    ? witness[kind.Length..].Trim()
                    : null)
                .OfType<string>()
                .Distinct(StringComparer.Ordinal),
        ];
        Assert.True(witnesses.Length > 30, $"only {witnesses.Length} measured witnesses were read; the manifest's reading is broken.");

        Assert.Equal(
            ["CommandPaletteTests.TheMenuIsDisabledOnlyAfterTheKeysLeaveIt", "SheetOpeningTests.NothingHoldingTheKeysGoesOffAsASheetOpens"],
            witnesses.Where(fact => reach.Skips(fact) is not null).Order(StringComparer.Ordinal));
        Assert.Contains("KeysFirstRecorder.SkipWhereTheDesktopRefusesTheMenu", reach.Skips("SheetOpeningTests.NothingHoldingTheKeysGoesOffAsASheetOpens") ?? string.Empty, StringComparison.Ordinal);
    }

    private static readonly Site PlantedPressedSite =
        new("xaml-command Planted.xaml Pressed PressCommand", null, null, null, null, ["Pressed"], null, "PressCommand");

    private static readonly Site PlantedMenuSite =
        new("xaml-trigger MainWindow.xaml MainMenu IsEnabled when Palette.IsOpen=True", null, null, null, null, ["MainMenu"], ["Palette"]);

    private const string PlantedSkips = """
        public sealed class PlantedTests
        {
            [Fact]
            public void Holds() => RunSta(() => { var button = host.Pressed; KeysFirstRecorder.AssertTheKeysLeaveFirst("Pressed", button, () => button.Command.Execute(null)); });

            [Fact]
            public void SkipsItself() => RunSta(() =>
            {
                var button = host.Pressed;
                if (!button.IsVisible)
                {
                    Assert.Skip("this desktop shows nothing");
                }

                KeysFirstRecorder.AssertTheKeysLeaveFirst("Pressed", button, () => button.Command.Execute(null));
            });

            [Fact]
            public void SkipsTwoHelpersDown() => RunSta(() => { var button = host.Pressed; Ready(button); KeysFirstRecorder.AssertTheKeysLeaveFirst("Pressed", button, () => button.Command.Execute(null)); });

            [Fact]
            public void SkipsThroughAMethodGroup() => RunSta(() => { var button = host.Pressed; Array.ForEach([button], Refuse); KeysFirstRecorder.AssertTheKeysLeaveFirst("Pressed", button, () => button.Command.Execute(null)); });

            [Fact]
            public void SkipsThroughALocalFunction() => RunSta(() =>
            {
                var button = host.Pressed;
                void Steady()
                {
                    if (!button.IsVisible)
                    {
                        throw new SkipException("this desktop shows nothing");
                    }
                }

                Steady();
                KeysFirstRecorder.AssertTheKeysLeaveFirst("Pressed", button, () => button.Command.Execute(null));
            });

            [Fact]
            public void SkipsThroughAConstructor() => RunSta(() => { using var bench = new Bench(); var button = host.Pressed; KeysFirstRecorder.AssertTheKeysLeaveFirst("Pressed", button, () => button.Command.Execute(null)); });

            [Theory]
            [MemberData(nameof(Rows))]
            public void SkipsThroughAProvidersRow(int row) => RunSta(() => { var button = host.Pressed; KeysFirstRecorder.AssertTheKeysLeaveFirst("Pressed", button, () => button.Command.Execute(row)); });

            [Theory]
            [InlineData(1)]
            [InlineData(2, Skip = "the second row needs a desktop")]
            public void HasASkippedRow(int row) => RunSta(() => { var button = host.Pressed; KeysFirstRecorder.AssertTheKeysLeaveFirst("Pressed", button, () => button.Command.Execute(row)); });

            [Theory]
            [InlineData(1)]
            [InlineData(2, Explicit = true)]
            public void HasAnExplicitRow(int row) => RunSta(() => { var button = host.Pressed; KeysFirstRecorder.AssertTheKeysLeaveFirst("Pressed", button, () => button.Command.Execute(row)); });

            [Fact]
            public void StaysOnButSkips() => RunSta(() => { var button = host.Pressed; Ready(button); KeysFirstRecorder.AssertItStaysOn("Pressed", button, () => button.Command.Execute(null), () => host.Ran); });

            [Fact]
            public void TheMenu() => RunSta(() => { var menu = host.Shell.MainMenu; KeysFirstRecorder.TakeTheMenuKeys(menu); KeysFirstRecorder.AssertTheKeysLeaveFirst("menu", menu, () => host.Palette.Open()); });

            [Fact]
            public void TheMenuOnAnyDesktop() => RunSta(() => { var menu = host.Shell.MainMenu; KeysFirstRecorder.AssertTheKeysLeaveFirst("menu", menu, () => host.Palette.Open()); });

            [Theory]
            [InlineData(true)]
            [InlineData(false, SkipWhen = nameof(NoMenu))]
            public void TheMenuWithASkippedRow(bool open) => RunSta(() => { var menu = host.Shell.MainMenu; KeysFirstRecorder.AssertTheKeysLeaveFirst("menu", menu, () => host.Palette.Open()); });

            public static bool NoMenu => false;

            public static IEnumerable<TheoryDataRow<int>> Rows => [new(1), new TheoryDataRow<int>(2) { Skip = "the second row needs a desktop" }];

            private static void Ready(Button button) => Desk.Check(button);

            private static void Refuse(Button button) => Assert.SkipWhen(!button.IsVisible, "this desktop shows nothing");

            private sealed class Bench : IDisposable
            {
                public Bench()
                {
                    if (!Desk.Shown)
                    {
                        throw SkipException.ForSkip("this desktop shows nothing");
                    }
                }

                public void Dispose()
                {
                }
            }
        }

        internal static class Desk
        {
            internal static bool Shown => true;

            internal static void Check(Button button) => Assert.SkipUnless(button.IsVisible, "this desktop shows nothing");
        }

        internal static class KeysFirstRecorder
        {
            internal static void TakeTheMenuKeys(UIElement item)
            {
                if (!item.Focus())
                {
                    _ = SkipWhereTheDesktopRefusesTheMenu("the keys stayed where they were");
                }
            }

            internal static MenuItem SkipWhereTheDesktopRefusesTheMenu(string what)
            {
                Assert.Skip(what);
                throw new UnreachableException();
            }
        }
        """;

    /// <summary>A judge over <see cref="PlantedSkips"/>: its facts, and the skip reading over
    /// its source.</summary>
    private static WitnessJudge PlantedSkipJudge()
    {
        CompilationUnitSyntax planted = SyntaxFactory.ParseCompilationUnit(PlantedSkips);
        HashSet<string> declared =
        [
            .. planted.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Where(method => method.AttributeLists.SelectMany(list => list.Attributes).Any(attribute => attribute.Name.ToString() is "Fact" or "Theory"))
                .Select(method => $"{((TypeDeclarationSyntax)method.Parent!).Identifier.ValueText}.{method.Identifier.ValueText}"),
        ];
        return new WitnessJudge(FactSources([planted], declared), new HashSet<string>(StringComparer.Ordinal), [], skips: new SkipReach([planted]));
    }

    /// <summary>
    /// Whether a fact can skip at run time, read in source by following calls by name. A
    /// skip is <c>Assert.Skip</c>, <c>Assert.SkipUnless</c> or <c>Assert.SkipWhen</c>, called
    /// or taken as a method group, a <c>SkipException</c> created or reached through its
    /// type, or a data row made skipped or explicit (a TheoryDataRow's Skip, SkipUnless,
    /// SkipWhen, SkipType or Explicit, WithSkip or WithExplicit). A call is any name in the
    /// code that names a method or a property of this project, called or not (a method
    /// group), and any object creation (its type's constructors, field and property
    /// initializers and Dispose; a target-typed creation whose type cannot be read reaches
    /// every type's). A name is resolved within its receiver's type when the receiver names a
    /// type of this project, within the enclosing type's chain when it has no receiver, to the
    /// local functions of its member first, and otherwise to every member of that name that C#
    /// lets it reach as it is written: through an instance, an instance member or an extension
    /// method; through a type or a static import, a static member; a private member only from
    /// inside its own type. The fact starts the walk with its class's construction and
    /// disposal and its data providers.
    /// </summary>
    internal sealed class SkipReach
    {
        private static readonly string[] AssertSkips = ["Skip", "SkipUnless", "SkipWhen"];
        private static readonly string[] RowSkips = ["Skip", "SkipUnless", "SkipWhen", "SkipType", "SkipExceptions"];
        private static readonly string[] RowCalls = ["WithSkip", "WithExplicit"];
        private static readonly string[] Lifetime = ["Dispose", "DisposeAsync", "InitializeAsync"];

        private readonly Dictionary<string, List<SyntaxNode>> _members = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<TypeDeclarationSyntax>> _types = new(StringComparer.Ordinal);
        private readonly Dictionary<string, MethodDeclarationSyntax> _facts = new(StringComparer.Ordinal);
        private readonly Dictionary<SyntaxNode, (string? Skip, SyntaxNode[] Callees)> _read = [];
        private readonly Dictionary<SyntaxNode, HashSet<string>> _locals = [];
        private readonly Dictionary<string, HashSet<string>> _values = new(StringComparer.Ordinal);
        private SyntaxNode[]? _everyConstruction;

        public SkipReach(IEnumerable<CompilationUnitSyntax> roots)
        {
            foreach (SyntaxNode node in roots.SelectMany(root => root.DescendantNodes()))
            {
                switch (node)
                {
                    case TypeDeclarationSyntax type:
                        Add(_types, type.Identifier.ValueText, type);
                        break;
                    case MethodDeclarationSyntax method:
                        Add(_members, method.Identifier.ValueText, method);
                        if (method.Parent is TypeDeclarationSyntax owner
                            && method.AttributeLists.SelectMany(list => list.Attributes).Any(attribute => LastName(attribute.Name) is "Fact" or "FactAttribute" or "Theory" or "TheoryAttribute"))
                        {
                            _ = _facts.TryAdd($"{owner.Identifier.ValueText}.{method.Identifier.ValueText}", method);
                        }

                        break;
                    case PropertyDeclarationSyntax property:
                        Add(_members, property.Identifier.ValueText, property);
                        break;
                }
            }
        }

        /// <summary>The path by which <paramref name="fact"/> (Class.Method) can skip, or
        /// null when it cannot.</summary>
        public string? Skips(string fact)
        {
            if (!_facts.TryGetValue(fact, out MethodDeclarationSyntax? method))
            {
                return null;
            }

            var from = new Dictionary<SyntaxNode, SyntaxNode?>() { [method] = null };
            var pending = new Queue<SyntaxNode>([method]);
            while (pending.TryDequeue(out SyntaxNode? node))
            {
                (string? skip, SyntaxNode[] callees) = Read(node);
                if (skip is not null)
                {
                    var path = new List<string> { skip };
                    for (SyntaxNode? at = node; at is not null; at = from[at])
                    {
                        path.Insert(0, Label(at));
                    }

                    return string.Join(" → ", path);
                }

                foreach (SyntaxNode callee in ReferenceEquals(node, method) ? callees.Concat(AroundTheFact(method)) : callees)
                {
                    if (from.TryAdd(callee, node))
                    {
                        pending.Enqueue(callee);
                    }
                }
            }

            return null;
        }

        /// <summary>A data row of <paramref name="fact"/> that is skipped or explicit: an
        /// attribute other than Fact or Theory (InlineData, MemberData, ClassData) given one
        /// of the skip arguments, or Explicit not false. Null when there is none.</summary>
        public string? SkippedRow(string fact) =>
            _facts.TryGetValue(fact, out MethodDeclarationSyntax? method)
                ? method.AttributeLists.SelectMany(list => list.Attributes)
                    .Where(attribute => LastName(attribute.Name) is not ("Fact" or "FactAttribute" or "Theory" or "TheoryAttribute"))
                    .FirstOrDefault(attribute => attribute.ArgumentList?.Arguments.Any(argument => argument.NameEquals?.Name.Identifier.ValueText switch
                    {
                        { } name when RowSkips.Contains(name) => !argument.Expression.IsKind(SyntaxKind.NullLiteralExpression),
                        "Explicit" => !argument.Expression.IsKind(SyntaxKind.FalseLiteralExpression),
                        _ => false,
                    }) == true)?.ToString() is { } row
                    ? $"[{row}]"
                    : null
                : null;

        private static void Add<T>(Dictionary<string, List<T>> index, string name, T node)
        {
            if (!index.TryGetValue(name, out List<T>? named))
            {
                named = [];
                index[name] = named;
            }

            named.Add(node);
        }

        /// <summary>What xunit runs around a fact: its class's construction and disposal,
        /// and the providers of its data rows.</summary>
        private IEnumerable<SyntaxNode> AroundTheFact(MethodDeclarationSyntax method)
        {
            TypeDeclarationSyntax owner = (TypeDeclarationSyntax)method.Parent!;
            foreach (SyntaxNode part in TypeParts(owner.Identifier.ValueText))
            {
                yield return part;
            }

            foreach (AttributeSyntax attribute in method.AttributeLists.SelectMany(list => list.Attributes))
            {
                SeparatedSyntaxList<AttributeArgumentSyntax> arguments = attribute.ArgumentList?.Arguments ?? default;
                switch (LastName(attribute.Name))
                {
                    case "MemberData" or "MemberDataAttribute" when arguments.Count > 0:
                        string? provider = arguments[0].Expression switch
                        {
                            LiteralExpressionSyntax literal => literal.Token.ValueText,
                            InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" }, ArgumentList.Arguments: [var named] }
                                => LastName(named.Expression),
                            _ => null,
                        };
                        string? holder = arguments.FirstOrDefault(argument => argument.NameEquals?.Name.Identifier.ValueText == "MemberType")?.Expression is TypeOfExpressionSyntax typed
                            ? LastName(typed.Type)
                            : owner.Identifier.ValueText;
                        if (provider is not null)
                        {
                            foreach (SyntaxNode member in TypeMembers(holder ?? owner.Identifier.ValueText, provider) ?? All(provider, method, Access.Type))
                            {
                                yield return member;
                            }
                        }

                        break;
                    case "ClassData" or "ClassDataAttribute" when arguments.Count > 0 && arguments[0].Expression is TypeOfExpressionSyntax data && LastName(data.Type) is { } rows:
                        foreach (SyntaxNode member in TypeParts(rows).Concat(TypeMembers(rows, "GetEnumerator") ?? []))
                        {
                            yield return member;
                        }

                        break;
                }
            }
        }

        /// <summary>A node's first skip and the nodes it calls, read once.</summary>
        private (string? Skip, SyntaxNode[] Callees) Read(SyntaxNode node)
        {
            if (_read.TryGetValue(node, out (string? Skip, SyntaxNode[] Callees) known))
            {
                return known;
            }

            string? skip = null;
            var callees = new List<SyntaxNode>();
            // A local function is read as a call of its own (its name resolves to it), a nested
            // type only when it is made, and a nameof or an attribute calls nothing.
            foreach (SyntaxNode at in node.DescendantNodesAndSelf(child => ReferenceEquals(child, node)
                || (child is not (LocalFunctionStatementSyntax or AttributeListSyntax or BaseTypeDeclarationSyntax) && !IsNameof(child))))
            {
                skip ??= SkipAt(at);
                switch (at)
                {
                    case SimpleNameSyntax name when !ReferenceEquals(at, node):
                        callees.AddRange(Resolve(name));
                        break;
                    case BaseObjectCreationExpressionSyntax creation:
                        callees.AddRange(Construct(creation));
                        break;
                }
            }

            known = (skip, [.. callees.Distinct()]);
            _read[node] = known;
            return known;
        }

        /// <summary>The skip a node makes itself, or null.</summary>
        private string? SkipAt(SyntaxNode node) => node switch
        {
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: var name, Expression: var owner } when AssertSkips.Contains(name) && LastName(owner) == "Assert"
                => $"Assert.{name}",
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: var name, Expression: var owner } when LastName(owner) == "SkipException"
                => $"SkipException.{name}",
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: var name } when RowCalls.Contains(name)
                => $"a data row's {name}",
            ObjectCreationExpressionSyntax { Type: var type } when LastName(type) == "SkipException"
                => "new SkipException",
            InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: var name } } when AssertSkips.Contains(name) && !_members.ContainsKey(name)
                => $"{name} (Assert's, imported)",
            AssignmentExpressionSyntax { Left: IdentifierNameSyntax { Identifier.ValueText: var name }, Right: var value, Parent: InitializerExpressionSyntax { Parent: BaseObjectCreationExpressionSyntax made } }
                when (made is ImplicitObjectCreationExpressionSyntax
                        || (made is ObjectCreationExpressionSyntax { Type: var type } && type.ToString().Contains("TheoryDataRow", StringComparison.Ordinal)))
                    && ((RowSkips.Contains(name) && !value.IsKind(SyntaxKind.NullLiteralExpression))
                        || (name == "Explicit" && !value.IsKind(SyntaxKind.FalseLiteralExpression)))
                => $"a data row's {name}",
            _ => null,
        };

        private static bool IsNameof(SyntaxNode node) =>
            node is InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" } };

        /// <summary>The members a name can call.</summary>
        private IEnumerable<SyntaxNode> Resolve(SimpleNameSyntax name)
        {
            string text = name.Identifier.ValueText;
            SyntaxNode[] locals = [.. LocalFunctions(name, text)];
            if (!_members.ContainsKey(text) && locals.Length == 0)
            {
                return [];
            }

            return name.Parent switch
            {
                MemberAccessExpressionSyntax access when ReferenceEquals(access.Name, name) => access.Expression switch
                {
                    ThisExpressionSyntax or BaseExpressionSyntax => InTypeChain(name, text) ?? All(text, name, Access.Instance),
                    var owner => Qualifier(owner, name) switch
                    {
                        (Access.Type, { } typeName) => TypeMembers(typeName, text)?.Where(member => Reachable(member, name)).ToArray() is { Length: > 0 } members
                            ? members
                            : All(text, name, Access.Type),
                        (var how, _) => All(text, name, how),
                    },
                },
                MemberBindingExpressionSyntax => All(text, name, Access.Instance),
                QualifiedNameSyntax => [],
                _ when locals.Length > 0 => locals,
                _ => InTypeChain(name, text) ?? All(text, name, Access.Imported),
            };
        }

        /// <summary>How a name reaches a member of another type: through an instance, through a
        /// type of this project, through a type this project does not declare (only an
        /// extension method of this project can be reached so), or unqualified, by a static
        /// import.</summary>
        private enum Access
        {
            Instance,
            Type,
            Foreign,
            Imported,
        }

        /// <summary>What a member access's qualifier is: a type of this project (with its
        /// name), a type it does not declare, or a value. A qualifier whose first name is a
        /// local, a parameter, or a field or property of the types around it is a value; else
        /// its last name is a type of this project, or, written as capitalised names, a type
        /// this project does not declare.</summary>
        private (Access Access, string? Type) Qualifier(ExpressionSyntax owner, SyntaxNode at)
        {
            ExpressionSyntax first = owner;
            while (first is MemberAccessExpressionSyntax { Expression: var inner })
            {
                first = inner;
            }

            if (first is not SimpleNameSyntax { Identifier.ValueText: var leading } || IsAValue(leading, at))
            {
                return (Access.Instance, null);
            }

            if (LastName(owner) is { } last && _types.ContainsKey(last))
            {
                return (Access.Type, last);
            }

            bool named = owner.DescendantNodesAndSelf().OfType<SimpleNameSyntax>().All(segment => char.IsUpper(segment.Identifier.ValueText[0]));
            return named ? (Access.Foreign, null) : (Access.Instance, null);
        }

        /// <summary>Whether <paramref name="name"/>, where <paramref name="at"/> is, is a
        /// local or a parameter of its member (a lambda's and a pattern's included), or a field,
        /// a property or a primary constructor's parameter of a type around it.</summary>
        private bool IsAValue(string name, SyntaxNode at)
        {
            if (at.Ancestors().OfType<MemberDeclarationSyntax>().FirstOrDefault(member => member is not BaseTypeDeclarationSyntax) is { } member)
            {
                if (!_locals.TryGetValue(member, out HashSet<string>? locals))
                {
                    locals = new HashSet<string>(
                        member.DescendantNodesAndSelf().Select(node => node switch
                        {
                            VariableDeclaratorSyntax variable => variable.Identifier.ValueText,
                            ParameterSyntax parameter => parameter.Identifier.ValueText,
                            SingleVariableDesignationSyntax designation => designation.Identifier.ValueText,
                            ForEachStatementSyntax loop => loop.Identifier.ValueText,
                            CatchDeclarationSyntax caught => caught.Identifier.ValueText,
                            _ => null,
                        }).OfType<string>(),
                        StringComparer.Ordinal);
                    _locals[member] = locals;
                }

                if (locals.Contains(name))
                {
                    return true;
                }
            }

            return at.Ancestors().OfType<TypeDeclarationSyntax>().Any(type => ValuesOf(type.Identifier.ValueText).Contains(name));
        }

        /// <summary>The fields, properties and primary constructor parameters of every type of
        /// this project named <paramref name="type"/> and of its base types, read once.</summary>
        private HashSet<string> ValuesOf(string type)
        {
            if (!_values.TryGetValue(type, out HashSet<string>? values))
            {
                values = new HashSet<string>(
                    Lineage(type).SelectMany(declared => (declared.ParameterList?.Parameters.Select(parameter => parameter.Identifier.ValueText) ?? [])
                        .Concat(declared.Members.SelectMany(member => member switch
                        {
                            FieldDeclarationSyntax field => field.Declaration.Variables.Select(variable => variable.Identifier.ValueText),
                            PropertyDeclarationSyntax property => [property.Identifier.ValueText],
                            _ => [],
                        }))),
                    StringComparer.Ordinal);
                _values[type] = values;
            }

            return values;
        }

        /// <summary>Every member of this project named <paramref name="name"/> that C# lets
        /// <paramref name="at"/> reach the way it is written: through an instance only an
        /// instance member or an extension method, through a type this project does not
        /// declare only an extension method, through a type or a static import only a static
        /// member, and a private member only from inside its own type.</summary>
        private IEnumerable<SyntaxNode> All(string name, SyntaxNode at, Access access) =>
            (_members.GetValueOrDefault(name) ?? []).Where(member =>
            {
                SyntaxTokenList modifiers = member switch
                {
                    MethodDeclarationSyntax method => method.Modifiers,
                    PropertyDeclarationSyntax property => property.Modifiers,
                    _ => default,
                };
                bool isStatic = modifiers.Any(SyntaxKind.StaticKeyword);
                bool extension = member is MethodDeclarationSyntax { ParameterList.Parameters: [var first, ..] } && first.Modifiers.Any(SyntaxKind.ThisKeyword);
                bool reachable = access switch
                {
                    Access.Instance => !isStatic || extension,
                    Access.Foreign => extension,
                    Access.Type => isStatic,
                    _ => isStatic && !extension,
                };
                return reachable && Reachable(member, at);
            });

        /// <summary>Whether <paramref name="at"/> may see <paramref name="declared"/> (a member
        /// or a type): a private one, or one inside a private type, only from inside the type
        /// that holds it. A member or a nested type with no accessibility written is
        /// private.</summary>
        private static bool Reachable(SyntaxNode declared, SyntaxNode at)
        {
            HashSet<string> around = [.. at.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().Select(type => type.Identifier.ValueText)];
            for (SyntaxNode node = declared; node.Parent is TypeDeclarationSyntax holder; node = holder)
            {
                SyntaxTokenList modifiers = node switch
                {
                    MemberDeclarationSyntax member => member.Modifiers,
                    _ => default,
                };
                bool unstated = !modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PublicKeyword) || modifier.IsKind(SyntaxKind.InternalKeyword)
                    || modifier.IsKind(SyntaxKind.ProtectedKeyword) || modifier.IsKind(SyntaxKind.PrivateKeyword));
                bool isPrivate = (modifiers.Any(SyntaxKind.PrivateKeyword) && !modifiers.Any(SyntaxKind.ProtectedKeyword))
                    || (unstated && holder is not InterfaceDeclarationSyntax);
                if (isPrivate && !around.Contains(holder.Identifier.ValueText))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The local functions named <paramref name="name"/> in the member that
        /// holds <paramref name="at"/>.</summary>
        private static IEnumerable<SyntaxNode> LocalFunctions(SyntaxNode at, string name) =>
            at.Ancestors().OfType<MemberDeclarationSyntax>().FirstOrDefault(member => member is not BaseTypeDeclarationSyntax) is { } member
                ? member.DescendantNodes().OfType<LocalFunctionStatementSyntax>().Where(local => local.Identifier.ValueText == name)
                : [];

        /// <summary>The members named <paramref name="name"/> of the types around
        /// <paramref name="at"/>, innermost first, with their partial parts and base types; null
        /// when none has one.</summary>
        private SyntaxNode[]? InTypeChain(SyntaxNode at, string name)
        {
            SyntaxNode[] found =
            [
                .. at.Ancestors().OfType<TypeDeclarationSyntax>()
                    .SelectMany(type => TypeMembers(type.Identifier.ValueText, name) ?? []),
            ];
            return found.Length > 0 ? found : null;
        }

        /// <summary>The methods and properties named <paramref name="name"/> declared in every
        /// type of this project named <paramref name="type"/> and in their base types; null
        /// when none declares one.</summary>
        private SyntaxNode[]? TypeMembers(string type, string name)
        {
            SyntaxNode[] found =
            [
                .. Lineage(type).SelectMany(declared => declared.Members)
                    .Where(member => member switch
                    {
                        MethodDeclarationSyntax method => method.Identifier.ValueText == name,
                        PropertyDeclarationSyntax property => property.Identifier.ValueText == name,
                        _ => false,
                    }),
            ];
            return found.Length > 0 ? found : null;
        }

        /// <summary>Every declaration of the types named <paramref name="type"/> (those
        /// <paramref name="at"/> may see, when it is given), and of their base types this project
        /// declares.</summary>
        private IEnumerable<TypeDeclarationSyntax> Lineage(string type, SyntaxNode? at = null)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<string>([type]);
            while (pending.TryPop(out string? named))
            {
                if (!seen.Add(named))
                {
                    continue;
                }

                foreach (TypeDeclarationSyntax declared in (_types.GetValueOrDefault(named) ?? [])
                    .Where(declared => at is null || named != type || Reachable(declared, at)))
                {
                    yield return declared;
                    foreach (BaseTypeSyntax inherited in declared.BaseList?.Types ?? default)
                    {
                        if (LastName(inherited.Type) is { } baseName && _types.ContainsKey(baseName))
                        {
                            pending.Push(baseName);
                        }
                    }
                }
            }
        }

        /// <summary>What an object creation runs: its type's construction and disposal (none
        /// for a type this project does not declare), or, for a target-typed creation whose type
        /// cannot be read, every type's it may see.</summary>
        private IEnumerable<SyntaxNode> Construct(BaseObjectCreationExpressionSyntax creation)
        {
            TypeSyntax? made = creation switch
            {
                ObjectCreationExpressionSyntax explicitly => explicitly.Type,
                ImplicitObjectCreationExpressionSyntax implicitly => TargetOf(implicitly),
                _ => null,
            };
            return made is null
                ? EveryConstruction().Where(part => Reachable(part.Ancestors().OfType<TypeDeclarationSyntax>().First(), creation))
                : LastName(made) is { } type ? TypeParts(type, creation) : [];
        }

        /// <summary>The type a target-typed creation makes, where its declaration says it.</summary>
        private static TypeSyntax? TargetOf(ImplicitObjectCreationExpressionSyntax creation)
        {
            TypeSyntax? declared = creation.Parent switch
            {
                EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration } } => declaration.Type,
                EqualsValueClauseSyntax { Parent: PropertyDeclarationSyntax property } => property.Type,
                EqualsValueClauseSyntax { Parent: ParameterSyntax parameter } => parameter.Type,
                ArrowExpressionClauseSyntax { Parent: PropertyDeclarationSyntax property } => property.Type,
                ArrowExpressionClauseSyntax { Parent: MethodDeclarationSyntax method } => method.ReturnType,
                _ => null,
            };
            return declared is IdentifierNameSyntax { IsVar: true } ? null : declared;
        }

        /// <summary>A type's construction and disposal: its constructors, its field and
        /// property initializers, its Dispose, DisposeAsync and InitializeAsync, and its base
        /// types' in this project.</summary>
        private IEnumerable<SyntaxNode> TypeParts(string type, SyntaxNode? at = null)
        {
            static IEnumerable<SyntaxNode> Parts(MemberDeclarationSyntax member) => member switch
            {
                ConstructorDeclarationSyntax constructor => [constructor],
                FieldDeclarationSyntax field => field.Declaration.Variables.Select(variable => variable.Initializer).OfType<SyntaxNode>(),
                PropertyDeclarationSyntax { Initializer: { } initializer } => [initializer],
                MethodDeclarationSyntax method when Lifetime.Contains(method.Identifier.ValueText) => [method],
                _ => [],
            };

            return Lineage(type, at).SelectMany(declared => declared.Members.SelectMany(Parts));
        }

        private SyntaxNode[] EveryConstruction() =>
            _everyConstruction ??= [.. _types.Keys.SelectMany(type => TypeParts(type)).Distinct()];

        /// <summary>A node as a path names it.</summary>
        private static string Label(SyntaxNode node) => node switch
        {
            MethodDeclarationSyntax method => $"{Owner(method)}.{method.Identifier.ValueText}",
            PropertyDeclarationSyntax property => $"{Owner(property)}.{property.Identifier.ValueText}",
            ConstructorDeclarationSyntax constructor => $"new {constructor.Identifier.ValueText}",
            LocalFunctionStatementSyntax local => local.Ancestors().OfType<MemberDeclarationSyntax>().FirstOrDefault(member => member is not BaseTypeDeclarationSyntax) is { } member
                ? $"{Label(member)}.{local.Identifier.ValueText}"
                : local.Identifier.ValueText,
            EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax variable } initializer => $"new {Owner(initializer)} ({variable.Identifier.ValueText}'s initializer)",
            EqualsValueClauseSyntax { Parent: PropertyDeclarationSyntax property } initializer => $"new {Owner(initializer)} ({property.Identifier.ValueText}'s initializer)",
            _ => node.ToString(),
        };

        private static string Owner(SyntaxNode node) =>
            node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText ?? "(no type)";

        /// <summary>The last simple name of a name, a type or a member access.</summary>
        private static string? LastName(SyntaxNode node) => node switch
        {
            SimpleNameSyntax simple => simple.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax aliased => aliased.Name.Identifier.ValueText,
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            NullableTypeSyntax nullable => LastName(nullable.ElementType),
            _ => null,
        };
    }
}
