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
    internal const int DatedCeiling = 0;

    /// <summary>Every key dated on <see cref="DatedOn"/>. Nothing is ever added.</summary>
    internal static readonly string[] DatedBaseline = [];

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
    internal static string[] DatedProblems(IReadOnlyDictionary<string, string> manifest, int ceiling) => [];

    [Fact]
    public void ThePlantedCommandSitesAreCaught()
    {
        const string Planted = """
            using System.Windows;
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

    // CB-17a's tests-first stubs: main's behaviour until the census lands. Triggers are
    // keyed per carrier, no command site is scraped, a constructor's binding is no site,
    // and the judge rules on nothing new.
    private static TriggerCondition? ConditionOf(XElement setter) => null;

    private static void ScrapeStyleSetters(SortedDictionary<string, Site> sites, string file, XDocument document)
    {
    }

    private static void ScrapeXamlCommands(SortedDictionary<string, Site> sites, string file, XDocument document, IReadOnlyList<XDocument> all)
    {
    }

    private static bool ScrapeACommandWrite(
        SortedDictionary<string, Site> sites, SemanticModel model, AssignmentExpressionSyntax assignment, IReadOnlySet<string> focusless) => false;

    private static bool ScrapeACommandCall(
        SortedDictionary<string, Site> sites, SemanticModel model, InvocationExpressionSyntax call, MemberAccessExpressionSyntax member, IReadOnlySet<string> focusless) => false;

    private static bool BindsAState(string call, string property) => false;

    internal sealed class KeysFirstRule
    {
        public KeysFirstRule(
            IReadOnlyDictionary<string, string> facts,
            IReadOnlyList<XDocument> documents,
            Compilation? commands,
            IReadOnlyCollection<string> dated)
        {
        }

        public bool Judges(Site site, string witness, out string? verdict)
        {
            verdict = null;
            return false;
        }
    }
}
