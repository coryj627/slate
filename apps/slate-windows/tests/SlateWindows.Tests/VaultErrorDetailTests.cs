// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using System.Reflection;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 (#1249, R-7): a caught error's detail is core's rendering of it
/// (<c>a11y::vault_error_detail</c>, exported as
/// <see cref="SlateUniffiMethods.VaultErrorDetail"/>), never host copy and
/// never the binding's field dump ("@message=…"). Core pins every
/// variant's words (<c>vault_error_detail_speaks_every_variant</c>) and the
/// binding crate pins the crossing; these facts pin what the host
/// receives. The variants are enumerated from the generated type rather
/// than listed here, so a variant added to core fails until core phrases
/// it. That the host USES this text — no host literal, no Message read —
/// is <c>VaultErrorDetailCensus</c>.
/// </summary>
public sealed class VaultErrorDetailTests
{
    public static TheoryData<string> Variants()
    {
        var data = new TheoryData<string>();
        foreach (Type type in VariantTypes())
        {
            data.Add(type.Name);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void EveryVariantReadsAsCoreTextWithoutBindingFieldLabels(string variant)
    {
        VaultException error = Construct(VariantTypes().Single(type => type.Name == variant));

        string text = SlateUniffiMethods.VaultErrorDetail(error);

        Assert.False(string.IsNullOrWhiteSpace(text), variant);
        Assert.DoesNotContain("@", text, StringComparison.Ordinal);
        Assert.NotEqual(error.Message, text);
    }

    /// <summary>The raw details stay passthrough values: an Io or Db error
    /// carries an OS or SQLite message that core does not reword.</summary>
    [Fact]
    public void RawIoAndDbDetailsPassThroughUnchanged()
    {
        Assert.Equal(
            "Access is denied. (os error 5)",
            SlateUniffiMethods.VaultErrorDetail(new VaultException.Io("Access is denied. (os error 5)")));
        Assert.Equal(
            "sqlite error: database disk image is malformed",
            SlateUniffiMethods.VaultErrorDetail(
                new VaultException.Db("sqlite error: database disk image is malformed")));
    }

    /// <summary>A structured variant arrives as core words it, and a
    /// conflict's detail carries neither content hash nor modification
    /// time.</summary>
    [Fact]
    public void StructuredVariantsArriveAsCoreWordsThem()
    {
        Assert.Equal(
            "Something named notes.md already exists there.",
            SlateUniffiMethods.VaultErrorDetail(new VaultException.DestinationExists("notes.md")));
        Assert.Equal(
            "File changed externally.",
            SlateUniffiMethods.VaultErrorDetail(new VaultException.WriteConflict(
                new string('a', 64),
                new string('b', 64),
                1_758_000_000_000)));
    }

    /// <summary>The enumeration is the census, so it must see the
    /// binding: a reflection filter that silently matched nothing would
    /// pass every theory above by running none of them.</summary>
    [Fact]
    public void TheCensusEnumeratesTheGeneratedVariants()
    {
        Type[] variants = VariantTypes().ToArray();
        Assert.Contains(typeof(VaultException.WriteConflict), variants);
        Assert.Contains(typeof(VaultException.Io), variants);
        Assert.Contains(typeof(VaultException.Cancelled), variants);
        Assert.True(variants.Length >= 20, $"only {variants.Length} variants enumerated");
    }

    private static IEnumerable<Type> VariantTypes() =>
        typeof(VaultException).GetNestedTypes(BindingFlags.Public)
            .Where(type => type.IsSubclassOf(typeof(VaultException)) && !type.IsAbstract)
            .OrderBy(type => type.Name, StringComparer.Ordinal);

    private static VaultException Construct(Type type)
    {
        ConstructorInfo constructor = Assert.Single(type.GetConstructors());
        object[] arguments = constructor.GetParameters()
            .Select(parameter => parameter.ParameterType == typeof(string)
                ? (object)"notes.md"
                : Convert.ChangeType(7, parameter.ParameterType, CultureInfo.InvariantCulture))
            .ToArray();
        return (VaultException)constructor.Invoke(arguments);
    }
}
