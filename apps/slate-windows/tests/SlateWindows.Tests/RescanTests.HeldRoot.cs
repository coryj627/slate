// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Tests;

/// <summary>Plan 44 WB-28: Refresh lists the folder the session holds, not
/// the one its spelling names now.</summary>
public sealed partial class RescanTests
{
    [Fact]
    public void ARefreshAfterTheVaultsSpellingIsRetargetedListsTheHeldVault() => RunSta(() =>
    {
        string held = Harness.NewRoot("held");
        string decoy = Harness.NewRoot("decoy");
        Harness.Seed(held, ("held.md", "# Held\n"));
        Harness.Seed(decoy, ("decoy.md", "# Decoy\n"));
        string spelling = held + "-spelling";
        Junctions.Create(spelling, held);
        try
        {
            using var h = Harness.OpenExisting(spelling);
            Junctions.Retarget(spelling, decoy);
            File.WriteAllText(Path.Combine(held, "late.md"), "# Late\n");

            h.Sidebar.RefreshCommand.Execute(null);
            h.Context.Await(h.Lifecycle.RescanCompletion);

            Assert.Contains(h.Sidebar.RootNodes, node => node.Path == "held.md");
            Assert.Contains(h.Sidebar.RootNodes, node => node.Path == "late.md");
            Assert.DoesNotContain(h.Sidebar.RootNodes, node => node.Path == "decoy.md");
        }
        finally
        {
            Junctions.Remove(spelling);
            foreach (string directory in new[] { held, decoy })
            {
                try
                {
                    Directory.Delete(directory, recursive: true);
                }
                catch (IOException)
                {
                }
            }
        }
    });
}
