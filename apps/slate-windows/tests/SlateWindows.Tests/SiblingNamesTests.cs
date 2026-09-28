// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;
using System.Dynamic;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 3 (#1246, R-4: a duplicate sibling is told apart; the spec
/// review, round 21): <see cref="SiblingNames"/>, the one collision rule
/// every named items host uses — pure, then hosted on a list, a tree and a
/// closed combo, as UIA reads the containers.
/// </summary>
public sealed class SiblingNamesTests
{
    [Fact]
    public void DistinctNamesReadBare() =>
        Assert.Equal(
            ["alpha", "beta"],
            SiblingNames.Compose(["alpha", "beta"], ["A", "B"], "item"));

    /// <summary>Namesakes take their distinguisher, and namesakes that share
    /// one — ignoring case, as speech does (one file in two tabs) — both
    /// take their ordinal; a namesake with no distinguisher keeps its name
    /// once the others read apart from it, and takes its ordinal while one
    /// still reads like it.</summary>
    [Fact]
    public void NamesakesReadTheirDistinguisherElseTheirOrdinal()
    {
        Assert.Equal(
            ["note.md, A", "note.md, B, tab 2", "Note.MD, B, tab 3", "other.md", "note.md"],
            SiblingNames.Compose(
                ["note.md", "note.md", "Note.MD", "other.md", "note.md"],
                ["A", "B", "B", "C", null],
                "tab"));
        Assert.Equal(
            ["note.md, tab 1", "note.md, tab 2"],
            SiblingNames.Compose(["note.md", "note.md"], [null, null], "tab"));
    }

    /// <summary>Uniqueness holds AFTER the suffixes (the spec review,
    /// rounds 21 and 22): a natural name that reads like a suffixed one sends
    /// the whole colliding group to its ordinal form. "note.md (2)", a copy's
    /// name in another tool's shape, is not this rule's shape and meets no
    /// one: it reads as itself.</summary>
    [Fact]
    public void ASuffixThatMeetsANaturalNameFallsBackToTheOrdinal() =>
        Assert.Equal(
            ["note.md, A, item 1", "note.md, A, item 2", "note.md, B", "note.md (2)"],
            SiblingNames.Compose(
                ["note.md", "note.md, A", "note.md", "note.md (2)"],
                ["A", null, "B", null],
                "item"));

    /// <summary>Codex PR 3 round 4: the ordinal never takes back a
    /// distinguisher already given. Three tabs titled "note" — one of
    /// A/note.md, two of B/note.md — read the first by its path and the pair
    /// by path AND place, so the pair still say which file they hold.</summary>
    [Fact]
    public void TheOrdinalKeepsTheDistinguisherItFollows() =>
        Assert.Equal(
            ["note, A/note.md", "note, B/note.md, tab 2", "note, B/note.md, tab 3"],
            SiblingNames.Compose(
                ["note", "note", "note"],
                ["A/note.md", "B/note.md", "B/note.md"],
                "tab"));

    /// <summary>A blank name is never nothing, and a pathological natural
    /// name that equals an ordinal form still ends distinct.</summary>
    [Fact]
    public void BlankAndPathologicalNamesEndDistinct()
    {
        Assert.Equal(["Item 1", "Item 2", "x"], SiblingNames.Compose([" ", null, "x"], [], "item"));
        string[] names = SiblingNames.Compose(["a", "a", "a, item 1", "Item 1"], [], "item");
        Assert.Equal(names.Length, names.Distinct(SiblingNames.ReadAlike).Count());
    }

    /// <summary>Codex PR 3 round 5: a state is the rule's INPUT — the name
    /// the reader hears is what the collision checks run over, and nothing
    /// is added after them. A dirty "draft" beside a clean "draft, unsaved
    /// changes" (a note whose own title reads so) both read "draft, unsaved
    /// changes" when the state is appended after the rule; as an input, the
    /// pair add their paths (else their places), each still ending in its own
    /// state. A state tells no one apart by itself: two namesakes read their
    /// paths whatever their states.</summary>
    [Fact]
    public void AStateIsTheRulesInputSoItNeverMakesANameReadLikeAnother()
    {
        Assert.Equal(
            ["draft, A/draft.md, unsaved changes", "draft, unsaved changes, B/draft, unsaved changes.md"],
            SiblingNames.Compose(
                ["draft", "draft, unsaved changes"],
                ["A/draft.md", "B/draft, unsaved changes.md"],
                "tab",
                ["unsaved changes", string.Empty]));
        Assert.Equal(
            ["draft, tab 1, missing from disk", "draft, missing from disk, tab 2"],
            SiblingNames.Compose(["draft", "draft, missing from disk"], [], "tab", ["missing from disk", null]));
        Assert.Equal(
            ["note, A/note.md", "note, B/note.md, unsaved changes"],
            SiblingNames.Compose(["note", "note"], ["A/note.md", "B/note.md"], "tab", [null, "unsaved changes"]));
        Assert.Equal(
            ["draft, missing from disk, unsaved changes", "other"],
            SiblingNames.Compose(["draft", "other"], [], "tab", ["missing from disk, unsaved changes", " "]));
    }

    /// <summary>...and so is a host's prefix ("Recent search: "): every
    /// name reads it first, and namesakes are told apart after it.</summary>
    [Fact]
    public void APrefixIsTheRulesInputToo() =>
        Assert.Equal(
            ["Recent search: draft, search 1", "Recent search: DRAFT, search 2", "Recent search: Recent search: draft"],
            SiblingNames.Compose(["draft", "DRAFT", "Recent search: draft"], [], "search", prefix: "Recent search: "));

    /// <summary>Codex PR 3 round 5: ONE comparison reads names alike, and it
    /// is culture-independent. Under tr-TR the current culture's case folding
    /// pairs I with ı and İ with i, so it reported "FILE" and "file" as two
    /// names and left both bare; speech does not hear case. And the ordinal's
    /// noun is the shell's English word, never cased by the machine's locale
    /// ("İtem 1").</summary>
    [Fact]
    public void UnderATurkishCultureCaseVariantsStillReadAlike() => UnderCulture("tr-TR", () =>
    {
        // The premise: this culture's own ignore-case comparison tells them apart.
        Assert.False(string.Equals("FILE", "file", StringComparison.CurrentCultureIgnoreCase));
        Assert.Equal(
            ["FILE, A/FILE.md", "file, B/file.md"],
            SiblingNames.Compose(["FILE", "file"], ["A/FILE.md", "B/file.md"], "item"));
        Assert.Equal(["FILE, item 1", "file, item 2"], SiblingNames.Compose(["FILE", "file"], [], "item"));
        Assert.Equal(["Item 1", "x"], SiblingNames.Compose([null, "x"], [], "item"));
    });

    /// <summary>Why the one comparison is the INVARIANT culture's
    /// ignore-case one rather than the ordinal: speech carries neither case
    /// nor a string's encoding. "café" composed and decomposed, or a name
    /// with a zero-width character in it, read identically — the ordinal
    /// comparison would call each pair two names and leave both
    /// bare.</summary>
    [Fact]
    public void EncodingsOfOneNameReadAlike()
    {
        const string Composed = "café";
        const string Decomposed = "café";
        Assert.False(string.Equals(Composed, Decomposed, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(
            [$"{Composed}, A", $"{Decomposed}, B"],
            SiblingNames.Compose([Composed, Decomposed], ["A", "B"], "item"));
        Assert.Equal(["note, A", "no​te, B"], SiblingNames.Compose(["note", "no​te"], ["A", "B"], "item"));
    }

    /// <summary>Codex PR 3 round 6, owner decision OD-9 — speech identity: a
    /// name's <see cref="SpeechKey"/> is what a reader speaks of it, its
    /// letter and digit runs after NFKC and invariant case folding, so
    /// whitespace of any kind, punctuation, a ligature, a fullwidth digit, a
    /// soft hyphen and a zero-width character change nothing, and a letter
    /// run meeting a digit run is two words.</summary>
    [Theory]
    [InlineData("Open tasks", "Open tasks ", "trailing space")]
    [InlineData("Open tasks", " Open tasks", "leading space")]
    [InlineData("Open tasks", "Open  tasks", "repeated space")]
    [InlineData("Open tasks", "Open\ttasks", "tab")]
    [InlineData("Open tasks", "Open\u00A0tasks", "no-break space")]
    [InlineData("Open tasks", "Open\u2003tasks", "em space")]
    [InlineData("Open tasks", "Open tasks\u3000", "trailing ideographic space")]
    [InlineData("Open tasks", "Open tasks.", "full stop")]
    [InlineData("Open tasks", "\u201COpen tasks\u201D", "quotation marks")]
    [InlineData("Open tasks", "Open, tasks!", "comma and exclamation")]
    [InlineData("note a", "note-a", "hyphen")]
    [InlineData("note a", "note_a", "underscore")]
    [InlineData("note a", "(note) [a]", "brackets")]
    [InlineData("Note 1", "Note1", "a letter run meeting a digit run")]
    [InlineData("Note 1", "Note \u0661", "an Arabic-Indic digit")]
    [InlineData("file 2", "\uFB01le \uFF12", "a ligature and a fullwidth digit")]
    [InlineData("note", "no\u00ADte", "soft hyphen")]
    [InlineData("note", "no\u200Bte", "zero-width space")]
    [InlineData("caf\u00E9", "cafe\u0301", "composed and decomposed")]
    [InlineData("FILE", "file", "case")]
    public void NamesThatReadAlikeShareASpeechKey(string name, string twin, string shape)
    {
        Assert.True(
            SpeechKey.Of(name) == SpeechKey.Of(twin) && SiblingNames.ReadAlike.Equals(name, twin),
            $"{shape}: \"{SpeechKey.Of(name)}\" and \"{SpeechKey.Of(twin)}\"");
        Assert.Equal(SiblingNames.ReadAlike.GetHashCode(name), SiblingNames.ReadAlike.GetHashCode(twin));
    }

    /// <summary>...and names a reader speaks apart keep their keys apart: the
    /// key is coarse, never blind — a combining mark stays with its letter (a
    /// Devanagari vowel sign, a diaeresis). Two scripts whose letters look
    /// alike (Latin "A", Greek "\u0391") stay apart too: AR-34.</summary>
    [Theory]
    [InlineData("note", "notes")]
    [InlineData("Note 1", "Note 12")]
    [InlineData("v1.2", "v12")]
    [InlineData("draft", "draft 2")]
    [InlineData("na\u00EFve", "naive")]
    [InlineData("\u0915\u093F", "\u0915\u093E")]
    [InlineData("\u0391", "A")]
    public void NamesSpokenApartKeepTheirKeysApart(string name, string other) =>
        Assert.NotEqual(SpeechKey.Of(name), SpeechKey.Of(other));

    /// <summary>The rule over those shapes: two names that read alike take
    /// their places, and each keeps its own spelling — the emitted label is
    /// verbatim; only the comparison is by key.</summary>
    [Theory]
    [InlineData("Open tasks", "Open tasks ")]
    [InlineData("Open tasks", " Open  tasks")]
    [InlineData("Open tasks", "Open\ttasks")]
    [InlineData("Open tasks", "Open tasks\u3000")]
    [InlineData("Open tasks", "Open tasks.")]
    [InlineData("note a", "note-a")]
    [InlineData("Note 1", "Note1")]
    public void NamesThatReadAlikeTakeTheirPlacesVerbatim(string name, string twin) =>
        Assert.Equal(
            [$"{name}, view 1", $"{twin}, view 2"],
            SiblingNames.Compose([name, twin], [], "view"));

    /// <summary>A name that says nothing — only whitespace, control or format
    /// characters — is no name: it reads its ordinal.</summary>
    [Fact]
    public void ASilentNameIsNoName() =>
        Assert.Equal(
            ["View 1", "View 2", "View 3", "Archive"],
            SiblingNames.Compose(["\u200B", "\u3000", "\t\u00AD", "Archive"], [], "view"));

    /// <summary>
    /// The closing fact of codex PR 3 round 5: whatever the names,
    /// distinguishers, states and prefix — every pair of a pool of
    /// adversarial items (names that are another's name with a state, an
    /// ordinal, a distinguisher; case, encoding and zero-width variants;
    /// blanks), and seeded random sets of three to six, under tr-TR — the
    /// final pass leaves no two spoken names alike, and every name still
    /// starts with its host's prefix and ends with its own state.
    /// </summary>
    [Fact]
    public void TheFinalPassLeavesNoTwoNamesAlikeWhateverItsInputs() => UnderCulture("tr-TR", () =>
    {
        string[] names =
        [
            "draft", "DRAFT", "dra​ft", "draft, unsaved changes", "draft, missing from disk",
            "draft, missing from disk, unsaved changes", "draft, A", "draft, A, item 1", "draft, item 2",
            "draft, A, unsaved changes", "Item 1", "item 2", "Item 1, unsaved changes", string.Empty, " ",
            "FILE", "file", "café", "café", "Recent search: draft",
            // Codex PR 3 round 6: whitespace and punctuation a reader does
            // not speak.
            "draft ", " draft", "dra  ft", "draft.", "\u201Cdraft\u201D", "draft-a", "draft a", "draft1", "draft 1",
            "draft, unsaved changes.", "Item 1.", "Item1",
        ];
        string?[] distinguishers = [null, "A", "a", "B"];
        string?[] states = [null, "unsaved changes", "missing from disk", "missing from disk, unsaved changes"];
        (string Name, string? Distinguisher, string? State)[] pool =
        [
            .. from name in names
               from distinguisher in distinguishers
               from state in states
               select (name, distinguisher, state),
        ];
        int checkedSets = 0;
        void Check((string Name, string? Distinguisher, string? State)[] set, string? prefix)
        {
            string[] spoken = SiblingNames.Compose(
                [.. set.Select(item => (string?)item.Name)],
                [.. set.Select(item => item.Distinguisher)],
                "item",
                [.. set.Select(item => item.State)],
                prefix);
            string Inputs() => string.Join(" | ", set.Select(item => $"{item.Name}/{item.Distinguisher}/{item.State}"));
            if (spoken.Distinct(SiblingNames.ReadAlike).Count() != spoken.Length)
            {
                Assert.Fail($"[{Inputs()}] read alike: [{string.Join(" | ", spoken)}]");
            }
            for (int index = 0; index < set.Length; index++)
            {
                if (!spoken[index].StartsWith(prefix ?? string.Empty, StringComparison.Ordinal)
                    || (!string.IsNullOrWhiteSpace(set[index].State)
                        && !spoken[index].EndsWith($", {set[index].State}", StringComparison.Ordinal)))
                {
                    Assert.Fail($"[{Inputs()}] item {index + 1} reads \"{spoken[index]}\": its prefix or state is lost");
                }
            }
            checkedSets++;
        }

        for (int first = 0; first < pool.Length; first++)
        {
            for (int second = first; second < pool.Length; second++)
            {
                Check([pool[first], pool[second]], first % 2 == 0 ? null : "Recent search: ");
            }
        }
        var random = new Random(1246);
        for (int round = 0; round < 4000; round++)
        {
            Check(
                [.. Enumerable.Range(0, random.Next(3, 7)).Select(_ => pool[random.Next(pool.Length)])],
                random.Next(2) == 0 ? null : "Recent search: ");
        }
        Assert.True(checkedSets > 50_000, $"only {checkedSets} sets checked");
    });

    /// <summary>Runs <paramref name="body"/> with the current culture and UI
    /// culture <paramref name="name"/>, restoring both after.</summary>
    internal static void UnderCulture(string name, Action body)
    {
        CultureInfo culture = CultureInfo.CurrentCulture;
        CultureInfo uiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
        try
        {
            body();
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
        }
    }

    /// <summary>Hosted on a list: UIA reads the rule's names — two equal
    /// strings are two rows — and follows an add, a removal and a rename.</summary>
    [Fact]
    public void AListReadsItsItemsApartAndFollowsTheirChanges() => RunSta(() =>
    {
        var items = new ObservableCollection<object>
        {
            Row("note.md", "A"),
            Row("note.md", "B"),
            Row("other.md", "C"),
        };
        var host = new ListBox { ItemContainerStyle = SiblingNames.ContainerStyle(typeof(ListBoxItem)) };
        SiblingNames.SetNamePath(host, "Name");
        SiblingNames.SetDistinguisherPath(host, "Path");
        host.ItemsSource = items;
        Hosted(host, () =>
        {
            Assert.Equal(["note.md, A", "note.md, B", "other.md"], Names(host));

            items.RemoveAt(1);
            Assert.Equal(["note.md", "other.md"], Names(host));

            ((IDictionary<string, object?>)items[1])["Name"] = "note.md";
            Assert.Equal(["note.md, A", "note.md, C"], Names(host));

            items.Add(Row("fresh.md", "D"));
            Assert.Equal(["note.md, A", "note.md, C", "fresh.md"], Names(host));
        });

        // Two rows with no distinguisher read their ordinals, in the host's
        // own noun.
        var warnings = new ListBox { ItemContainerStyle = SiblingNames.ContainerStyle(typeof(ListBoxItem)) };
        SiblingNames.SetNamePath(warnings, "Name");
        SiblingNames.SetNoun(warnings, "warning");
        warnings.ItemsSource = new[] { Row("Missing file", null), Row("Missing file", null) };
        Hosted(warnings, () => Assert.Equal(["Missing file, warning 1", "Missing file, warning 2"], Names(warnings)));
    });

    /// <summary>A RECYCLING list hands a container that scrolled away to
    /// another item without re-applying its style, and the name binding's
    /// source — the container itself — never changes: every reused
    /// container must still read its new item's name. (The Files tree, the
    /// filter results and the dual pane recycle.)</summary>
    [Fact]
    public void ARecyclingListNamesEachReusedContainerForItsNewItem() => RunSta(() =>
    {
        var items = new ObservableCollection<object>();
        for (int index = 0; index < 300; index++)
        {
            items.Add(Row(index % 3 == 0 ? "shared.md" : $"note {index}.md", $"folder {index}"));
        }
        string[] expected = SiblingNames.Compose(
            [.. items.Select(item => (string?)((IDictionary<string, object?>)item)["Name"])],
            [.. items.Select(item => (string?)((IDictionary<string, object?>)item)["Path"])],
            "item");
        var host = new ListBox
        {
            ItemContainerStyle = SiblingNames.ContainerStyle(typeof(ListBoxItem)),
            Height = 150,
        };
        VirtualizingPanel.SetIsVirtualizing(host, true);
        VirtualizingPanel.SetVirtualizationMode(host, VirtualizationMode.Recycling);
        SiblingNames.SetNamePath(host, "Name");
        SiblingNames.SetDistinguisherPath(host, "Path");
        host.ItemsSource = items;
        Hosted(host, () =>
        {
            void Expect(string where)
            {
                host.UpdateLayout();
                PumpedDispatcher.Drain();
                int realized = 0;
                for (int index = 0; index < items.Count; index++)
                {
                    if (host.ItemContainerGenerator.ContainerFromIndex(index) is ListBoxItem container)
                    {
                        realized++;
                        string read = AutomationProperties.GetName(container);
                        Assert.True(
                            read == expected[index],
                            $"{where}: row {index} reads \"{read}\", not \"{expected[index]}\"");
                    }
                }
                Assert.True(realized is > 0 and < 100, $"{where}: {realized} rows realized — the list does not virtualize");
            }

            Expect("at the top");
            host.ScrollIntoView(items[150]);
            Expect("mid-way");
            host.ScrollIntoView(items[^1]);
            Expect("at the end");
            host.ScrollIntoView(items[0]);
            Expect("back at the top");
        });
    });

    /// <summary>A batch of changes — a folder resorted row by row, five
    /// hundred filter results added one at a time — is ONE refresh of the
    /// names, once the batch is done: each refresh reads every item, so one
    /// per change would be quadratic.</summary>
    [Fact]
    public void ABatchOfChangesIsOneRefresh() => RunSta(() =>
    {
        var items = new ObservableCollection<object>();
        var host = new ListBox
        {
            ItemContainerStyle = SiblingNames.ContainerStyle(typeof(ListBoxItem)),
            Height = 150,
        };
        SiblingNames.SetNamePath(host, "Name");
        host.ItemsSource = items;
        Hosted(host, () =>
        {
            host.UpdateLayout();
            PumpedDispatcher.Drain();
            int before = SiblingNames.RefreshesForTests(host);
            Assert.True(before >= 0, "the host declares no scope");
            for (int index = 0; index < 500; index++)
            {
                items.Add(Row($"note {index % 250}.md", null));
            }
            Assert.Equal(before, SiblingNames.RefreshesForTests(host));
            host.UpdateLayout();
            PumpedDispatcher.Drain();
            Assert.Equal(before + 1, SiblingNames.RefreshesForTests(host));
            // ...and that one refresh named them: each name twice, told apart
            // by place.
            var first = (ListBoxItem)host.ItemContainerGenerator.ContainerFromIndex(0);
            Assert.Equal("note 0.md, item 1", AutomationProperties.GetName(first));
        });
    });

    /// <summary>UIA names an item no container holds — a closed combo's
    /// selection, a row a virtualized list has not realized — through a
    /// throwaway wrapper container that sits in no panel, and it reuses that
    /// wrapper for the next such item. The wrapper must read its item's name
    /// under the rule (never the item's ToString, which the Bases and Graph
    /// inspector journeys heard as a record dump), and read again for every
    /// item it is handed.</summary>
    [Fact]
    public void AnUnrealizedItemIsNamedThroughTheRuleToo() => RunSta(() =>
    {
        var host = new ComboBox { ItemContainerStyle = SiblingNames.ContainerStyle(typeof(ComboBoxItem)) };
        SiblingNames.SetNamePath(host, "Name");
        SiblingNames.SetNoun(host, "view");
        host.ItemsSource = new[] { Row("Open tasks", null), Row("Open tasks", null), Row("Archive", null) };
        host.SelectedIndex = 1;
        Hosted(host, () =>
        {
            host.UpdateLayout();
            PumpedDispatcher.Drain();
            // Never opened: no item has a container of its own.
            Assert.Null(host.ItemContainerGenerator.ContainerFromIndex(1));
            AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(host);
            // The peer UIA hands out for the selection (SelectorAutomationPeer
            // makes it the same way): an item peer with no container behind it.
            System.Reflection.MethodInfo create = typeof(ItemsControlAutomationPeer).GetMethod(
                "CreateItemAutomationPeer",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            string SelectedName() =>
                ((ItemAutomationPeer)create.Invoke(peer, [host.SelectedItem])!).GetName();

            Assert.Equal("Open tasks, view 2", SelectedName());
            host.SelectedIndex = 2;
            PumpedDispatcher.Drain();
            Assert.Equal("Archive", SelectedName());
            host.SelectedIndex = 0;
            PumpedDispatcher.Drain();
            Assert.Equal("Open tasks, view 1", SelectedName());
        });
    });

    /// <summary>A tree scopes per level: each tree item names its own
    /// children among themselves.</summary>
    [Fact]
    public void ATreeNamesEachLevelAmongItself() => RunSta(() =>
    {
        var tree = new TreeView
        {
            ItemContainerStyle = SiblingNames.ContainerStyle(typeof(TreeViewItem)),
            ItemTemplate = new HierarchicalDataTemplate { ItemsSource = new System.Windows.Data.Binding("Children") },
        };
        SiblingNames.SetNamePath(tree, "Name");
        SiblingNames.SetDistinguisherPath(tree, "Path");
        dynamic folder = Row("Notes", "Notes");
        folder.Children = new ObservableCollection<object> { Row("Untitled", "Notes/a.md"), Row("Untitled", "Notes/b.md") };
        tree.ItemsSource = new ObservableCollection<object> { folder, Row("Untitled", "c.md") };
        Hosted(tree, () =>
        {
            Assert.Equal(["Notes", "Untitled"], Names(tree));
            var first = (TreeViewItem)tree.ItemContainerGenerator.ContainerFromIndex(0);
            first.IsExpanded = true;
            first.UpdateLayout();
            PumpedDispatcher.Drain();
            Assert.Equal(["Untitled, Notes/a.md", "Untitled, Notes/b.md"], Names(first));
        });
    });

    /// <summary>A combo's items — their containers live in its drop-down —
    /// read the rule's names, the selected one included (the value NVDA
    /// speaks for the combo; the closed combo's is pinned by the FlaUI
    /// journeys).</summary>
    [Fact]
    public void AComboReadsItsItemsApart() => RunSta(() =>
    {
        var combo = new ComboBox { ItemContainerStyle = SiblingNames.ContainerStyle(typeof(ComboBoxItem)) };
        SiblingNames.SetNamePath(combo, "Name");
        SiblingNames.SetNoun(combo, "view");
        combo.ItemsSource = new[] { Row("Main", null), Row("Main", null) };
        combo.SelectedIndex = 1;
        Hosted(combo, () =>
        {
            combo.IsDropDownOpen = true;
            Assert.Equal(["Main, view 1", "Main, view 2"], Names(combo));
            AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(combo);
            ItemAutomationPeer selected = Assert.Single(
                peer.GetChildren().OfType<ItemAutomationPeer>(),
                item => ReferenceEquals(item.Item, combo.SelectedItem));
            Assert.Equal("Main, view 2", selected.GetName());
        });
    });

    private static ExpandoObject Row(string name, string? path)
    {
        var row = new ExpandoObject();
        var fields = (IDictionary<string, object?>)row;
        fields["Name"] = name;
        fields["Path"] = path;
        return row;
    }

    private static string[] Names(ItemsControl host)
    {
        host.UpdateLayout();
        PumpedDispatcher.Drain();
        host.UpdateLayout();
        AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(host);
        peer.ResetChildrenCache();
        return [.. (peer.GetChildren() ?? []).OfType<ItemAutomationPeer>().Select(item => item.GetName())];
    }

    private static void Hosted(FrameworkElement content, Action body)
    {
        var window = new Window
        {
            Content = content,
            Width = 480,
            Height = 360,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            body();
        }
        finally
        {
            window.Close();
        }
    }

    private static void RunSta(Action body) =>
        StaThread.Run(body, TimeSpan.FromSeconds(60), "STA test body timed out.");
}
