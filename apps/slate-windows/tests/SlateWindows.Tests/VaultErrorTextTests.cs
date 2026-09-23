// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using System.Reflection;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 (#1249, R-7): every error variant the binding can throw reads as
/// text, and none reads as the binding's field dump ("@message=…"). The
/// variants are enumerated from the generated type rather than listed
/// here, so a variant added to core fails until it is phrased.
/// </summary>
public sealed class VaultErrorTextTests
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
    public void EveryVariantReadsAsTextWithoutBindingFieldLabels(string variant)
    {
        VaultException error = Construct(VariantTypes().Single(type => type.Name == variant));

        string text = VaultErrorText.HumanReadable(error);

        Assert.False(string.IsNullOrWhiteSpace(text), variant);
        Assert.NotEqual(VaultErrorText.Unphrased, text);
        Assert.DoesNotContain("@", text, StringComparison.Ordinal);
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
