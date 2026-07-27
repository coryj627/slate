// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;
using System.Windows.Automation.Peers;
using SlateWindows.Reading;
using WpfMath.Parsers;
using WpfMath.Rendering;
using XamlMath;
using XamlMath.Exceptions;

namespace SlateWindows.Tests;

/// <summary>
/// W3-2 math foundations: the MathML-over-UIA convention layer and
/// the WPFMath rendering spike.
/// </summary>
public sealed class ReadingMathTests
{
    /// <summary>
    /// THE CONDITION THE OWNER CALL ATTACHED to accepting the WPF
    /// private-reflection dependency (gap G23): if dotnet/wpf ever
    /// changes the AutomationPeer property-table shape, this fails
    /// LOUDLY here and in CI instead of the convention layer rotting
    /// silently. InactiveReason names the exact break.
    /// </summary>
    [Fact]
    public void MathMlUiaPropertyBridgeInstalls()
    {
        RunSta(() =>
        {
            MathMlUiaProperty.Initialize();
            Assert.True(
                MathMlUiaProperty.IsActive,
                $"the MathML UIA convention layer is OFF: {MathMlUiaProperty.InactiveReason}");
            Assert.True(MathMlUiaProperty.PropertyIdForTests > 0);
        });
    }

    /// <summary>
    /// The bridge answers through any peer implementing
    /// <see cref="IMathMlUiaSource"/> — verified end to end through
    /// AutomationPeer's own internal property dispatch, the exact
    /// path a UIA client request takes inside WPF.
    /// </summary>
    [Fact]
    public void RegisteredPropertyResolvesThroughThePeerTable()
    {
        RunSta(() =>
        {
            MathMlUiaProperty.Initialize();
            Assert.True(MathMlUiaProperty.IsActive, MathMlUiaProperty.InactiveReason);

            var peer = new FakeMathPeer("<math><mi>x</mi></math>");
            System.Reflection.MethodInfo dispatch = typeof(AutomationPeer).GetMethod(
                "GetPropertyValue",
                System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Instance,
                types: new[] { typeof(int) })!;
            object? value = dispatch.Invoke(
                peer, new object[] { MathMlUiaProperty.PropertyIdForTests });
            Assert.Equal("<math><mi>x</mi></math>", value);

            var empty = new FakeMathPeer(string.Empty);
            Assert.Null(dispatch.Invoke(
                empty, new object[] { MathMlUiaProperty.PropertyIdForTests }));
        });
    }

    /// <summary>
    /// WPFMath currency spike (w3_spec §W3-2 first task): v2.1.0
    /// parses and builds representative formulas under .NET 10, and
    /// signals unsupported input with TexException — the documented
    /// boundary the renderer degrades on.
    /// </summary>
    [Fact]
    public void WpfMathParsesRepresentativeFormulasAndSignalsGaps()
    {
        RunSta(() =>
        {
            TexFormulaParser parser = WpfTeXFormulaParser.Instance;

            // Representative supported set (docs/matrices.md).
            foreach (string supported in new[]
            {
                @"x^2 + \frac{a}{b}",
                @"\sum_{i=0}^{n} i^2",
                @"\sqrt{b^2 - 4ac}",
                @"\begin{pmatrix} a & b \\ c & d \end{pmatrix}",
                @"\text{speed} = \frac{d}{t}",
            })
            {
                TexFormula formula = parser.Parse(supported);
                TexEnvironment environment =
                    WpfTeXEnvironment.Create(TexStyle.Display, 20.0, "Arial");
                System.Windows.Media.Imaging.BitmapSource bitmap =
                    formula.RenderToBitmap(environment);
                Assert.True(
                    bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0,
                    $"'{supported}' rendered to an empty bitmap");
            }

            // The documented coverage boundary: unsupported environments
            // throw TexException — the signal the reading renderer
            // degrades on (monospace source, matrix-rowed).
            Assert.Throws<TexParseException>(() =>
                parser.Parse(@"\begin{bmatrix} 1 \\ 2 \end{bmatrix}"));
        });
    }

    private sealed class FakeMathPeer
        : FrameworkElementAutomationPeer, IMathMlUiaSource
    {
        public FakeMathPeer(string mathMl)
            : base(new System.Windows.Controls.Border())
        {
            MathMl = mathMl;
        }

        public string MathMl { get; }
    }

    /// <summary>WPF objects require STA; xunit runs MTA.</summary>
    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "STA test body timed out.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
