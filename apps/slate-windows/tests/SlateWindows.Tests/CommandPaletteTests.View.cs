// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// R-11 (#1254): the results list as the shell ships it. The list is
/// <c>MainWindow.xaml</c>'s own <c>CommandPaletteResultsList</c> — its
/// container style, templates, grouping, selection and virtualization
/// settings — driven by the production
/// <see cref="CommandPaletteResultsPresenter"/> over the fake command
/// source, so every announcement is countable. The view-model facts above
/// never render; the stale "Selected: New Canvas" of the NVDA pass lived
/// entirely in the view.
/// </summary>
public sealed partial class CommandPaletteTests
{
    // --- R-11 / P7 / P10: the view never selects on the user's behalf ------

    /// <summary>
    /// At most one <c>PaletteCommandSelected</c> per query change, and none
    /// when the selected id survives it — with the list showing, and
    /// scrolled to, exactly the row the view model chose.
    /// </summary>
    /// <remarks>
    /// The discriminating step is the survivor that is NOT the new first
    /// row. Swapping a grouped view into a list synchronized with its
    /// current item selects the view's first row; the pointer route handed
    /// that to the view model, which announced it, and the view model then
    /// restored its survivor and announced that too — two sentences for a
    /// change that owes none, the first naming a row the user never chose.
    /// </remarks>
    [Fact]
    public void TheShippedListAnnouncesOnlyTheViewModelsSelection() => RunSta(() =>
    {
        using var host = new ShippedResultsList(StandardCommands());
        CommandPaletteViewModel palette = host.Palette;
        palette.Open();
        host.Settle();
        host.AssertShowsTheViewModelsSelection();
        Assert.Empty(host.SelectionAnnouncements);

        palette.Select(palette.Rows[3]);
        host.Settle();
        Assert.Equal("slate.editor.bold", palette.SelectedId);
        Assert.Equal(["Toggle Bold"], host.SelectionAnnouncements);

        // Survives, and is the LAST of three rows: nothing to say (P7).
        host.ChangeQuery("o");
        Assert.Equal(
            ["slate.file.newNote", "slate.nav.quickOpen", "slate.editor.bold"],
            host.Harness.RowIds);
        Assert.Equal("slate.editor.bold", palette.SelectedId);
        Assert.Empty(host.SelectionAnnouncements);
        host.AssertShowsTheViewModelsSelection();

        // Vanishes: the snap to the first row is one sentence, not two.
        host.ChangeQuery("q");
        Assert.Equal(["Quick Open"], host.SelectionAnnouncements);
        host.AssertShowsTheViewModelsSelection();

        // Zero matches: no selection, nothing said.
        host.ChangeQuery("zzzz");
        Assert.Null(palette.SelectedRow);
        Assert.Empty(host.SelectionAnnouncements);
        host.AssertShowsTheViewModelsSelection();

        // Recovering from none selects, and says so, once.
        host.ChangeQuery("sav");
        Assert.Equal(["Save"], host.SelectionAnnouncements);
        host.AssertShowsTheViewModelsSelection();
    });

    /// <summary>
    /// A query that removes the selected id moves the selection to its
    /// replacement, and the reader hears exactly that: one
    /// <c>PaletteCommandSelected</c> naming the replacement, then the
    /// query's count — and a query the selection survives says the count
    /// alone (contract 28 P7, P10), through the shipped list.
    /// </summary>
    /// <remarks>
    /// The other R-11 facts forbid a selection the user never made; this
    /// one forbids the opposite repair — silencing selection announcements
    /// altogether — which would pass every "none on a survivor" and "never
    /// the wrong row" assertion while the reader lost the row they are on.
    /// </remarks>
    [Fact]
    public void AQueryThatRemovesTheSelectionAnnouncesItsReplacementThenTheCount() => RunSta(() =>
    {
        using var host = new ShippedResultsList(StandardCommands());
        CommandPaletteViewModel palette = host.Palette;
        palette.Open();
        host.Settle();
        palette.SelectLast();
        host.Settle();
        Assert.Equal("slate.tasks.review", palette.SelectedId);

        // "Tasks Review" has no 'o': the selection moves to the first of
        // the three rows left, and that move is the one thing to say
        // before the count.
        host.ChangeQuery("o");
        Assert.Equal("slate.file.newNote", palette.SelectedId);
        Assert.Collection(
            host.Harness.Announcements,
            announced => Assert.Equal(
                "New Note",
                Assert.IsType<A11yEvent.PaletteCommandSelected>(announced).Label),
            announced => Assert.Equal(
                (3u, "o"),
                (Assert.IsType<A11yEvent.PaletteFilterCount>(announced).Count,
                    ((A11yEvent.PaletteFilterCount)announced).Query)));
        host.AssertShowsTheViewModelsSelection();

        // "New Note" survives "no": the count, and nothing else.
        host.ChangeQuery("no");
        Assert.Equal("slate.file.newNote", palette.SelectedId);
        A11yEvent.PaletteFilterCount count = Assert.IsType<A11yEvent.PaletteFilterCount>(
            Assert.Single(host.Harness.Announcements));
        Assert.Equal("no", count.Query);
        host.AssertShowsTheViewModelsSelection();
    });

    /// <summary>
    /// The selection-sync guard alone keeps the swap silent (R-11). The
    /// shipped list is switched back to synchronizing its current item — as
    /// a later style, template or control could — so the swap really does
    /// select the new view's first row, and nothing may reach the view
    /// model as a choice the user made.
    /// </summary>
    /// <remarks>
    /// The XAML half hides the guard from every other runtime fact: an
    /// unsynchronized swap only deselects. With that half turned off here,
    /// the guard is the only lock left, so this fails when the swap leaves
    /// the guard and when the pointer route stops honouring it. The first
    /// assertion after the query change proves the list did select the
    /// first row; without it this fact could pass on a list that never
    /// synchronized at all.
    /// </remarks>
    [Fact]
    public void TheGuardAloneKeepsASynchronizedSwapSilent() => RunSta(() =>
    {
        using var host = new ShippedResultsList(StandardCommands());
        host.List.IsSynchronizedWithCurrentItem = true;
        CommandPaletteViewModel palette = host.Palette;
        palette.Open();
        host.Settle();
        palette.Select(palette.Rows[3]);
        host.Settle();
        Assert.Equal("slate.editor.bold", palette.SelectedId);
        Assert.Equal(["Toggle Bold"], host.SelectionAnnouncements);

        // The survivor is the last of three rows; the synchronized swap
        // selects the first, and nobody may hear about it (P7).
        host.ChangeQuery("o");
        Assert.Contains(
            host.ListSelections,
            added => added is CommandPaletteRowViewModel { Id: "slate.file.newNote" });
        Assert.Empty(host.SelectionAnnouncements);
        Assert.Equal("slate.editor.bold", palette.SelectedId);
        Assert.Same(palette.SelectedRow, host.List.SelectedItem);

        // The survivor vanishes: the view model's one snap, and no other.
        host.ChangeQuery("q");
        Assert.Equal(["Quick Open"], host.SelectionAnnouncements);
        Assert.Same(palette.SelectedRow, host.List.SelectedItem);
    });

    /// <summary>
    /// With the list no longer synchronized to its current item, every
    /// keyboard move still reaches it: Up, Down, Home and End (PD-1) each
    /// highlight the view model's row and scroll it into view, each says
    /// one sentence, and Enter runs the row the list shows.
    /// </summary>
    /// <remarks>
    /// Enough rows that End and the wrap from the top land far outside the
    /// first page, so the scroll is real work for a virtualized list.
    /// </remarks>
    [Fact]
    public void KeyboardMovesAndEnterStillDriveTheShippedList() => RunSta(() =>
    {
        using var host = new ShippedResultsList(SyntheticCommands(400));
        CommandPaletteViewModel palette = host.Palette;
        palette.Open();
        host.Settle();
        host.AssertShowsTheViewModelsSelection();

        void Move(Action move, int expectedIndex)
        {
            host.ClearObservations();
            move();
            host.Settle();
            CommandPaletteRowViewModel expected = palette.Rows[expectedIndex];
            Assert.Same(expected, palette.SelectedRow);
            Assert.Equal([expected.Label], host.SelectionAnnouncements);
            host.AssertShowsTheViewModelsSelection();
        }

        int last = palette.Rows.Count - 1;
        Move(palette.SelectLast, last);
        Move(palette.SelectFirst, 0);
        Move(() => palette.MoveSelection(1), 1);
        Move(() => palette.MoveSelection(1), 2);
        Move(() => palette.MoveSelection(-1), 1);
        Move(() => palette.MoveSelection(-1), 0);
        // Up from the first row wraps to the last (contract P7).
        Move(() => palette.MoveSelection(-1), last);

        // Enter runs the row the list highlights — the view model's.
        CommandPaletteRowViewModel highlighted =
            Assert.IsType<CommandPaletteRowViewModel>(host.List.SelectedItem);
        palette.InvokeSelected();
        Assert.Equal([SyntheticId(last)], host.Harness.Source.Invoked);
        Assert.Equal(SyntheticId(last), highlighted.Id);
    });

    // --- R-11: grouped rows virtualize --------------------------------------

    /// <summary>
    /// Only the rows near the viewport are realized, grouped as they are,
    /// and a row two thousand down is realized — and its section heading
    /// rendered — when the selection travels there.
    /// </summary>
    /// <remarks>
    /// A grouped <c>ListBox</c> stops virtualizing unless
    /// <c>VirtualizingPanel.IsVirtualizingWhenGrouping</c> says otherwise,
    /// and then every keystroke re-templated every matching row: the NVDA
    /// pass's first keystroke was 174 of them, with each row's segment
    /// template, before a single one could be read.
    /// </remarks>
    [Fact]
    public void GroupedRowsVirtualize() => RunSta(() =>
    {
        using var host = new ShippedResultsList(SyntheticCommands(2_000));
        CommandPaletteViewModel palette = host.Palette;
        palette.Open();
        host.Settle();
        Assert.Equal(2_000, palette.Rows.Count);

        int realizedAtTheTop = host.RealizedContainers().Length;
        Assert.InRange(realizedAtTheTop, 1, 150);
        Assert.Contains(SyntheticSections[0].Title, host.RenderedHeadings());

        host.ClearObservations();
        palette.SelectLast();
        host.Settle();
        host.AssertShowsTheViewModelsSelection();
        Assert.InRange(host.RealizedContainers().Length, 1, 150);
        Assert.Contains(SyntheticSections[^1].Title, host.RenderedHeadings());
    });

    // --- helpers -------------------------------------------------------------

    private static readonly (CommandSection Section, string Title)[] SyntheticSections =
    [
        (CommandSection.File, "File"),
        (CommandSection.Navigation, "Navigation"),
        (CommandSection.Editor, "Editor"),
        (CommandSection.Tasks, "Tasks"),
    ];

    private static string SyntheticId(int index) =>
        $"slate.synthetic.row{index:D5}";

    /// <summary>Commands spread evenly over four sections, in section order
    /// so the empty query renders them as listed.</summary>
    private static Command[] SyntheticCommands(int count)
    {
        var commands = new Command[count];
        for (int index = 0; index < count; index++)
        {
            CommandSection section = SyntheticSections[index * SyntheticSections.Length / count].Section;
            commands[index] = Cmd(SyntheticId(index), $"Synthetic Command {index:D5}", section);
        }

        return commands;
    }

    /// <summary>
    /// The shipped results list, lifted out of a real (never shown)
    /// <see cref="MainWindow"/> — the Move-To focus fixture's technique —
    /// and re-hosted off-screen over a palette whose command source is the
    /// fake. The shell's own presenter lets go first, so the list answers
    /// one view model.
    /// </summary>
    private sealed class ShippedResultsList : IDisposable
    {
        private readonly MainWindow _shell;
        private readonly Window _window;
        private readonly CommandPaletteResultsPresenter _presenter;

        public ShippedResultsList(Command[] commands)
        {
            Harness = new PaletteHarness(commands);
            _shell = new MainWindow();
            Assert.IsType<CommandPaletteResultsPresenter>(_shell.PaletteResults).Dispose();
            List = _shell.CommandPaletteResultsList;
            Assert.IsAssignableFrom<Panel>(List.Parent).Children.Remove(List);
            List.SelectionChanged += List_SelectionChanged;

            // The overlay's list row, near enough: 470 high less the search
            // box, the footer and the padding. Off-screen and never
            // activated, so a journey running on this desktop keeps the
            // foreground.
            _window = new Window
            {
                Content = List,
                DataContext = Harness.Palette,
                Width = 640,
                Height = 360,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -20_000,
                Top = -20_000,
                ShowInTaskbar = false,
                ShowActivated = false,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
            };
            _window.Show();
            _presenter = new CommandPaletteResultsPresenter(List, Harness.Palette);
        }

        public PaletteHarness Harness { get; }

        public CommandPaletteViewModel Palette => Harness.Palette;

        public ListBox List { get; }

        /// <summary>Every row the list added to its OWN selection since the
        /// last clear — each one a UIA ElementSelected an AT can hear.</summary>
        public List<object> ListSelections { get; } = [];

        public string[] SelectionAnnouncements =>
        [
            .. Harness.Announcements
                .OfType<A11yEvent.PaletteCommandSelected>()
                .Select(selected => selected.Label),
        ];

        public void ClearObservations()
        {
            Harness.Announcements.Clear();
            ListSelections.Clear();
        }

        /// <summary>One query change, observed from a clean slate.</summary>
        public void ChangeQuery(string query)
        {
            ClearObservations();
            Palette.Query = query;
            Settle();
        }

        /// <summary>Layout, the queued <c>ScrollIntoView</c>, and the layout
        /// that one causes.</summary>
        public void Settle()
        {
            List.UpdateLayout();
            PumpedDispatcher.Drain();
            List.UpdateLayout();
            PumpedDispatcher.Drain();
        }

        /// <summary>
        /// The list highlights the view model's row, never selected
        /// anything else on the way there, and — when there is a row —
        /// has it realized and inside the viewport.
        /// </summary>
        public void AssertShowsTheViewModelsSelection()
        {
            CommandPaletteRowViewModel? selected = Palette.SelectedRow;
            Assert.Same(selected, List.SelectedItem);
            Assert.All(ListSelections, added => Assert.Same(selected, added));
            if (selected is null)
            {
                return;
            }

            ListBoxItem? container = RealizedContainers()
                .SingleOrDefault(item => ReferenceEquals(item.DataContext, selected));
            Assert.True(
                container is not null,
                $"'{selected.Label}' is selected but its row was never realized — "
                + "ScrollIntoView did not bring it into the list.");
            Assert.True(container!.IsSelected, $"'{selected.Label}' is not highlighted.");
            ScrollContentPresenter viewport = Descendants<ScrollContentPresenter>(List).Single();
            Rect bounds = container
                .TransformToAncestor(viewport)
                .TransformBounds(new Rect(container.RenderSize));
            Assert.True(
                bounds.Top >= -0.5 && bounds.Bottom <= viewport.ActualHeight + 0.5,
                $"'{selected.Label}' is realized at {bounds} but the viewport is "
                + $"0..{viewport.ActualHeight}: selected off-screen.");
        }

        public ListBoxItem[] RealizedContainers() => [.. Descendants<ListBoxItem>(List)];

        /// <summary>The section headings the grouped list has rendered.</summary>
        public string[] RenderedHeadings() =>
        [
            .. Descendants<GroupItem>(List)
                .SelectMany(Descendants<TextBlock>)
                .Where(text => AutomationPropertiesHeading(text))
                .Select(text => text.Text),
        ];

        public void Dispose()
        {
            List.SelectionChanged -= List_SelectionChanged;
            _presenter.Dispose();
            _window.Close();
            _shell.Close();
        }

        private static bool AutomationPropertiesHeading(TextBlock text) =>
            System.Windows.Automation.AutomationProperties.GetHeadingLevel(text)
                != System.Windows.Automation.AutomationHeadingLevel.None;

        private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            foreach (object added in e.AddedItems)
            {
                ListSelections.Add(added);
            }
        }

        private static IEnumerable<T> Descendants<T>(DependencyObject root)
            where T : DependencyObject
        {
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, index);
                if (child is T match)
                {
                    yield return match;
                }

                foreach (T nested in Descendants<T>(child))
                {
                    yield return nested;
                }
            }
        }
    }

    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                PumpedDispatcher.Run(body);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(120)), "the hosted palette fact timed out.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
