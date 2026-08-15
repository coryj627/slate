// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;
using System.Xml.Linq;
using SlateWindows.Commands;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W5-1 (#741) command-drift twins, contract P13.
/// </summary>
/// <remarks>
/// <para>
/// Two of the three directions live here. <c>ChordTableTests</c> owns
/// P13(c) — the chord table against the shipped binding surface, both
/// ways — and <c>CommandRegistrationTests</c> owns the catalog-to-registry
/// set equality. What was missing is the pair that needs the PALETTE and
/// the MENU rather than the table: whether every registered command can
/// actually be reached, and whether every menu affordance is backed by a
/// registered command.
/// </para>
/// <para>
/// Per the owner's 2026-08-13 call, the <c>docs/help/</c> per-platform
/// chord tables and their drift test stay with #756 (W8-6), which owns
/// docs wholesale.
/// </para>
/// </remarks>
public sealed class CommandDriftTests
{
    /// <summary>
    /// P13(a): the palette is the universal surface. A command the bridge
    /// registers but the palette never lists is unreachable for anyone
    /// who does not already know its chord — which is the whole reason
    /// the palette exists.
    /// </summary>
    [Fact]
    public void EveryRegisteredCommandIsReachableThroughThePalette()
    {
        Command[] registered = ChordTable.RegisteredRows
            .Select(row => new Command(
                row.Id,
                row.Label,
                row.Hint,
                row.WindowsChord,
                row.Section))
            .ToArray();

        // The empty query is the reachability case: it is the state the
        // palette opens in, and it is the only one that must show
        // everything.
        PaletteSection[] sections = SlateUniffiMethods.PaletteSections(
            registered,
            string.Empty,
            [],
            ChordTable.SidebarPinnedOrder);

        HashSet<string> reachable = sections
            .SelectMany(section => section.Rows)
            .Select(row => row.Command.Id)
            .ToHashSet(StringComparer.Ordinal);

        string[] unreachable = registered
            .Select(command => command.Id)
            .Where(id => !reachable.Contains(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            unreachable.Length == 0,
            "registered commands the palette never lists: "
            + string.Join(", ", unreachable));

        // Guard against the assertion passing vacuously on an empty
        // catalog — the failure mode mac's tests explicitly defend.
        Assert.NotEmpty(registered);
        Assert.Equal(registered.Length, reachable.Count);
    }

    /// <summary>
    /// P13(b): every menu affordance is backed by a registered command,
    /// so no verb exists outside the command system.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Windows twin cannot be mac's exact test. Mac reads one
    /// <c>keyboardShortcut(...)</c> construct that carries both the
    /// display and the delivery; WPF splits them, and its menu binds an
    /// <c>ICommand</c> property path rather than a command id. The link
    /// is therefore the resolver table: a menu item bound to
    /// <c>Workspace.SplitRightCommand</c> is backed iff some registered
    /// id resolves to that same path.
    /// </para>
    /// <para>
    /// Both sides are read from source rather than from a live tree,
    /// which is the same idiom mac's drift tests use and the reason each
    /// scrape asserts it found something before comparing.
    /// </para>
    /// </remarks>
    [Fact]
    public void EveryMenuItemIsBackedByARegisteredCommand()
    {
        HashSet<string> menuPaths = MenuCommandPaths();
        HashSet<string> resolvedPaths = ResolverCommandPaths();

        Assert.NotEmpty(menuPaths);
        Assert.NotEmpty(resolvedPaths);

        // Tripwire for the scrape itself. The first version of this test
        // matched only one level of indirection and reported twelve
        // registered commands as unbacked — a non-empty result is not
        // evidence the scrape saw everything, so pin one shallow and one
        // deep path explicitly. Under-matching now fails here, naming the
        // cause, instead of surfacing as phantom drift.
        Assert.Contains("OpenVaultCommand", resolvedPaths);
        Assert.Contains("Workspace.EditorPreferences.ZoomInCommand", resolvedPaths);
        Assert.Contains(
            "Workspace.ActiveGroup.ActiveTab.EditorInteractions.ActivateAtCaretCommand",
            resolvedPaths);

        string[] unbacked = menuPaths
            .Where(path => !resolvedPaths.Contains(path))
            .Where(path => !CommandlessMenuLeaves.ContainsKey(path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            unbacked.Length == 0,
            "menu items bound to commands no registered id resolves: "
            + string.Join(", ", unbacked)
            + ". Register the capability, or record it in "
            + nameof(CommandlessMenuLeaves) + " with a reason.");
    }

    /// <summary>
    /// Each menu accelerator names the command that same item invokes.
    /// </summary>
    /// <remarks>
    /// Resolving accelerators from the chord table closed the
    /// hand-typed-string hazard, but not the identity one: a
    /// <c>{cmd:ChordText …}</c> referencing any OTHER valid chorded id
    /// still parses, still renders, and still passes a check that only
    /// asks "does this id exist". Swapping two items' ids, or reusing one
    /// twice, would advertise the wrong shortcut in the menu and in UIA
    /// while every gate stayed green — the identity-loss sibling of the
    /// bare-string shielding this replaced. Codex found it; this pins the
    /// pairing.
    /// </remarks>
    [Fact]
    public void EveryMenuAcceleratorNamesTheCommandThatItemInvokes()
    {
        XElement menu = MenuElement();
        HashSet<string> resolvedPaths = ResolverCommandPaths();
        Dictionary<string, string> idByPath = ResolverIdsByCommandPath();
        Assert.NotEmpty(idByPath);

        var checkedItems = 0;
        foreach (XElement item in menu.DescendantsAndSelf()
            .Where(element => element.Name.LocalName == "MenuItem"))
        {
            string? accelerator = item.Attribute("InputGestureText")?.Value;
            if (string.IsNullOrEmpty(accelerator))
            {
                continue;
            }

            Match reference = Regex.Match(
                accelerator, @"^\{cmd:ChordText\s+([A-Za-z0-9_.]+)\}$");
            Assert.True(
                reference.Success,
                $"menu accelerator '{accelerator}' is authored literally rather "
                + "than resolved from the chord table (PINV-5).");
            string acceleratorId = reference.Groups[1].Value;

            // The id this item actually invokes, via its Command binding or
            // its allow-listed click handler.
            string? invokedId = null;
            Match binding = Regex.Match(
                item.Attribute("Command")?.Value ?? string.Empty,
                @"^\{Binding ([A-Za-z0-9_.]+)\}$");
            if (binding.Success)
            {
                idByPath.TryGetValue(binding.Groups[1].Value, out invokedId);
            }
            else if (item.Attribute("Click")?.Value is { } handler)
            {
                invokedId = ClickHandlerId(handler);
            }

            Assert.True(
                invokedId is not null,
                $"menu item advertising '{acceleratorId}' has no resolvable "
                + "command, so its accelerator cannot be checked against what "
                + "it invokes.");

            Assert.True(
                string.Equals(invokedId, acceleratorId, StringComparison.Ordinal),
                $"menu item invokes '{invokedId}' but advertises the chord of "
                + $"'{acceleratorId}'. The accelerator must name the command "
                + "that item runs.");
            checkedItems++;
        }

        // Guard against a scrape that quietly matches nothing.
        Assert.True(checkedItems >= 20, $"only {checkedItems} accelerators checked");
        Assert.NotEmpty(resolvedPaths);
    }

    /// <summary>
    /// The two menu items whose command is reached through a click handler
    /// rather than a binding: the id each is taken to invoke, and the call
    /// in its body that has to be there for that to be true.
    /// </summary>
    /// <remarks>
    /// Codex's round-3 high. The first version of this was a bare
    /// name → id dictionary, which asserted nothing about the handler:
    /// point <c>QuickOpen_Click</c> at <c>_viewModel.Palette.Open()</c> and
    /// the menu item advertises Quick Open's chord while opening the
    /// command palette, with the gate still green because the id was
    /// hard-coded beside the name. The marker is still a proxy — it reads
    /// the call, not the runtime effect — but it is a CHECKED proxy, and it
    /// fails on exactly that substitution.
    /// </remarks>
    private sealed record ClickHandlerCommand(string Id, string BodyMarker);

    private static readonly Dictionary<string, ClickHandlerCommand> ClickHandlerCommandIds =
        new(StringComparer.Ordinal)
        {
            ["QuickOpen_Click"] = new("slate.workspace.quickOpen", "switcher.Open()"),
            ["FocusFilter_Click"] = new("slate.sidebar.focusFilter", "SidebarFilterTextBox.Focus()"),
        };

    /// <summary>
    /// Each allow-listed click handler still contains the call the
    /// allow-list credits it with.
    /// </summary>
    private static string? ClickHandlerId(string handler)
    {
        if (!ClickHandlerCommandIds.TryGetValue(handler, out ClickHandlerCommand? entry))
        {
            return null;
        }

        string source = SourceText.WithoutComments(
            File.ReadAllText(Path.Combine(SourceRoot(), "MainWindow.xaml.cs")));
        MatchCollection declarations = Regex.Matches(
            source,
            $@"\r?\n    private void {Regex.Escape(handler)}\s*\(");
        Assert.True(
            declarations.Count > 0,
            $"{handler} is gone from MainWindow.xaml.cs, but the menu still "
            + "names it and this allow-list still credits it with "
            + $"{entry.Id}.");

        // An unused overload declared first, carrying the marker, would
        // certify a handler that does something else entirely — the menu
        // then advertises one command's chord while running another,
        // which is the very defect this gate was added to catch.
        Assert.True(
            declarations.Count == 1,
            $"{handler} has {declarations.Count} declarations. XAML binds one of "
            + "them and this gate reads the first — disambiguate before adding "
            + "an overload.");

        Match declaration = declarations[0];
        int start = declaration.Index + declaration.Length;
        Match next = Regex.Match(
            source[start..],
            @"\r?\n    (?:private|internal|public|protected)[ \r\n]");
        string body = next.Success ? source.Substring(start, next.Index) : source[start..];

        Assert.True(
            body.Contains(entry.BodyMarker, StringComparison.Ordinal),
            $"{handler} no longer calls {entry.BodyMarker}, so the claim that it "
            + $"invokes {entry.Id} is unfounded — the menu item would advertise "
            + "one command's chord while running another.");
        return entry.Id;
    }

    /// <summary>
    /// Maps each resolver's <c>ICommand</c> property path to the command
    /// id that resolves to it.
    /// </summary>
    private static Dictionary<string, string> ResolverIdsByCommandPath()
    {
        string source = SourceText.WithoutComments(File.ReadAllText(
            Path.Combine(SourceRoot(), "Commands", "SlateCommandRegistrar.cs")));
        string flattened = Regex.Replace(source, @"\s+", string.Empty).Replace("?", string.Empty);

        string table = SourceText.WithoutComments(File.ReadAllText(
            Path.Combine(SourceRoot(), "Commands", "ChordTable.cs")));
        var idByName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(
            table, @"(?:public|internal) const string (\w+)\s*=\s*""([^""]+)"""))
        {
            idByName[match.Groups[1].Value] = match.Groups[2].Value;
        }

        var byPath = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(
            flattened, @"\[ChordTable\.Ids\.(\w+)\]=host=>host\.([A-Za-z0-9_.]*?Command)\b"))
        {
            if (idByName.TryGetValue(match.Groups[1].Value, out string? id))
            {
                byPath[match.Groups[2].Value] = id;
            }
        }

        return byPath;
    }

    /// <summary>
    /// The allow-list is checked for staleness in both directions. Mac's
    /// rule, and what makes an allow-list non-bypassable: an entry that
    /// no longer matches a live menu leaf would silently shield the next
    /// unbacked item added in its place.
    /// </summary>
    [Fact]
    public void TheCommandlessMenuAllowListIsNeitherStaleNorUnexplained()
    {
        HashSet<string> menuPaths = MenuCommandPaths();
        HashSet<string> clickHandlers = MenuClickHandlers();
        HashSet<string> resolvedPaths = ResolverCommandPaths();

        foreach ((string key, string reason) in CommandlessMenuLeaves)
        {
            Assert.False(
                string.IsNullOrWhiteSpace(reason),
                $"allow-listed menu leaf {key} carries no reason.");

            Assert.True(
                menuPaths.Contains(key) || clickHandlers.Contains(key),
                $"allow-list entry {key} matches no menu leaf — it is stale "
                + "and must be removed.");

            Assert.False(
                resolvedPaths.Contains(key),
                $"allow-list entry {key} IS now resolved by a registered "
                + "command — drop the entry rather than letting it mask the "
                + "next unbacked leaf.");
        }
    }

    /// <summary>
    /// Menu leaves that legitimately carry no registered command, each
    /// with the reason. Keyed by the binding path or the
    /// <c>Click</c> handler name.
    /// </summary>
    private static readonly Dictionary<string, string> CommandlessMenuLeaves =
        new(StringComparer.Ordinal)
        {
            ["QuickOpen_Click"] =
                "Quick Open's menu item predates the view model's OpenCommand and "
                + "routes through code-behind; the chord already goes through the "
                + "registry. Converging the menu item is W5-2's, where the overlay "
                + "is reworked.",
            ["FocusFilter_Click"] =
                "slate.sidebar.focusFilter has no ICommand at all — the chord and "
                + "the menu item both move focus into a TextBox the shell owns. "
                + "Registering it needs a focus seam on ISlateCommandHost; recorded "
                + "in the chord table as unregistered with the same reason.",
            ["ApplicationCommands.Close"] =
                "Exit is the WPF application command, deliberately not a Slate verb "
                + "— mac's Quit is likewise outside SlateCommandID.",

            // The parameterized preference setters (PR-4). Each menu item
            // supplies a CommandParameter naming the value it selects
            // ("clearSpeak", "terse", "nemeth", "preambleFirstLine"), which a
            // palette row has no way to provide — a bare "Set Math Speech
            // Style" command could not know WHICH style. Verified against
            // mac: SlateCommands.swift declares no id for any of these four,
            // so there is no twin to be out of parity with.
            ["Workspace.EditorPreferences.SetMathSpeechStyleCommand"] =
                "Parameterized: the menu passes the style to select.",
            ["Workspace.EditorPreferences.SetMathVerbosityCommand"] =
                "Parameterized: the menu passes the verbosity to select.",
            ["Workspace.EditorPreferences.SetMathBrailleCodeCommand"] =
                "Parameterized: the menu passes the braille code to select.",
            ["Workspace.EditorPreferences.SetCodePreambleVerbosityCommand"] =
                "Parameterized: the menu passes the preamble verbosity to select.",
        };

    /// <summary>
    /// Every <c>Command="{Binding path}"</c> under the menu bar. Scoped to
    /// the <c>Menu</c> subtree so overlay bindings elsewhere in the window
    /// cannot leak in.
    /// </summary>
    private static HashSet<string> MenuCommandPaths()
    {
        XElement menu = MenuElement();
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (XElement item in menu.DescendantsAndSelf()
            .Where(element => element.Name.LocalName == "MenuItem"))
        {
            string? command = item.Attribute("Command")?.Value;
            if (string.IsNullOrEmpty(command))
            {
                continue;
            }

            Match binding = Regex.Match(command, @"^\{Binding\s+([A-Za-z0-9_.]+)\}$");
            if (binding.Success)
            {
                paths.Add(binding.Groups[1].Value);
                continue;
            }

            // {x:Static ApplicationCommands.Close} and friends.
            Match statik = Regex.Match(command, @"^\{x:Static\s+([A-Za-z0-9_.]+)\}$");
            if (statik.Success)
            {
                paths.Add(statik.Groups[1].Value);
            }
        }

        return paths;
    }

    private static HashSet<string> MenuClickHandlers()
    {
        XElement menu = MenuElement();
        return menu.DescendantsAndSelf()
            .Where(element => element.Name.LocalName == "MenuItem")
            .Select(element => element.Attribute("Click")?.Value)
            .Where(value => !string.IsNullOrEmpty(value))
            .Select(value => value!)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static XElement MenuElement()
    {
        XDocument document = XDocument.Load(Path.Combine(SourceRoot(), "MainWindow.xaml"));
        XElement? menu = document.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "Menu");
        return menu ?? throw new InvalidOperationException(
            "MainWindow.xaml has no Menu element — the menu-scrape twin has "
            + "nothing to read. Fix the scrape rather than letting the drift "
            + "check pass on an empty parse.");
    }

    /// <summary>
    /// The <c>ICommand</c> property paths the registrar resolves, read
    /// from its resolver table: <c>host =&gt; host.Workspace?.XCommand</c>
    /// yields <c>Workspace.XCommand</c>, and <c>host =&gt;
    /// host.OpenVaultCommand</c> yields <c>OpenVaultCommand</c>.
    /// </summary>
    private static HashSet<string> ResolverCommandPaths()
    {
        string source = SourceText.WithoutComments(File.ReadAllText(
            Path.Combine(SourceRoot(), "Commands", "SlateCommandRegistrar.cs")));

        // Normalise before matching, because the resolvers are not uniform:
        // depth varies (host.OpenVaultCommand through
        // host.Workspace?.ActiveGroup?.ActiveTab?.EditorInteractions?.X),
        // null-conditionals appear only where the link is nullable, and long
        // lambdas wrap across lines. Collapsing whitespace and dropping '?'
        // makes every shape one dotted path.
        string flattened = Regex.Replace(source, @"\s+", string.Empty).Replace("?", string.Empty);

        return Regex.Matches(flattened, @"host\.([A-Za-z0-9_.]*?Command)\b")
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string SourceRoot() =>
        Path.Combine(RepoRoot(), "apps", "slate-windows", "src", "SlateWindows");

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Cargo.toml")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}
