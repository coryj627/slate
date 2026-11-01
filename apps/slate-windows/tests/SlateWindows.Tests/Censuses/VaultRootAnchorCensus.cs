// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace SlateWindows.Tests.Censuses;

/// <summary>
/// Plan 44 WB-28 PR 2: every vault store the shell builds reaches the vault
/// through a root anchor. A store built without one opens its root by path,
/// which this test assembly allows for every fact (<see cref="TestRootAnchors"/>)
/// and the app refuses at run time — so a production site that forgot the
/// anchor would pass every behavioural fact here and fail only in the app.
/// This census binds each construction and requires its <c>root</c> argument.
/// </summary>
public sealed class VaultRootAnchorCensus
{
    private static readonly string[] StoreTypes =
    [
        "SlateWindows.WorkspacePersistence",
        "SlateWindows.SidebarSettingsStore",
        "SlateWindows.FileRecentsStore",
        "SlateWindows.Search.SearchRecentsStore",
        "SlateWindows.Graph.GraphConfigStore",
        "SlateWindows.Graph.GraphPreferencesViewModel",
    ];

    [Fact]
    public void EveryShellVaultStoreIsBuiltWithARootAnchor()
    {
        var found = new List<string>();
        foreach ((string relative, CSharpSource source) in ShellCompilation.Sources)
        {
            SemanticModel model = ShellCompilation.ModelFor(source);
            foreach (BaseObjectCreationExpressionSyntax creation in source.Root.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
            {
                if (model.GetOperation(creation) is not IObjectCreationOperation operation
                    || operation.Constructor is not { } constructor
                    || !StoreTypes.Contains(constructor.ContainingType.ToDisplayString()))
                {
                    continue;
                }

                IArgumentOperation? root = operation.Arguments.FirstOrDefault(argument => argument.Parameter?.Name == "root");
                bool anchored = root is { ArgumentKind: not ArgumentKind.DefaultValue }
                    && root.Value.ConstantValue is not { HasValue: true, Value: null };
                found.Add($"{relative}:{constructor.ContainingType.Name}:{(anchored ? "anchored" : "BY PATH")}");
            }
        }

        Assert.Equal(
            [
                "FilesSidebarViewModel.cs:FileRecentsStore:anchored",
                "FilesSidebarViewModel.cs:SidebarSettingsStore:anchored",
                "Graph/GraphConfigWriter.cs:GraphConfigStore:anchored",
                "Graph/GraphPreferencesViewModel.cs:GraphConfigStore:anchored",
                "Graph/WorkspaceViewModel.Graph.cs:GraphPreferencesViewModel:anchored",
                "QuickSwitcherViewModel.cs:FileRecentsStore:anchored",
                "Search/VaultSearchSource.cs:SearchRecentsStore:anchored",
                "VaultLifecycleViewModel.cs:WorkspacePersistence:anchored",
                "WorkspaceViewModel.cs:WorkspacePersistence:anchored",
            ],
            found.Order(StringComparer.Ordinal).ToList());
    }
}
