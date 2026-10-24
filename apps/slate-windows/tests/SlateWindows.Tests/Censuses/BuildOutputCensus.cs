// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// XB-4b (#1574): the build output carries the native libraries this
// machine's Windows loads and nothing else, and SkiaSharp and HarfBuzzSharp
// resolve at the versions the app names, not at whatever Svg.Skia's lower
// bounds bring in. The trim is apps/slate-windows/Directory.Build.targets; it
// runs in every project because each one resolves its own package assets, so
// every output that reaches a test or the flaui-binaries artifact is checked.

using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "build-output")]
public sealed class BuildOutputCensus
{
    /// <summary>The RID the apphost and the test host run as, which is the
    /// one the trim keeps.</summary>
    private static readonly string ThisMachine =
        "win-" + RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();

    private static readonly string[] KeptRuntimeIdentifiers = ["any", "win", ThisMachine];

    private static readonly string[] SkiaLibraries = ["libSkiaSharp.dll", "libHarfBuzzSharp.dll"];

    /// <summary>Each output, with the files under <c>runtimes/</c> it cannot
    /// run without. SlateWindows.Tests references every one of them, so a
    /// test build has built them all.</summary>
    private static readonly Dictionary<string, (string Project, string[] Required)> Outputs = new()
    {
        ["SlateWindows.Tests"] = (@"tests\SlateWindows.Tests", SkiaLibraries),
        ["SlateWindows"] = (@"src\SlateWindows", SkiaLibraries),
        ["GridConformanceHost"] = (@"tools\GridConformanceHost", SkiaLibraries),
        ["SvgDecodeProbe"] = (@"tools\SvgDecodeProbe", SkiaLibraries),
        // System.Management's lib copy only throws PlatformNotSupportedException;
        // the shell gate's FlaUI and Axe.Windows need the runtimes/win one.
        ["SlateWindows.AccessibilityTests"] = (@"tests\SlateWindows.AccessibilityTests", ["System.Management.dll"]),
    };

    public static TheoryData<string> OutputNames() => new(Outputs.Keys);

    [Theory]
    [MemberData(nameof(OutputNames))]
    public void TheNativeRuntimesCarryOnlyThisMachinesLibrariesAndNoSymbols(string output)
    {
        string directory = OutputDirectory(output);
        string runtimes = Path.Combine(directory, "runtimes");
        string[] files = Directory.GetFiles(runtimes, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(runtimes, path))
            .ToArray();

        // Files, not folders: an incremental build deletes the files it no
        // longer copies but leaves their empty folders behind.
        string[] foreign = files
            .Select(RuntimeIdentifierOf)
            .Distinct()
            .Where(rid => !KeptRuntimeIdentifiers.Contains(rid))
            .ToArray();
        Assert.True(foreign.Length == 0, $"{output}: runtimes/ holds {string.Join(", ", foreign)}");

        string[] symbols = files.Where(path => path.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)).ToArray();
        Assert.True(symbols.Length == 0, $"{output}: runtimes/ holds {string.Join(", ", symbols)}");

        // The trim keeps what the output loads: without the Skia libraries
        // the SVG preview and the diagrams cannot decode.
        foreach (string library in Outputs[output].Required)
        {
            Assert.True(
                files.Any(path => Path.GetFileName(path) == library
                    && RuntimeIdentifierOf(path) is string rid
                    && (rid == ThisMachine || rid == "win")),
                $"{output}: no runtimes/{ThisMachine} or runtimes/win copy of {library}");
        }

        // The deps.json names no file the output lacks.
        string[] stale = RuntimeTargets(output)
            .Where(path => !File.Exists(Path.Combine(directory, path)))
            .ToArray();
        Assert.True(stale.Length == 0, $"{output}.deps.json names {string.Join(", ", stale)}");
    }

    /// <summary>MSBuild imports only the nearest Directory.Build.targets, and
    /// a project can turn the import off. Either would leave that project's
    /// output untrimmed while every output above still passes.</summary>
    [Fact]
    public void EveryProjectHereImportsTheTrim()
    {
        string windows = Path.Combine(SourceText.RepoRoot(), "apps", "slate-windows");
        string[] buildFiles = Directory
            .EnumerateFiles(windows, "*.*", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(windows, path))
            .Where(path => path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".props", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".targets", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        string[] nearer = buildFiles
            .Where(path => Path.GetFileName(path).Equals("Directory.Build.targets", StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetRelativePath(windows, path))
            .Where(path => path != "Directory.Build.targets")
            .ToArray();
        Assert.True(nearer.Length == 0, $"a nearer Directory.Build.targets hides the trim: {string.Join(", ", nearer)}");

        string[] optedOut = buildFiles
            .Where(path => File.ReadAllText(path).Contains("ImportDirectoryBuildTargets", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(windows, path))
            .ToArray();
        Assert.True(optedOut.Length == 0, $"these files name ImportDirectoryBuildTargets: {string.Join(", ", optedOut)}");
    }

    [Fact]
    public void ThePackageGraphPinsSkiaSharpAndHarfBuzzSharpDirectly()
    {
        string csproj = Path.Combine(
            SourceText.ShellSourceRoot(), "SlateWindows.csproj");
        XElement[] references = XDocument.Load(csproj).Descendants("PackageReference").ToArray();

        foreach (string package in new[] { "SkiaSharp", "HarfBuzzSharp" })
        {
            XElement[] pins = references
                .Where(reference => (string?)reference.Attribute("Include") == package)
                .ToArray();
            Assert.True(pins.Length == 1, $"SlateWindows.csproj names {pins.Length} {package} PackageReferences, not one");

            string version = (string?)pins[0].Attribute("Version") ?? "";
            Assert.Matches(new Regex(@"^\d+\.\d+\.\d+(\.\d+)?$"), version);

            // Nearest-wins takes the direct reference over Svg.Skia's lower
            // bound; the resolved graph is the proof.
            foreach (string output in new[] { "SlateWindows", "SlateWindows.Tests", "GridConformanceHost" })
            {
                Assert.Contains($"{package}/{version}", Libraries(output));
            }
        }
    }

    private static string RuntimeIdentifierOf(string pathUnderRuntimes) =>
        pathUnderRuntimes.Split(Path.DirectorySeparatorChar)[0];

    private static bool IsBuildOutput(string root, string path) =>
        Path.GetRelativePath(root, path)
            .Split(Path.DirectorySeparatorChar)
            .Any(segment => segment is "bin" or "obj");

    private static string OutputDirectory(string output)
    {
        string directory = Path.Combine(
            SourceText.RepoRoot(), "apps", "slate-windows", Outputs[output].Project,
            "bin", BuildConfiguration(), "net10.0-windows");
        Assert.True(
            File.Exists(Path.Combine(directory, $"{output}.deps.json")),
            $"{output} is not built at {directory} (build the solution first)");
        return directory;
    }

    private static JsonElement DepsJson(string output)
    {
        string path = Path.Combine(OutputDirectory(output), $"{output}.deps.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    private static IEnumerable<string> Libraries(string output) =>
        DepsJson(output).GetProperty("libraries").EnumerateObject().Select(library => library.Name);

    private static IEnumerable<string> RuntimeTargets(string output)
    {
        foreach (JsonProperty target in DepsJson(output).GetProperty("targets").EnumerateObject())
        {
            foreach (JsonProperty library in target.Value.EnumerateObject())
            {
                if (library.Value.TryGetProperty("runtimeTargets", out JsonElement assets))
                {
                    foreach (JsonProperty asset in assets.EnumerateObject())
                    {
                        yield return asset.Name;
                    }
                }
            }
        }
    }

    private static string BuildConfiguration()
    {
#if DEBUG
        return "Debug";
#else
        return "Release";
#endif
    }
}
