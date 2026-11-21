// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

// Plan 47 CB-17a (#1866; CD11 as the owner answered it on 2026-11-16: every
// self-disabling button moves the keys first, whatever the NVDA session hears).
// A control turned off while it holds the keys says "unavailable" in NVDA, and
// the keys fall to whatever is behind it. This part of the census:
//
//   - reads every Command= on an element that can take the keys itself
//     (xaml-command), and every C# write of a button's or a hyperlink's command
//     (command), an initializer's and a constructor's included, since the
//     command lives on with the control. A menu row is not one: WPF closes its
//     menu and gives the keys back before the row's command runs
//     (MenuRowCommandTests), unless it stays open on click;
//   - keys each IsEnabled trigger on its own, by its condition, and a style's
//     own IsEnabled setter that sets False or binds;
//   - holds every site that can turn a control off (an IsEnabled trigger,
//     binding or write, and every command site) to one of six witnesses:
//       keys-first: Class.Method   a fact whose code names the site (and, for
//                                  a trigger, its condition's token; for an
//                                  unnamed button, its command) and whose own
//                                  code calls KeysFirstRecorder.AssertTheKeysLeaveFirst
//       exempt: not focusable      checked against the XAML
//       exempt: always on: T.P     checked by reading every construction of
//                                  every property of that name in the shell
//       exempt: its own press never turns it off: <why>
//       exempt: never fires: <why> (Class.Method)
//       dated: <DatedOn>; CB-17b (CD11): <mechanism>   only for a key in
//                                  DatedBaseline, and only fewer each time
//     guard: and a fact that only names the site no longer witness such a site.
//
// The press class's limit: another driver can still turn such a control off
// under the keys (a refresh's or a rescan's deselect, an in-place change of
// item, a find bar's mode switch). This census does not judge other drivers
// (#1955).

using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

public sealed partial class FocusIntegrityManifestCensus
{
    /// <summary>The one day the dated rows were written.</summary>
    internal const string DatedOn = "2026-11-21";

    /// <summary>The chunk that ends every dated row, then deletes this kind.</summary>
    internal const string DatedOwner = "CB-17b (CD11)";

    /// <summary>The dated rows the manifest may hold: a PR that ends rows lowers it, and
    /// nothing raises it. CB-17b lowers it to zero and deletes the dated kind.</summary>
    internal const int DatedCeiling = 46;

    /// <summary>Every key dated on <see cref="DatedOn"/>. Nothing is ever added.</summary>
    internal static readonly string[] DatedBaseline =
    [
        "ui-state SlateWindows.Bases.BaseSurfaceView.RenderChrome _quickFilter IsEnabled",
        "ui-state SlateWindows.Bases.BaseSurfaceView.RenderChrome _viewPicker IsEnabled",
        "ui-state SlateWindows.Graph.ConnectionsLeafView.RenderCore _depth IsEnabled",
        "ui-state SlateWindows.Graph.GraphInspectorView.RenderGate section IsEnabled",
        "xaml-binding MainWindow.xaml BulkRenameApplyButton IsEnabled",
        "xaml-binding MainWindow.xaml BulkRenamePreviewButton IsEnabled",
        "xaml-binding MainWindow.xaml CitationSummaryWalkButton IsEnabled",
        "xaml-command MainWindow.xaml CancelTrash CancelTrashCommand",
        "xaml-command MainWindow.xaml CanvasInspectionClose CloseCanvasInspectionCommand",
        "xaml-command MainWindow.xaml MoveToCancelButton CancelCommand",
        "xaml-command MainWindow.xaml MoveToRetry RetryCommand",
        "xaml-command MainWindow.xaml QuickSwitcherClose DismissCommand",
        "xaml-command MainWindow.xaml SidebarBatchActions BatchTrashCommand",
        "xaml-command MainWindow.xaml SidebarBatchActions MoveToCommand",
        "xaml-command MainWindow.xaml SidebarExpandLoaded ExpandLoadedCommand",
        "xaml-command MainWindow.xaml SidebarFileActionsExpander CancelImportCommand",
        "xaml-command MainWindow.xaml SidebarFileActionsExpander CreateFolderNoteCommand",
        "xaml-command MainWindow.xaml SidebarFileActionsExpander DeleteCommand",
        "xaml-command MainWindow.xaml SidebarFileActionsExpander DeleteFolderNoteCommand",
        "xaml-command MainWindow.xaml SidebarFileActionsExpander ImportCommand",
        "xaml-command MainWindow.xaml SidebarFileActionsExpander MoveToCommand",
        "xaml-command MainWindow.xaml SidebarFileActionsExpander RenameCommand",
        "xaml-command MainWindow.xaml SidebarSettingsRetry RetrySettingsCommand",
        "xaml-command MainWindow.xaml SidebarShortcutsActions RemoveShortcutCommand",
        "xaml-command WorkspaceTemplates.xaml EditorPopoverClose ClosePopoverCommand",
        "xaml-command WorkspaceTemplates.xaml EditorPopoverOpenSource OpenPopoverSourceCommand",
        "xaml-trigger MainWindow.xaml MainMenu IsEnabled when FileSidebar.MoveToSheet=True",
        "xaml-trigger MainWindow.xaml MainMenu IsEnabled when SettingsSheet=True",
        "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.AddPropertySheet=True",
        "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.BaseQueryBuilderSheet=True",
        "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.BulkRenameSheet=True",
        "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.CanvasCardEditorSheet=True",
        "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.CanvasCardPickerSheet=True",
        "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.CanvasPromptSheet=True",
        "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.CitationDetails=True",
        "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.CitationSummary=True",
        "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.DashboardEditorSheet=True",
        "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.FilesCiting=True",
        "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.TemplateFlowSheet=True",
        "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.TemplatePickerSheet=True",
        "xaml-trigger MainWindow.xaml WrapPanel(QueriesOpenBaseFile) IsEnabled when QueriesBaseFilesList.SelectedItem={x:Null}",
        "xaml-trigger MainWindow.xaml WrapPanel(QueriesOpenDashboard) IsEnabled when QueriesDashboardsList.SelectedItem={x:Null}",
        "xaml-trigger MainWindow.xaml WrapPanel(QueriesRun) IsEnabled when QueriesSavedList.SelectedItem={x:Null}",
        "xaml-trigger WorkspaceTemplates.xaml Button in PropertyRowTemplate#0 IsEnabled when (style default)",
        "xaml-trigger WorkspaceTemplates.xaml Button in PropertyRowTemplate#0 IsEnabled when WriteInFlight=True",
        "xaml-trigger WorkspaceTemplates.xaml Style:Slate.LauncherRowStyle IsEnabled when IsModalSheetOpen=True",
    ];

    private static readonly XNamespace PresentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly string[] Untokened = ["IsOpen", "IsKeyboardFocusWithin", "SelectedItem"];

    /// <summary>A site that can turn a control off: an IsEnabled trigger,
    /// binding or write, and every command site.</summary>
    internal static bool IsDisableSite(string key) =>
        key.StartsWith("xaml-command ", StringComparison.Ordinal)
        || key.StartsWith("command ", StringComparison.Ordinal)
        || ((key.StartsWith("xaml-trigger ", StringComparison.Ordinal)
                || key.StartsWith("xaml-binding ", StringComparison.Ordinal)
                || key.StartsWith("ui-state ", StringComparison.Ordinal))
            && (key.EndsWith(" IsEnabled", StringComparison.Ordinal) || key.Contains(" IsEnabled when ", StringComparison.Ordinal)));

    /// <summary>The dated rows against their ceiling: more is a new dated row, fewer is a
    /// ceiling to lower.</summary>
    internal static string[] DatedProblems(IReadOnlyDictionary<string, string> manifest, int ceiling)
    {
        int rows = manifest.Values.Count(witness => witness.StartsWith("dated: ", StringComparison.Ordinal));
        return rows > ceiling
            ? [$"{rows} dated rows, above DatedCeiling {ceiling}: a new site takes keys-first or an exemption, never a dated row"]
            : rows < ceiling
                ? [$"{rows} dated rows, under DatedCeiling {ceiling}: lower DatedCeiling to {rows}, so the rows this PR ended cannot come back"]
                : [];
    }

    [Fact]
    public void ThePlantedCommandSitesAreCaught()
    {
        const string Planted = """
            using System.Windows;
            using System.Windows.Controls;
            using System.Windows.Controls.Primitives;
            using System.Windows.Data;
            using System.Windows.Input;

            namespace Planted;

            internal sealed class Commands
            {
                private readonly Button _button = new();
                private readonly Button _field;
                private readonly Button Unfocused = new();
                private readonly MenuItem _menuItem = new();
                private readonly MenuItem _stays = new() { StaysOpenOnClick = true };
                private readonly ICommand _command = new RoutedCommand();

                public Commands()
                {
                    _field = new Button { Command = _command };
                    _button.SetBinding(UIElement.IsEnabledProperty, new Binding("Ready"));
                }

                private void Assign() => _button.Command = _command;
                private void Clear(Button item) => item.Command = null;
                private void Row() => _menuItem.Command = _command;
                private void Stay() => _stays.Command = _command;
                private void Caption() => Unfocused.Command = _command;
                private void Set() => _button.SetValue(ButtonBase.CommandProperty, _command);
                private void Bind() => BindingOperations.SetBinding(_button, ButtonBase.CommandProperty, new Binding("Command"));
            }
            """;
        CSharpParseOptions options = (CSharpParseOptions)ShellCompilation.Sources[0].Source.Root.SyntaxTree.Options;
        SyntaxTree tree = CSharpSyntaxTree.ParseText(Planted, options, path: "Planted.cs");
        CSharpCompilation compilation = ShellCompilation.Compilation.AddSyntaxTrees(tree);
        XDocument xaml = XDocument.Parse(
            """
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:local="clr-namespace:SlateWindows">
              <Window.InputBindings>
                <KeyBinding Key="O" Modifiers="Control" Command="{Binding OpenCommand}" />
              </Window.InputBindings>
              <StackPanel>
                <Button x:Name="Pressed" Command="{Binding PressCommand}" />
                <Button x:Name="Unfocused" Focusable="False" Command="{Binding PressCommand}" />
                <TextBlock><Hyperlink x:Name="Linked" Command="{Binding FollowCommand}">Link</Hyperlink></TextBlock>
                <CheckBox x:Name="Checked" Command="{Binding Path=Model.CheckCommand}" />
                <Menu>
                  <MenuItem Header="Row" Command="{Binding RowCommand}" />
                  <local:CheckMenuItem Header="Check" Command="{Binding CheckRowCommand}" />
                  <MenuItem x:Name="StaysOpen" StaysOpenOnClick="True" Command="{Binding StayCommand}" />
                  <MenuItem x:Name="StyledOpen" Command="{Binding StyledCommand}">
                    <MenuItem.Style>
                      <Style TargetType="MenuItem">
                        <Setter Property="StaysOpenOnClick" Value="True" />
                      </Style>
                    </MenuItem.Style>
                  </MenuItem>
                </Menu>
                <Border>
                  <Border.ContextMenu>
                    <ContextMenu>
                      <MenuItem Header="Context" Command="{Binding ContextCommand}" />
                    </ContextMenu>
                  </Border.ContextMenu>
                </Border>
                <MenuItem x:Name="Loose" Command="{Binding LooseCommand}" />
                <WrapPanel x:Name="Group">
                  <Button Command="{Binding SaveCommand}" />
                  <Button Command="{Binding RevertCommand}" />
                  <Button Command="{Binding RevertCommand}" />
                </WrapPanel>
              </StackPanel>
            </Window>
            """,
            LoadOptions.SetLineInfo);

        SortedDictionary<string, Site> sites = Scrape(
            [((CompilationUnitSyntax)tree.GetRoot(), compilation.GetSemanticModel(tree))], [("Planted.xaml", xaml)]);

        Assert.Equal(
            [
                "command Planted.Commands..ctor _field",
                "command Planted.Commands.Assign _button",
                "command Planted.Commands.Bind _button",
                "command Planted.Commands.Set _button",
                "command Planted.Commands.Stay _stays",
                "ui-state Planted.Commands..ctor _button IsEnabled",
                "xaml-command Planted.xaml Checked CheckCommand",
                "xaml-command Planted.xaml Group RevertCommand",
                "xaml-command Planted.xaml Group RevertCommand #2",
                "xaml-command Planted.xaml Group SaveCommand",
                "xaml-command Planted.xaml Linked FollowCommand",
                "xaml-command Planted.xaml Loose LooseCommand",
                "xaml-command Planted.xaml Pressed PressCommand",
                "xaml-command Planted.xaml StaysOpen StayCommand",
                "xaml-command Planted.xaml StyledOpen StyledCommand",
            ],
            sites.Keys.Where(key => key.StartsWith("command ", StringComparison.Ordinal)
                || key.StartsWith("ui-state ", StringComparison.Ordinal)
                || key.StartsWith("xaml-command ", StringComparison.Ordinal)));
        Assert.Equal(["Group"], sites["xaml-command Planted.xaml Group SaveCommand"].Names);
        Assert.Equal(["SaveCommand"], sites["xaml-command Planted.xaml Group SaveCommand"].Tokens);
        Assert.Equal("CheckCommand", sites["xaml-command Planted.xaml Checked CheckCommand"].Command);
    }

    [Fact]
    public void TheIsEnabledTriggersAreKeyedOneByOne()
    {
        XDocument xaml = XDocument.Parse(
            """
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <StackPanel>
                <Button x:Name="Styled">
                  <Button.Style>
                    <Style TargetType="Button">
                      <Style.Triggers>
                        <DataTrigger Binding="{Binding SelectedItem, ElementName=List}" Value="{x:Null}">
                          <Setter Property="IsEnabled" Value="False" />
                        </DataTrigger>
                        <DataTrigger Binding="{Binding Path=Model.IsBusy}" Value="True">
                          <Setter Property="IsEnabled" Value="False" />
                        </DataTrigger>
                        <MultiDataTrigger>
                          <MultiDataTrigger.Conditions>
                            <Condition Binding="{Binding Sheet.IsOpen}" Value="True" />
                            <Condition Binding="{Binding IsKeyboardFocusWithin, RelativeSource={RelativeSource Self}}" Value="False" />
                          </MultiDataTrigger.Conditions>
                          <Setter Property="IsEnabled" Value="False" />
                        </MultiDataTrigger>
                        <DataTrigger Binding="{Binding Model.IsBusy, Converter={StaticResource Same}}" Value="True">
                          <Setter Property="IsEnabled" Value="False" />
                        </DataTrigger>
                        <DataTrigger Binding="{Binding Ready}">
                          <DataTrigger.Value>
                            <x:Null />
                          </DataTrigger.Value>
                          <Setter Property="IsEnabled" Value="False" />
                        </DataTrigger>
                        <DataTrigger Binding="{Binding Ready}" Value="True">
                          <Setter Property="IsEnabled" Value="True" />
                        </DataTrigger>
                        <DataTrigger Binding="{Binding Hidden}" Value="True">
                          <Setter Property="Visibility" Value="Collapsed" />
                        </DataTrigger>
                        <DataTrigger Binding="{Binding Gone}" Value="True">
                          <Setter Property="Visibility" Value="Collapsed" />
                        </DataTrigger>
                      </Style.Triggers>
                    </Style>
                  </Button.Style>
                </Button>
                <Button x:Name="Defaulted">
                  <Button.Style>
                    <Style TargetType="Button">
                      <Setter Property="IsEnabled" Value="False" />
                      <Style.Triggers>
                        <DataTrigger Binding="{Binding IsDirty}" Value="True">
                          <Setter Property="IsEnabled" Value="True" />
                        </DataTrigger>
                      </Style.Triggers>
                    </Style>
                  </Button.Style>
                </Button>
                <Button x:Name="BoundStyle">
                  <Button.Style>
                    <Style TargetType="Button">
                      <Setter Property="IsEnabled" Value="{Binding Ready}" />
                    </Style>
                  </Button.Style>
                </Button>
                <ContentControl x:Name="Templated" Focusable="False">
                  <ContentControl.Template>
                    <ControlTemplate TargetType="ContentControl">
                      <StackPanel>
                        <Button x:Name="Part" />
                        <ContentPresenter />
                      </StackPanel>
                      <ControlTemplate.Triggers>
                        <Trigger SourceName="Part" Property="IsMouseOver" Value="True">
                          <Setter TargetName="Part" Property="IsEnabled" Value="False" />
                        </Trigger>
                      </ControlTemplate.Triggers>
                    </ControlTemplate>
                  </ContentControl.Template>
                </ContentControl>
                <ListBox x:Name="List" />
              </StackPanel>
            </Window>
            """,
            LoadOptions.SetLineInfo);

        SortedDictionary<string, Site> sites = Scrape([], [("Planted.xaml", xaml)]);

        Assert.Equal(
            [
                "xaml-trigger Planted.xaml BoundStyle IsEnabled when (style binding)",
                "xaml-trigger Planted.xaml Defaulted IsEnabled when (style default)",
                "xaml-trigger Planted.xaml Part IsEnabled when Part.IsMouseOver=True",
                "xaml-trigger Planted.xaml Styled IsEnabled when List.SelectedItem={x:Null}",
                "xaml-trigger Planted.xaml Styled IsEnabled when Model.IsBusy=True",
                "xaml-trigger Planted.xaml Styled IsEnabled when Model.IsBusy=True #2",
                "xaml-trigger Planted.xaml Styled IsEnabled when Ready=(element)",
                "xaml-trigger Planted.xaml Styled IsEnabled when Sheet.IsOpen=True & IsKeyboardFocusWithin=False",
                "xaml-trigger Planted.xaml Styled Visibility",
            ],
            sites.Keys.Where(key => key.StartsWith("xaml-trigger ", StringComparison.Ordinal)));
        Assert.Equal(["List"], sites["xaml-trigger Planted.xaml Styled IsEnabled when List.SelectedItem={x:Null}"].Tokens);
        Assert.Equal(["IsBusy"], sites["xaml-trigger Planted.xaml Styled IsEnabled when Model.IsBusy=True"].Tokens);
        Assert.Equal(["Sheet"], sites["xaml-trigger Planted.xaml Styled IsEnabled when Sheet.IsOpen=True & IsKeyboardFocusWithin=False"].Tokens);
        Assert.Equal(["Styled"], sites["xaml-trigger Planted.xaml Styled IsEnabled when Ready=(element)"].Names);
        Assert.Empty(sites["xaml-trigger Planted.xaml Defaulted IsEnabled when (style default)"].Tokens!);
    }

    /// <summary>A keys-first fact exists, its code names the site (and a trigger's
    /// condition token), and its own method calls the recorder, qualified: not in a
    /// string, a nameof, a method group, a look-alike of its own class, a helper it calls,
    /// or the list form. The sites are planted as the scrape keys them (fact 2 holds the
    /// scrape to its tokens).</summary>
    [Fact]
    public void AKeysFirstWitnessNamesTheSiteItsTokenAndCallsTheRecorder()
    {
        var pressed = new Site("xaml-command Planted.xaml Pressed PressCommand", null, null, null, null, ["Pressed"], null, "PressCommand");
        var sheet = new Site(
            "xaml-trigger Planted.xaml MainMenu IsEnabled when Workspace.AddPropertySheet=True", null, null, null, null, ["MainMenu"], ["AddPropertySheet"]);
        var judge = new WitnessJudge(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["PlantedTests.Holds"] = "[Fact] void Holds() => RunSta(() => { var button = host.Pressed; KeysFirstRecorder.AssertTheKeysLeaveFirst(\"Pressed\", button, () => button.Command.Execute(null)); });",
                ["PlantedTests.TheSheet"] = "[Fact] void TheSheet() => RunSta(() => { var menu = host.Shell.MainMenu; KeysFirstRecorder.AssertTheKeysLeaveFirst(\"menu\", menu, () => host.Workspace.AddPropertySheet = new()); });",
                ["PlantedTests.NoCall"] = "[Fact] void NoCall() => RunSta(() => { var button = host.Pressed; button.Command.Execute(null); });",
                ["PlantedTests.NoName"] = "[Fact] void NoName() => RunSta(() => { var button = host.Other; KeysFirstRecorder.AssertTheKeysLeaveFirst(\"button\", button, () => { }); });",
                ["PlantedTests.InAString"] = "[Fact] void InAString() => RunSta(() => { var button = host.Pressed; Log(\"KeysFirstRecorder.AssertTheKeysLeaveFirst(button)\"); });",
                ["PlantedTests.ByNameof"] = "[Fact] void ByNameof() => RunSta(() => { var button = host.Pressed; Log(nameof(KeysFirstRecorder.AssertTheKeysLeaveFirst)); });",
                ["PlantedTests.AsAGroup"] = "[Fact] void AsAGroup() => RunSta(() => { var button = host.Pressed; Run(KeysFirstRecorder.AssertTheKeysLeaveFirst); });",
                ["PlantedTests.Unqualified"] = "[Fact] void Unqualified() => RunSta(() => { var button = host.Pressed; AssertTheKeysLeaveFirst(\"Pressed\", button, () => { }); });",
                ["PlantedTests.InAHelper"] = "[Fact] void InAHelper() => RunSta(() => { var button = host.Pressed; PressUnderTheRecorder(button); });",
                ["PlantedTests.TheList"] = "[Fact] void TheList() => RunSta(() => { var button = host.Pressed; Assert.Empty(KeysFirstRecorder.DisabledWithTheKeysInside(button, () => { })); });",
                ["PlantedTests.NoToken"] = "[Fact] void NoToken() => RunSta(() => { var menu = host.Shell.MainMenu; KeysFirstRecorder.AssertTheKeysLeaveFirst(\"menu\", menu, () => host.Workspace.Open()); });",
            },
            new HashSet<string>(StringComparer.Ordinal),
            []);

        Assert.Null(judge.Judge(pressed, "keys-first: PlantedTests.Holds"));
        Assert.Null(judge.Judge(sheet, "keys-first: PlantedTests.TheSheet"));
        Assert.Contains("calls", judge.Judge(pressed, "keys-first: PlantedTests.NoCall"), StringComparison.Ordinal);
        Assert.Contains("does not name the site", judge.Judge(pressed, "keys-first: PlantedTests.NoName"), StringComparison.Ordinal);
        Assert.Contains("calls", judge.Judge(pressed, "keys-first: PlantedTests.InAString"), StringComparison.Ordinal);
        Assert.Contains("calls", judge.Judge(pressed, "keys-first: PlantedTests.ByNameof"), StringComparison.Ordinal);
        Assert.Contains("calls", judge.Judge(pressed, "keys-first: PlantedTests.AsAGroup"), StringComparison.Ordinal);
        Assert.Contains("calls", judge.Judge(pressed, "keys-first: PlantedTests.Unqualified"), StringComparison.Ordinal);
        Assert.Contains("calls", judge.Judge(pressed, "keys-first: PlantedTests.InAHelper"), StringComparison.Ordinal);
        Assert.Contains("calls", judge.Judge(pressed, "keys-first: PlantedTests.TheList"), StringComparison.Ordinal);
        Assert.Contains("names no fact", judge.Judge(pressed, "keys-first: NoSuchTests.Method"), StringComparison.Ordinal);
        Assert.Contains("AddPropertySheet", judge.Judge(sheet, "keys-first: PlantedTests.NoToken"), StringComparison.Ordinal);
    }

    [Fact]
    public void ADisableSiteTakesOnlyKeysFirstAnExemptionOrADate()
    {
        XDocument xaml = XDocument.Parse(
            """
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <Window.Resources>
                <Style x:Key="Quiet" TargetType="Button">
                  <Setter Property="Focusable" Value="False" />
                  <Style.Triggers>
                    <DataTrigger Binding="{Binding IsModalSurfaceOpen}" Value="True">
                      <Setter Property="IsEnabled" Value="False" />
                    </DataTrigger>
                  </Style.Triggers>
                </Style>
                <Style x:Key="Loud" TargetType="Button">
                  <Setter Property="Focusable" Value="False" />
                  <Style.Triggers>
                    <DataTrigger Binding="{Binding IsModalSurfaceOpen}" Value="True">
                      <Setter Property="IsEnabled" Value="False" />
                    </DataTrigger>
                  </Style.Triggers>
                </Style>
              </Window.Resources>
              <Grid x:Name="Columns">
                <Border x:Name="FilesPaneBorder">
                  <StackPanel>
                    <Button x:Name="Bound" IsEnabled="{Binding Ready}" />
                    <Button x:Name="Triggered">
                      <Button.Style>
                        <Style TargetType="Button">
                          <Style.Triggers>
                            <DataTrigger Binding="{Binding Model.IsBusy}" Value="True">
                              <Setter Property="IsEnabled" Value="False" />
                            </DataTrigger>
                          </Style.Triggers>
                        </Style>
                      </Button.Style>
                    </Button>
                    <Button x:Name="Pressed" Command="{Binding PressCommand}" />
                    <Button x:Name="Shown" Visibility="{Binding Shown}" />
                    <Button x:Name="Unfocused" Focusable="False" />
                    <Button x:Name="TitleButton" Style="{StaticResource Quiet}" />
                    <Button x:Name="OpenButton" Style="{StaticResource Loud}" Focusable="True" />
                  </StackPanel>
                </Border>
              </Grid>
            </Window>
            """,
            LoadOptions.SetLineInfo);
        SortedDictionary<string, Site> sites = Scrape([], [("Planted.xaml", xaml)]);
        Site bound = sites["xaml-binding Planted.xaml Bound IsEnabled"];
        Site shown = sites["xaml-binding Planted.xaml Shown Visibility"];
        var judge = new WitnessJudge(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["PlantedTests.NamesThem"] = "[Fact] void NamesThem() { host.Bound.IsEnabled = false; host.Pressed.Command = null; host.Triggered.IsEnabled = false; }",
                ["PlantedTests.NeverOpens"] = "[Fact] void NeverOpens() { Assert.False(host.Model.IsBusy); }",
            },
            new HashSet<string>(["Columns", "FilesPaneBorder"], StringComparer.Ordinal),
            [xaml]);

        // A guard lands the keys once they are lost; it does not keep a control from
        // going off while it holds them.
        Assert.Null(judge.Judge(shown, "guard: FilesPaneBorder"));
        Assert.NotNull(judge.Judge(bound, "guard: FilesPaneBorder"));
        Site pressed = sites["xaml-command Planted.xaml Pressed PressCommand"];
        Site triggered = sites["xaml-trigger Planted.xaml Triggered IsEnabled when Model.IsBusy=True"];
        Site quiet = sites["xaml-trigger Planted.xaml Style:Quiet IsEnabled when IsModalSurfaceOpen=True"];
        Site loud = sites["xaml-trigger Planted.xaml Style:Loud IsEnabled when IsModalSurfaceOpen=True"];
        var unfocused = new Site("ui-state Planted.View.Disable Unfocused IsEnabled", null, null, null, "Unfocused", ["Unfocused", "View"]);
        foreach (Site site in new[] { bound, triggered, pressed })
        {
            Assert.NotNull(judge.Judge(site, "guard: FilesPaneBorder"));
            Assert.NotNull(judge.Judge(site, "PlantedTests.NamesThem"));
            Assert.NotNull(judge.Judge(site, "exempt: covered by PlantedTests.NamesThem"));
            Assert.Null(judge.Judge(site, "exempt: its own press never turns it off: it reads Ready, which no press of its changes"));
            Assert.NotNull(judge.Judge(site, "exempt: its own press never turns it off: fine"));
            Assert.NotNull(judge.Judge(site, "exempt: its own press never turns it off: it reads Ready, as PlantedTests.NoSuchFact shows"));
            Assert.NotNull(judge.Judge(site, "exempt: not focusable"));
        }

        Assert.Null(judge.Judge(quiet, "exempt: not focusable"));
        Assert.Null(judge.Judge(unfocused, "exempt: not focusable"));
        Assert.NotNull(judge.Judge(loud, "exempt: not focusable"));
        Assert.Null(judge.Judge(triggered, "exempt: never fires: the model is never busy here (PlantedTests.NeverOpens)"));
        Assert.NotNull(judge.Judge(triggered, "exempt: never fires: the model is never busy here"));
        Assert.NotNull(judge.Judge(triggered, "exempt: never fires: the model is never busy here (PlantedTests.NoSuchFact)"));
        Assert.NotNull(judge.Judge(pressed, "exempt: never fires: a command is no trigger (PlantedTests.NeverOpens)"));
    }

    [Fact]
    public void AnAlwaysOnExemptionReadsTheCommand()
    {
        const string Planted = """
            using System.Threading.Tasks;
            using System.Windows.Input;
            using SlateWindows;

            namespace Planted;

            internal interface IPlantedHost
            {
                ICommand PlantedSharedCommand { get; }
            }

            internal sealed class AlwaysOn : IPlantedHost
            {
                private RelayCommand? _lazy;
                private readonly ICommand _admitted;

                public AlwaysOn()
                {
                    PlantedCtorCommand = new RelayCommand(_ => { }, _ => true);
                    _admitted = new AsyncRelayCommand(_ => Task.CompletedTask, () => true, admitWhileExecuting: true);
                    PlantedStateCommand = new RelayCommand(_ => { }, _ => IsOpen);
                    PlantedBusyCommand = new AsyncRelayCommand(_ => Task.CompletedTask, () => true);
                    PlantedSharedCommand = new RelayCommand(_ => { }, _ => true);
                    PlantedTwinCommand = new RelayCommand(_ => { }, _ => true);
                    PlantedRecordCommand = new RelayCommand(_ => { }, _ => true);
                }

                public bool IsOpen { get; set; }
                public ICommand PlantedCtorCommand { get; }
                public ICommand PlantedLazyCommand => _lazy ??= new RelayCommand(_ => { }, _ => true);
                public ICommand PlantedAdmittedCommand => _admitted;
                public ICommand PlantedStateCommand { get; }
                public ICommand PlantedBusyCommand { get; }
                public ICommand PlantedSharedCommand { get; }
                public ICommand PlantedTwinCommand { get; }
                public ICommand PlantedRecordCommand { get; }
            }

            internal sealed class Forwarder(AlwaysOn row)
            {
                public ICommand PlantedForwardCommand => row.PlantedCtorCommand;
            }

            internal sealed class Twin
            {
                public Twin() => PlantedTwinCommand = new RelayCommand(_ => { }, _ => Ready);

                public bool Ready { get; set; }
                public ICommand PlantedTwinCommand { get; }
            }

            internal sealed record PlantedRow(ICommand PlantedRecordCommand);
            """;
        CSharpParseOptions options = (CSharpParseOptions)ShellCompilation.Sources[0].Source.Root.SyntaxTree.Options;
        CSharpCompilation compilation = ShellCompilation.Compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(Planted, options, path: "Planted.cs"));
        var judge = new WitnessJudge(
            new Dictionary<string, string>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal), [], commands: compilation);
        static Site Bound(string command) => new($"xaml-command Planted.xaml Thing {command}", null, null, null, null, ["Thing"], Command: command);

        Assert.Null(judge.Judge(Bound("PlantedCtorCommand"), "exempt: always on: AlwaysOn.PlantedCtorCommand"));
        Assert.Null(judge.Judge(Bound("PlantedLazyCommand"), "exempt: always on: AlwaysOn.PlantedLazyCommand"));
        Assert.Null(judge.Judge(Bound("PlantedAdmittedCommand"), "exempt: always on: AlwaysOn.PlantedAdmittedCommand"));
        Assert.Null(judge.Judge(Bound("PlantedForwardCommand"), "exempt: always on: Forwarder.PlantedForwardCommand"));
        Assert.Null(judge.Judge(Bound("PlantedSharedCommand"), "exempt: always on: AlwaysOn.PlantedSharedCommand"));
        Assert.NotNull(judge.Judge(Bound("PlantedStateCommand"), "exempt: always on: AlwaysOn.PlantedStateCommand"));
        Assert.NotNull(judge.Judge(Bound("PlantedBusyCommand"), "exempt: always on: AlwaysOn.PlantedBusyCommand"));
        Assert.NotNull(judge.Judge(Bound("PlantedCtorCommand"), "exempt: always on: Twin.PlantedCtorCommand"));
        Assert.NotNull(judge.Judge(Bound("PlantedOtherCommand"), "exempt: always on: AlwaysOn.PlantedCtorCommand"));
        Assert.NotNull(judge.Judge(Bound("PlantedTwinCommand"), "exempt: always on: AlwaysOn.PlantedTwinCommand"));
        Assert.NotNull(judge.Judge(Bound("PlantedRecordCommand"), "exempt: always on: AlwaysOn.PlantedRecordCommand"));
    }

    [Fact]
    public void ADatedWitnessHasItsDateItsOwnerAndAMechanism()
    {
        var bound = new Site("xaml-binding Planted.xaml Apply IsEnabled", null, null, null, null, ["Apply"]);
        var outside = new Site("xaml-binding Planted.xaml Preview IsEnabled", null, null, null, null, ["Preview"]);
        var shown = new Site("xaml-binding Planted.xaml Apply Visibility", null, null, null, null, ["Apply"]);
        var judge = new WitnessJudge(
            new Dictionary<string, string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal),
            [],
            dated: [bound.Key, shown.Key]);

        Assert.Null(judge.Judge(bound, $"dated: {DatedOn}; CB-17b (CD11): its press starts the apply and CanApply falls"));
        Assert.Contains(DatedOn, judge.Judge(bound, "dated: 2026-11-22; CB-17b (CD11): its press starts the apply"), StringComparison.Ordinal);
        Assert.Contains("owned by", judge.Judge(bound, $"dated: {DatedOn}; CB-18 (CD11): its press starts the apply"), StringComparison.Ordinal);
        Assert.Contains("says how", judge.Judge(bound, $"dated: {DatedOn}; CB-17b (CD11):  "), StringComparison.Ordinal);
        Assert.Contains("DatedBaseline", judge.Judge(outside, $"dated: {DatedOn}; CB-17b (CD11): its press runs the preview"), StringComparison.Ordinal);
        Assert.Contains("only for a site", judge.Judge(shown, $"dated: {DatedOn}; CB-17b (CD11): it hides"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheDatedRowsOnlyShrink()
    {
        string row = $"dated: {DatedOn}; CB-17b (CD11): its press starts the work";
        var two = new Dictionary<string, string>(StringComparer.Ordinal) { ["a"] = row, ["b"] = row, ["c"] = "guard: Pane" };
        var three = new Dictionary<string, string>(two, StringComparer.Ordinal) { ["d"] = row };
        var one = new Dictionary<string, string>(StringComparer.Ordinal) { ["a"] = row, ["c"] = "guard: Pane" };

        Assert.Empty(DatedProblems(two, ceiling: 2));
        Assert.Contains(DatedProblems(three, ceiling: 2), problem => problem.Contains("never a dated row", StringComparison.Ordinal));
        Assert.Contains(DatedProblems(one, ceiling: 2), problem => problem.Contains("lower DatedCeiling to 1", StringComparison.Ordinal));
    }

    [Fact]
    public void TheDatedBaselineHoldsTheRowsOfD()
    {
        Assert.Equal(46, DatedBaseline.Length);
        Assert.Equal(DatedBaseline.Length, DatedBaseline.Distinct(StringComparer.Ordinal).Count());
        Assert.All(DatedBaseline, key => Assert.True(IsDisableSite(key), $"{key} can turn no control off"));
    }

    /// <summary>An IsEnabled setter's trigger, read for its own key: its conditions as
    /// written and the token a keys-first fact must name. One that sets True turns
    /// nothing off.</summary>
    internal sealed record TriggerCondition(string Text, IReadOnlyList<string> Tokens, XElement Trigger, bool SetsTrue);

    private static TriggerCondition ConditionOf(XElement setter)
    {
        bool setsTrue = (string?)setter.Attribute("Value") == "True";
        XElement? trigger = setter.Ancestors().FirstOrDefault(ancestor =>
            ancestor.Name.LocalName is "DataTrigger" or "Trigger" or "MultiDataTrigger" or "MultiTrigger");
        if (trigger is null)
        {
            return new TriggerCondition("(unknown)", [], setter, setsTrue);
        }

        IEnumerable<XElement> conditions = trigger.Name.LocalName.StartsWith("Multi", StringComparison.Ordinal)
            ? trigger.Elements().Where(element => element.Name.LocalName.EndsWith(".Conditions", StringComparison.Ordinal))
                .Elements().Where(element => element.Name.LocalName == "Condition")
            : [trigger];
        (string Text, string? Token)[] read = [.. conditions.Select(Describe)];
        return new TriggerCondition(string.Join(" & ", read.Select(part => part.Text)), [.. read.Select(part => part.Token).OfType<string>()], trigger, setsTrue);
    }

    /// <summary>A condition as written: its binding's path (Path= dropped) after its
    /// ElementName, or a property trigger's property after its SourceName, then = and its
    /// value. Its token is the path's last segment that says more than open, focused or
    /// selected; failing that, its ElementName or SourceName.</summary>
    private static (string Text, string? Token) Describe(XElement condition)
    {
        string path;
        string? named;
        if ((string?)condition.Attribute("Binding") is { } binding)
        {
            (path, named) = BindingParts(binding);
        }
        else if (condition.Elements().Any(element => element.Name.LocalName.EndsWith(".Binding", StringComparison.Ordinal)))
        {
            (path, named) = ("(binding)", null);
        }
        else
        {
            (path, named) = ((string?)condition.Attribute("Property") ?? string.Empty, (string?)condition.Attribute("SourceName"));
        }

        string value = (string?)condition.Attribute("Value")
            ?? (condition.Elements().Any(element => element.Name.LocalName.EndsWith(".Value", StringComparison.Ordinal)) ? "(element)" : string.Empty);
        string? token = path == "(binding)"
            ? named
            : path.Split('.').LastOrDefault(segment => segment.Length > 0 && !Untokened.Contains(segment)) ?? named;
        return ($"{(named is null ? string.Empty : named + ".")}{path}={value}", token);
    }

    /// <summary>A binding's path and ElementName, from its markup.</summary>
    private static (string Path, string? ElementName) BindingParts(string markup)
    {
        string inner = markup.Trim();
        inner = inner.StartsWith('{') && inner.EndsWith('}') ? inner[1..^1] : inner;
        int space = inner.IndexOf(' ', StringComparison.Ordinal);
        string path = string.Empty;
        string? element = null;
        foreach (string part in TopLevel(space < 0 ? string.Empty : inner[(space + 1)..], ','))
        {
            string[] named = [.. TopLevel(part, '=')];
            if (named.Length == 1)
            {
                path = part.Trim();
            }
            else if (named[0].Trim() == "Path")
            {
                path = named[1].Trim();
            }
            else if (named[0].Trim() == "ElementName")
            {
                element = named[1].Trim();
            }
        }

        return (path, element);
    }

    /// <summary>A markup string split at <paramref name="separator"/> outside braces.</summary>
    private static IEnumerable<string> TopLevel(string text, char separator)
    {
        int depth = 0;
        int start = 0;
        for (int index = 0; index < text.Length; index++)
        {
            depth += text[index] switch { '{' => 1, '}' => -1, _ => 0 };
            if (depth == 0 && text[index] == separator)
            {
                yield return text[start..index];
                start = index + 1;
            }
        }

        yield return text[start..];
    }

    /// <summary>A style's own IsEnabled setter: False turns the control off whenever no
    /// trigger setting True applies; a markup value can turn it off at any time.</summary>
    private static void ScrapeStyleSetters(SortedDictionary<string, Site> sites, string file, XDocument document)
    {
        foreach (XElement style in document.Descendants().Where(element => element.Name.LocalName == "Style"))
        {
            foreach (string? value in OwnSetters(style)
                .Where(setter => (string?)setter.Attribute("Property") == "IsEnabled")
                .Select(setter => (string?)setter.Attribute("Value")
                    ?? (setter.Elements().Any(element => element.Name.LocalName == "Setter.Value") ? "{" : null)))
            {
                string? when = value == "False" ? "(style default)" : value?.StartsWith('{') == true ? "(style binding)" : null;
                if (when is not null && CarrierOf(style, null) is { } carried)
                {
                    string key = $"xaml-trigger {file} {carried.Identity} IsEnabled when {when}";
                    sites[key] = new Site(key, carried.Element, carried.ResourceKey, null, null, carried.Names, []);
                }
            }
        }
    }

    private static IEnumerable<XElement> OwnSetters(XElement style) =>
        style.Elements()
            .SelectMany(child => child.Name.LocalName == "Style.Setters" ? child.Elements() : [child])
            .Where(element => element.Name.LocalName == "Setter");

    private static string? StyleValue(XElement style, string property) =>
        OwnSetters(style).Where(setter => (string?)setter.Attribute("Property") == property)
            .Select(setter => (string?)setter.Attribute("Value")).LastOrDefault();

    /// <summary>Every Command= on an element that can take the keys itself, keyed by its
    /// own name, or by its nearest named holder and its command.</summary>
    private static void ScrapeXamlCommands(SortedDictionary<string, Site> sites, string file, XDocument document, IReadOnlyList<XDocument> all)
    {
        var unnamed = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (XElement element in document.Descendants().Where(element => element.Attribute("Command") is not null
            && TakesTheKeysItself(element)
            && !IsAMenuRow(element, all)))
        {
            string command = CommandName((string)element.Attribute("Command")!);
            string? own = Named(element);
            string? holder = own ?? element.Ancestors().Select(Named).FirstOrDefault(name => name is not null);
            string key = $"xaml-command {file} {holder ?? "the window"} {command}";
            if (own is null)
            {
                int ordinal = unnamed[key] = unnamed.GetValueOrDefault(key) + 1;
                key += ordinal > 1 ? $" #{ordinal}" : string.Empty;
            }

            sites[key] = new Site(key, element, null, null, null, holder is null ? [] : [holder], own is null ? [command] : null, command);
        }
    }

    private static bool TakesTheKeysItself(XElement element) =>
        element.Name == PresentationNamespace + "Hyperlink"
            ? (string?)element.Attribute("Focusable") != "False"
            : RegionBoundaryCensus.IsFocusable(element);

    /// <summary>A menu row: a MenuItem in a Menu or a ContextMenu that does not stay
    /// open on click. WPF closes its menu and gives the keys back before its command runs
    /// (MenuRowCommandTests).</summary>
    private static bool IsAMenuRow(XElement element, IReadOnlyList<XDocument> all) =>
        RegionBoundaryCensus.Resolve(element) is { } type
        && typeof(MenuItem).IsAssignableFrom(type)
        && element.Ancestors().Any(ancestor => RegionBoundaryCensus.Resolve(ancestor) is { } menu && typeof(MenuBase).IsAssignableFrom(menu))
        && !StaysOpenOnClick(element, all);

    private static bool StaysOpenOnClick(XElement element, IReadOnlyList<XDocument> all)
    {
        if ((string?)element.Attribute("StaysOpenOnClick") is { } own)
        {
            return own == "True";
        }

        IEnumerable<XElement> everyStyle = all.SelectMany(document => document.Descendants()).Where(candidate => candidate.Name.LocalName == "Style");
        string? keyed = (string?)element.Attribute("Style") is { } reference ? ResourceKeyOf(reference) : null;
        return element.Elements().Where(child => child.Name.LocalName.EndsWith(".Style", StringComparison.Ordinal)).Elements()
            .Concat(everyStyle.Where(style => keyed is not null && (string?)style.Attribute(Xaml + "Key") == keyed))
            .Concat(everyStyle.Where(style => style.Attribute(Xaml + "Key") is null
                && style.Parent?.Name.LocalName.EndsWith(".Resources", StringComparison.Ordinal) == true
                && (string?)style.Attribute("TargetType") is "MenuItem" or "{x:Type MenuItem}"))
            .Any(style => StyleValue(style, "StaysOpenOnClick") == "True");
    }

    private static string? ResourceKeyOf(string reference) =>
        reference.Trim().TrimStart('{').TrimEnd('}').Split(' ', StringSplitOptions.RemoveEmptyEntries) is [_, var key] ? key : null;

    /// <summary>A command's name: its binding path's last segment, or an x:Static's
    /// member.</summary>
    private static string CommandName(string markup)
    {
        string path = markup.StartsWith("{Binding", StringComparison.Ordinal) || markup.StartsWith("{TemplateBinding", StringComparison.Ordinal)
            ? BindingParts(markup).Path
            : markup.Trim().TrimStart('{').TrimEnd('}').Split(' ', StringSplitOptions.RemoveEmptyEntries).Last();
        string last = path.Split('.', ':').Last();
        return last.Length > 0 ? last : "(context)";
    }

    /// <summary>A C# write of a button's or a hyperlink's command, or a menu item's that
    /// its type keeps open on click: an assignment or an initializer, a constructor's too.
    /// A write of null, and a write to a named XAML element out of the keys' reach, are
    /// not sites.</summary>
    private static bool ScrapeACommandWrite(
        SortedDictionary<string, Site> sites, SemanticModel model, AssignmentExpressionSyntax assignment, IReadOnlySet<string> focusless)
    {
        if (model.GetSymbolInfo(assignment.Left).Symbol is not IPropertySymbol { Name: "Command" } property
            || !TakesACommand(property.ContainingType))
        {
            return false;
        }

        string receiver = assignment.Left is MemberAccessExpressionSyntax access
            ? access.Expression.ToString()
            : assignment.Parent is InitializerExpressionSyntax initializer ? InitializedReceiver(initializer) : "this";
        if (!IsNull(assignment.Right) && IsACommandSite(assignment, property.ContainingType, receiver, focusless))
        {
            Add(sites, model, assignment, "command", receiver, property: null, constructorWrite: true);
        }

        return true;
    }

    private static bool ScrapeACommandCall(
        SortedDictionary<string, Site> sites, SemanticModel model, InvocationExpressionSyntax call, MemberAccessExpressionSyntax member, IReadOnlySet<string> focusless)
    {
        bool operations = member.Expression.ToString() == "BindingOperations";
        int at = operations ? 1 : 0;
        SeparatedSyntaxList<ArgumentSyntax> arguments = call.ArgumentList.Arguments;
        if (member.Name.Identifier.ValueText is not ("SetValue" or "SetCurrentValue" or "SetBinding")
            || arguments.Count <= at
            || model.GetSymbolInfo(arguments[at].Expression).Symbol is not IFieldSymbol { Name: "CommandProperty" } field
            || !TakesACommand(field.ContainingType))
        {
            return false;
        }

        string receiver = operations ? arguments[0].Expression.ToString() : member.Expression.ToString();
        if (!(arguments.Count > at + 1 && IsNull(arguments[at + 1].Expression)) && IsACommandSite(call, field.ContainingType, receiver, focusless))
        {
            Add(sites, model, call, "command", receiver, property: null, constructorWrite: true);
        }

        return true;
    }

    private static bool TakesACommand(ITypeSymbol? type) =>
        Derives(type, "System.Windows.Controls.Primitives.ButtonBase")
        || Derives(type, "System.Windows.Documents.Hyperlink")
        || Derives(type, "System.Windows.Controls.MenuItem");

    private static bool IsACommandSite(SyntaxNode node, ITypeSymbol owner, string receiver, IReadOnlySet<string> focusless) =>
        !focusless.Contains(receiver.TrimEnd('!'))
        && (!Derives(owner, "System.Windows.Controls.MenuItem") || StaysOpenIn(node, receiver));

    /// <summary>Whether the type writing a menu item's command also keeps that item open
    /// on click.</summary>
    private static bool StaysOpenIn(SyntaxNode node, string receiver) =>
        node.Ancestors().OfType<TypeDeclarationSyntax>().LastOrDefault() is { } type
        && type.DescendantNodes().OfType<AssignmentExpressionSyntax>().Any(assignment =>
            assignment.Right.IsKind(SyntaxKind.TrueLiteralExpression)
            && (assignment.Left is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "StaysOpenOnClick" } access
                    && access.Expression.ToString() == receiver
                || assignment.Left is IdentifierNameSyntax { Identifier.ValueText: "StaysOpenOnClick" }
                    && assignment.Parent is InitializerExpressionSyntax initializer
                    && InitializedReceiver(initializer) == receiver));

    /// <summary>The receiver an object initializer builds: what the new object is
    /// assigned to.</summary>
    private static string InitializedReceiver(InitializerExpressionSyntax initializer) =>
        initializer.Parent?.Parent switch
        {
            AssignmentExpressionSyntax outer => outer.Left.ToString(),
            EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator } => declarator.Identifier.ValueText,
            EqualsValueClauseSyntax { Parent: PropertyDeclarationSyntax declared } => declared.Identifier.ValueText,
            _ => "new",
        };

    private static bool IsNull(ExpressionSyntax expression) =>
        expression is PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression } suppressed
            ? IsNull(suppressed.Operand)
            : expression.IsKind(SyntaxKind.NullLiteralExpression) || expression.IsKind(SyntaxKind.DefaultLiteralExpression);

    /// <summary>A constructor's binding of IsEnabled lives on and can turn the control
    /// off later, unlike a constructor's plain write.</summary>
    private static bool BindsAState(string call, string property) => call == "SetBinding" && property == "IsEnabled";

    /// <summary>#1866 (CD11): the witnesses of a site that can turn a control off, and the
    /// keys-first and dated kinds wherever they are written.</summary>
    internal sealed class KeysFirstRule(
        IReadOnlyDictionary<string, string> facts,
        IReadOnlyList<XDocument> documents,
        Compilation? commands,
        IReadOnlyCollection<string> dated)
    {
        private const string KeysFirst = "keys-first: ";
        private const string Dated = "dated: ";
        private const string AlwaysOn = "exempt: always on: ";
        private const string OwnPress = "exempt: its own press never turns it off: ";
        private const string NeverFires = "exempt: never fires: ";
        private CommandConstancy? _constancy;

        /// <summary>True when this rule rules on the witness, with its verdict (null when
        /// it holds).</summary>
        public bool Judges(Site site, string witness, out string? verdict)
        {
            bool disables = IsDisableSite(site.Key);
            verdict = null;
            if (witness.StartsWith(KeysFirst, StringComparison.Ordinal))
            {
                verdict = KeysFirstVerdict(site, witness[KeysFirst.Length..].Trim());
            }
            else if (witness.StartsWith(Dated, StringComparison.Ordinal))
            {
                verdict = disables ? DatedVerdict(site, witness[Dated.Length..]) : "a dated row is only for a site that can turn a control off with the keys on it";
            }
            else if (!disables)
            {
                return false;
            }
            else if (witness == "exempt: not focusable")
            {
                verdict = OutOfReach(site);
            }
            else if (witness.StartsWith(AlwaysOn, StringComparison.Ordinal))
            {
                verdict = commands is null
                    ? "no compilation to read the command in"
                    : (_constancy ??= new CommandConstancy(commands)).Verdict(site, witness[AlwaysOn.Length..].Trim());
            }
            else if (witness.StartsWith(OwnPress, StringComparison.Ordinal))
            {
                string why = witness[OwnPress.Length..].Trim();
                verdict = why.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 4
                    ? "say what its command or trigger reads, and that its own press does not change it"
                    : Uncited(why);
            }
            else if (witness.StartsWith(NeverFires, StringComparison.Ordinal))
            {
                Match cited = Regex.Match(witness[NeverFires.Length..], @"^(?<why>.*\S)\s+\((?<fact>[A-Z]\w*Tests\.[A-Z]\w*)\)$");
                verdict = !site.Key.StartsWith("xaml-trigger ", StringComparison.Ordinal) ? "only a trigger can never fire"
                    : !cited.Success ? "never fires says why, then cites the fact that shows it: (SomethingTests.Method)"
                    : Uncited(cited.Groups["fact"].Value);
            }
            else
            {
                verdict = "a site that can turn a control off with the keys on it (#1866, CD11) takes 'keys-first: Class.Method', "
                    + "'exempt: not focusable', 'exempt: always on: Type.Property', 'exempt: its own press never turns it off: <why>', "
                    + $"'exempt: never fires: <why> (Class.Method)' or 'dated: {DatedOn}; {DatedOwner}: <mechanism>'";
            }

            return true;
        }

        private string? KeysFirstVerdict(Site site, string fact)
        {
            if (!facts.TryGetValue(fact, out string? source))
            {
                return $"'{fact}' names no fact in this project";
            }

            HashSet<string> tokens = WitnessJudge.CodeTokens(source);
            return !site.Names.Any(name => name.Length > 2 && tokens.Contains(name))
                ? $"the fact's code does not name the site ({string.Join(", ", site.Names)})"
                : site.Tokens is { Count: > 0 } wanted && !wanted.Any(tokens.Contains)
                    ? $"the fact's code does not name the trigger's condition or the button's command ({string.Join(", ", wanted)})"
                    : CallsTheRecorder(source) ? null : "the fact's own code never calls KeysFirstRecorder.AssertTheKeysLeaveFirst";
        }

        /// <summary>An invocation of KeysFirstRecorder.AssertTheKeysLeaveFirst, qualified so,
        /// in the fact's own method: not a nameof, a method group, a string, a look-alike of
        /// the fact's own class, or a call inside a helper.</summary>
        private static bool CallsTheRecorder(string source) =>
            SyntaxFactory.ParseMemberDeclaration(source) is { } method
            && method.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(call =>
                call.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "AssertTheKeysLeaveFirst" } access
                && access.Expression switch
                {
                    IdentifierNameSyntax { Identifier.ValueText: "KeysFirstRecorder" } => true,
                    MemberAccessExpressionSyntax { Name.Identifier.ValueText: "KeysFirstRecorder" } => true,
                    _ => false,
                });

        private string? DatedVerdict(Site site, string rest)
        {
            int semicolon = rest.IndexOf("; ", StringComparison.Ordinal);
            string owned = semicolon < 0 ? string.Empty : rest[(semicolon + 2)..];
            return semicolon < 0 || rest[..semicolon] != DatedOn
                ? $"a dated row carries {DatedOn}, the one day the rows were written: 'dated: {DatedOn}; {DatedOwner}: <mechanism>'"
                : !owned.StartsWith(DatedOwner + ":", StringComparison.Ordinal)
                    ? $"a dated row is owned by {DatedOwner}, which ends it"
                    : owned[(DatedOwner.Length + 1)..].Trim().Length == 0
                        ? "a dated row says how the control goes off under the keys"
                        : dated.Contains(site.Key)
                            ? null
                            : "a new site takes keys-first or an exemption, never a dated row: DatedBaseline does not hold it";
        }

        private string? Uncited(string text) =>
            Regex.Matches(text, @"\b[A-Z]\w*Tests\.[A-Z]\w*\b").Select(match => match.Value)
                .FirstOrDefault(cited => !facts.ContainsKey(cited)) is { } missing
                ? $"the exemption cites '{missing}', which names no fact in this project"
                : null;

        /// <summary>Whether the site's control is out of the keys' reach: declared or styled
        /// not focusable, and holding nothing that can take them.</summary>
        private string? OutOfReach(Site site)
        {
            XElement[] all = [.. documents.SelectMany(document => document.Descendants())];
            if (site.ResourceKey is { } key)
            {
                XElement[] styles = [.. all.Where(element => element.Name.LocalName == "Style" && (string?)element.Attribute(Xaml + "Key") == key)];
                XElement? open = all.FirstOrDefault(user => user.Attributes().Any(attribute => attribute.Value.Contains($"Resource {key}}}", StringComparison.Ordinal))
                    && (user.Name.LocalName == "Style" ? StyleValue(user, "Focusable") == "True" : !Unreachable(user, all)));
                return styles.Length == 0 || !styles.All(style => StyleValue(style, "Focusable") == "False")
                    ? $"the style {key} does not set Focusable False"
                    : open is not null ? $"{Identity(open)} uses {key} and can take the keys" : null;
            }

            XElement? element = site.Element
                ?? (site.Member is { } member ? all.FirstOrDefault(candidate => (string?)candidate.Attribute(Xaml + "Name") == member.TrimEnd('!')) : null);
            return element is null ? "nothing in the XAML shows the site's control out of the keys' reach"
                : Unreachable(element, all) ? null
                : $"{Identity(element)} can take the keys, or holds what can";
        }

        private static bool Unreachable(XElement element, XElement[] all)
        {
            string? keyed = (string?)element.Attribute("Style") is { } reference ? ResourceKeyOf(reference) : null;
            bool declared = (string?)element.Attribute("Focusable") switch
            {
                "False" => true,
                null => element.Elements().Where(child => child.Name.LocalName.EndsWith(".Style", StringComparison.Ordinal)).Elements()
                    .Concat(all.Where(style => keyed is not null && style.Name.LocalName == "Style" && (string?)style.Attribute(Xaml + "Key") == keyed))
                    .Any(style => StyleValue(style, "Focusable") == "False"),
                _ => false,
            };
            Type? type = RegionBoundaryCensus.Resolve(element);
            return declared
                && !(type is not null && (typeof(ItemsControl).IsAssignableFrom(type) || type.Assembly == typeof(MainWindow).Assembly && typeof(Control).IsAssignableFrom(type)))
                && !(type is not null && typeof(ContentControl).IsAssignableFrom(type) && (string?)element.Attribute("Content") is { } content && content.StartsWith('{'))
                && !element.Elements()
                    .SelectMany(child => !child.Name.LocalName.Contains('.', StringComparison.Ordinal) ? [child]
                        : child.Name.LocalName.EndsWith(".Content", StringComparison.Ordinal) || child.Name.LocalName.EndsWith(".Child", StringComparison.Ordinal) ? child.Elements()
                        : [])
                    .Any(RegionBoundaryCensus.HoldsAStop);
        }
    }

    /// <summary>#1866 (CD11): whether a command is always on, read from every construction
    /// that reaches every property of its name in the shell, since a binding's data context
    /// is not resolved. Always on is a RelayCommand whose canExecute is a lambda of the
    /// literal true, or an AsyncRelayCommand of the same that admits while executing.
    /// Interface members are skipped; a record's parameter, or anything the reading cannot
    /// follow, is not always on.</summary>
    internal sealed class CommandConstancy(Compilation compilation)
    {
        private INamedTypeSymbol[]? _types;

        public string? Verdict(Site site, string named)
        {
            int dot = named.LastIndexOf('.');
            string type = dot > 0 ? named[..dot] : string.Empty;
            string property = named[(dot + 1)..];
            INamedTypeSymbol[] declared = [.. Types().Where(candidate => candidate.Name == type && candidate.TypeKind != TypeKind.Interface)];
            if (site.Command != property)
            {
                return $"the site runs {site.Command ?? "no bound command"}, not {property}";
            }

            if (declared.Length != 1 || !declared[0].GetMembers(property).OfType<IPropertySymbol>().Any())
            {
                return $"'{named}' names no one shell class's property";
            }

            return Types().Where(candidate => candidate.TypeKind != TypeKind.Interface)
                .SelectMany(candidate => candidate.GetMembers(property).OfType<IPropertySymbol>())
                .Select(same => Unsteady(same, new HashSet<ISymbol>(SymbolEqualityComparer.Default)) is { } why ? $"{same.ContainingType.Name}.{property} {why}" : null)
                .FirstOrDefault(why => why is not null);
        }

        private INamedTypeSymbol[] Types() =>
            _types ??= [.. compilation.GetSymbolsWithName(_ => true, SymbolFilter.Type).OfType<INamedTypeSymbol>()];

        private SemanticModel Model(SyntaxNode node) => compilation.GetSemanticModel(node.SyntaxTree);

        /// <summary>Null when every construction reaching <paramref name="property"/> is
        /// always on, else why not.</summary>
        private string? Unsteady(IPropertySymbol property, HashSet<ISymbol> seen)
        {
            if (!seen.Add(property) || property.DeclaringSyntaxReferences.IsEmpty)
            {
                return "is not a property the reading can follow";
            }

            if (property.SetMethod is { DeclaredAccessibility: not Microsoft.CodeAnalysis.Accessibility.Private } && property.SetMethod.DeclaringSyntaxReferences.Length > 0)
            {
                return "can be set by another type";
            }

            var built = new List<ExpressionSyntax>();
            bool forwarded = false;
            foreach (SyntaxNode node in property.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax()))
            {
                if (node is not PropertyDeclarationSyntax declaration)
                {
                    return "is not a property the reading can follow (a record's parameter)";
                }

                if (declaration.Initializer is { } initializer)
                {
                    built.Add(initializer.Value);
                }

                AccessorDeclarationSyntax? getter = declaration.AccessorList?.Accessors.FirstOrDefault(accessor => accessor.IsKind(SyntaxKind.GetAccessorDeclaration));
                ExpressionSyntax? body = declaration.ExpressionBody?.Expression
                    ?? getter?.ExpressionBody?.Expression
                    ?? (getter?.Body?.Statements is [ReturnStatementSyntax { Expression: { } returned }] ? returned : null);
                if (body is null && getter?.Body is not null)
                {
                    return "has a getter the reading cannot follow";
                }

                if (body is not null && Follow(Unwrapped(body), built, seen, ref forwarded) is { } why)
                {
                    return why;
                }
            }

            built.AddRange(Assigned(property));
            ExpressionSyntax[] made = [.. built.Select(Unwrapped).Where(expression => !IsNull(expression))];
            return made.Length == 0 && !forwarded ? "has no construction the reading can find"
                : made.FirstOrDefault(construction => !IsAlwaysOn(construction)) is { } off
                    ? $"is built by '{Regex.Replace(off.ToString(), @"\s+", " ")}', which can turn it off"
                : null;
        }

        /// <summary>A getter's expression: a construction, a lazy field (??=), a field, or
        /// another command property it forwards to.</summary>
        private string? Follow(ExpressionSyntax body, List<ExpressionSyntax> built, HashSet<ISymbol> seen, ref bool forwarded)
        {
            if (body is BaseObjectCreationExpressionSyntax)
            {
                built.Add(body);
                return null;
            }

            ExpressionSyntax read = body is AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.CoalesceAssignmentExpression } lazy ? lazy.Left : body;
            if (read != body)
            {
                built.Add(((AssignmentExpressionSyntax)body).Right);
            }

            switch (Model(read).GetSymbolInfo(read).Symbol)
            {
                case IFieldSymbol { DeclaredAccessibility: Microsoft.CodeAnalysis.Accessibility.Private } field:
                    built.AddRange(field.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax())
                        .OfType<VariableDeclaratorSyntax>().Select(declarator => declarator.Initializer?.Value).OfType<ExpressionSyntax>());
                    built.AddRange(Assigned(field));
                    return null;
                case IPropertySymbol other when read == body:
                    forwarded = true;
                    return Unsteady(other, seen);
                default:
                    return $"returns '{body}', which the reading cannot follow";
            }
        }

        /// <summary>Every value assigned to <paramref name="symbol"/> in its type.</summary>
        private IEnumerable<ExpressionSyntax> Assigned(ISymbol symbol) =>
            symbol.ContainingType.DeclaringSyntaxReferences
                .SelectMany(reference => reference.GetSyntax().DescendantNodes().OfType<AssignmentExpressionSyntax>())
                .Where(assignment => SymbolEqualityComparer.Default.Equals(Model(assignment).GetSymbolInfo(assignment.Left).Symbol, symbol))
                .Select(assignment => assignment.Right);

        private bool IsAlwaysOn(ExpressionSyntax construction)
        {
            if (construction is not BaseObjectCreationExpressionSyntax created
                || Model(created).GetSymbolInfo(created).Symbol is not IMethodSymbol constructor)
            {
                return false;
            }

            string made = constructor.ContainingType.ToDisplayString();
            bool admits = made == "SlateWindows.RelayCommand"
                || (made == "SlateWindows.AsyncRelayCommand"
                    && Argument(created, constructor, "admitWhileExecuting") is { } admit
                    && admit.IsKind(SyntaxKind.TrueLiteralExpression));
            return admits
                && Argument(created, constructor, "canExecute") is LambdaExpressionSyntax { ExpressionBody: { } returns }
                && returns.IsKind(SyntaxKind.TrueLiteralExpression);
        }

        private static ExpressionSyntax? Argument(BaseObjectCreationExpressionSyntax created, IMethodSymbol constructor, string name)
        {
            SeparatedSyntaxList<ArgumentSyntax> arguments = created.ArgumentList?.Arguments ?? default;
            for (int index = 0; index < arguments.Count; index++)
            {
                string? parameter = arguments[index].NameColon?.Name.Identifier.ValueText
                    ?? (index < constructor.Parameters.Length ? constructor.Parameters[index].Name : null);
                if (parameter == name)
                {
                    return arguments[index].Expression;
                }
            }

            return null;
        }

        private static ExpressionSyntax Unwrapped(ExpressionSyntax expression) =>
            expression switch
            {
                ParenthesizedExpressionSyntax inner => Unwrapped(inner.Expression),
                PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression } suppressed => Unwrapped(suppressed.Operand),
                _ => expression,
            };
    }
}
