// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Canonical JSON writer for §W-A parity artifacts (w0_spec §W0-3 item 5).
// Byte-identical output is the whole point, so serialization is a fixed
// bespoke algorithm rather than a platform JSON library (library escaping
// and float formatting differ across platforms/versions). The Swift twin
// (apps/slate-mac/Tests/SlateMacTests/ParityHarnessTests.swift) implements
// the identical rules; the committed goldens arbitrate.
//
// Rules (change both implementations together, never one):
// - Objects: keys in the order the serializer emits them (fixed per
//   surface, no sorting at write time), `{"k":v,...}` with no whitespace.
// - Strings: UTF-8; escape `"` `\` as `\"` `\\`, control chars U+0000–001F
//   as `\n` `\r` `\t` for those three else `\u00XX` lowercase hex; all
//   other scalars raw.
// - Integers: invariant decimal. Booleans: `true`/`false`. Null: `null`.
// - Doubles: printf `%.6f` invariant (fixed six decimals, no exponent).
// - Top level: one object per line? No — one artifact = one JSON document
//   followed by exactly one trailing `\n`.

using System.Globalization;
using System.Text;

namespace ParityHarness;

public sealed class CanonicalJson
{
    private readonly StringBuilder _sb = new();

    public override string ToString() => _sb.ToString();

    public CanonicalJson Raw(string s)
    {
        _sb.Append(s);
        return this;
    }

    public CanonicalJson Str(string value)
    {
        _sb.Append('"');
        foreach (char c in value)
        {
            switch (c)
            {
                case '"':
                    _sb.Append("\\\"");
                    break;
                case '\\':
                    _sb.Append("\\\\");
                    break;
                case '\n':
                    _sb.Append("\\n");
                    break;
                case '\r':
                    _sb.Append("\\r");
                    break;
                case '\t':
                    _sb.Append("\\t");
                    break;
                default:
                    if (c < 0x20)
                    {
                        _sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        _sb.Append(c);
                    }
                    break;
            }
        }
        _sb.Append('"');
        return this;
    }

    public CanonicalJson Num(long value)
    {
        _sb.Append(value.ToString(CultureInfo.InvariantCulture));
        return this;
    }

    public CanonicalJson Num(ulong value)
    {
        _sb.Append(value.ToString(CultureInfo.InvariantCulture));
        return this;
    }

    public CanonicalJson Num(double value)
    {
        _sb.Append(value.ToString("F6", CultureInfo.InvariantCulture));
        return this;
    }

    public CanonicalJson Bool(bool value)
    {
        _sb.Append(value ? "true" : "false");
        return this;
    }

    public CanonicalJson Null()
    {
        _sb.Append("null");
        return this;
    }
}
