// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;

namespace ContainerSpike;

/// Synthetic large notes for the W3-1 performance spike.
///
/// Deterministic by construction (a fixed-seed SplitMix64, the repo's
/// census PRNG) so a regression is reproducible and two runs are
/// comparable. Shapes are drawn from what actually lands in a vault:
/// prose with inline links, tags and citations; lists; quotes; code
/// fences; tables; block embeds.
internal static class Corpus
{
    internal readonly struct Spec
    {
        public Spec(string name, int words, int links, int longRunBytes = 0)
        {
            Name = name;
            Words = words;
            Links = links;
            LongRunBytes = longRunBytes;
        }

        public string Name { get; }
        public int Words { get; }
        public int Links { get; }

        /// Bytes of a single enormous inline destination per link — the
        /// converter-generated shape that makes a file large WITHOUT
        /// making it wordy, and the exact case the payload-duplication
        /// residual (Θ(runs × destination)) was shipped against.
        public int LongRunBytes { get; }
    }

    private static readonly string[] Words =
    {
        "vault", "note", "graph", "canvas", "reading", "editor", "parity",
        "windows", "accessible", "document", "structure", "inline", "segment",
        "render", "token", "anchor", "citation", "heading", "paragraph",
        "content", "surface", "container", "keyboard", "narration", "review",
    };

    public static string Build(Spec spec)
    {
        var rng = new SplitMix64(0x5104E);
        var sb = new StringBuilder(Math.Max(1024, spec.Words * 7));
        int wordsWritten = 0;
        int linksWritten = 0;
        int section = 0;

        while (wordsWritten < spec.Words)
        {
            sb.Append("## Section ").Append(++section).Append("\n\n");

            // Six block families per section, so no size is dominated by
            // one shape and the block walk sees a realistic mix.
            for (int block = 0; block < 6 && wordsWritten < spec.Words; block++)
            {
                switch (block)
                {
                    case 0:
                    case 1:
                        sb.Append(Paragraph(ref rng, spec, ref wordsWritten, ref linksWritten));
                        break;
                    case 2:
                        for (int item = 0; item < 4 && wordsWritten < spec.Words; item++)
                        {
                            sb.Append("- ")
                              .Append(Sentence(ref rng, spec, 12, ref wordsWritten, ref linksWritten))
                              .Append('\n');
                        }
                        sb.Append('\n');
                        break;
                    case 3:
                        sb.Append("> ")
                          .Append(Sentence(ref rng, spec, 18, ref wordsWritten, ref linksWritten))
                          .Append("\n\n");
                        break;
                    case 4:
                        sb.Append("```rust\nfn section_").Append(section)
                          .Append("() -> usize { ").Append(section).Append(" }\n```\n\n");
                        break;
                    default:
                        sb.Append("| name | value |\n| --- | --- |\n| row ")
                          .Append(section).Append(" | ").Append(section * 7).Append(" |\n\n");
                        break;
                }
            }

            // One block-level embed per section: the card path, which
            // takes a different branch through the builder.
            sb.Append("![[note-").Append(section).Append("]]\n\n");
        }

        // Top the links up if the prose ran out first, so the link count
        // is honoured rather than approximated.
        while (linksWritten < spec.Links)
        {
            sb.Append(Link(ref rng, spec, ref linksWritten)).Append(' ');
            if (linksWritten % 12 == 0)
            {
                sb.Append("\n\n");
            }
        }

        return sb.ToString();
    }

    private static string Paragraph(
        ref SplitMix64 rng, Spec spec, ref int words, ref int links)
    {
        var sb = new StringBuilder();
        int sentences = 3 + (int)(rng.Next() % 3);
        for (int i = 0; i < sentences; i++)
        {
            sb.Append(Sentence(ref rng, spec, 22, ref words, ref links)).Append(' ');
        }
        sb.Append("\n\n");
        return sb.ToString();
    }

    private static string Sentence(
        ref SplitMix64 rng, Spec spec, int length, ref int words, ref int links)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < length; i++)
        {
            // Link density is derived from the target counts so the two
            // knobs stay independent: more words never silently means
            // more links.
            bool wantLink = links < spec.Links
                && spec.Words > 0
                && rng.Next() % (ulong)Math.Max(2, spec.Words / Math.Max(1, spec.Links)) == 0;

            if (wantLink)
            {
                sb.Append(Link(ref rng, spec, ref links));
            }
            else
            {
                sb.Append(Words[(int)(rng.Next() % (ulong)Words.Length)]);
            }
            sb.Append(' ');
            words++;
        }
        sb.Append('.');
        return sb.ToString();
    }

    /// Rotates the five activatable shapes so no single kind dominates —
    /// resolution, anchors, aliases, tags, citations and external URLs
    /// all take different branches through the run walker.
    private static string Link(ref SplitMix64 rng, Spec spec, ref int links)
    {
        int ordinal = links++;
        if (spec.LongRunBytes > 0)
        {
            // The anti-correlation the residual decision rests on says a
            // long destination carries a SHORT plain label. Reproduced
            // faithfully — inflating the label too would be testing a
            // shape the evidence says does not occur.
            return $"[ref]({new string('q', spec.LongRunBytes)})";
        }

        return (ordinal % 6) switch
        {
            0 => $"[[note-{ordinal % 97}]]",
            1 => $"[[note-{ordinal % 97}|alias {ordinal}]]",
            2 => $"[[note-{ordinal % 97}#Section {ordinal % 11}]]",
            3 => $"#tag-{ordinal % 41}",
            4 => $"[@cite{ordinal % 29}]",
            _ => $"[external {ordinal}](https://example.com/{ordinal}/path)",
        };
    }

    /// splitmix64 — the repo's census PRNG, so a failing size replays
    /// byte for byte.
    internal struct SplitMix64
    {
        private ulong _state;

        public SplitMix64(ulong seed) => _state = seed;

        public ulong Next()
        {
            _state += 0x9E3779B97F4A7C15UL;
            ulong z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
