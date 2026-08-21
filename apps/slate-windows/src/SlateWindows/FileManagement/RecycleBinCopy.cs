// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.FileManagement;

/// <summary>
/// W5-4 (F6): mac's <c>BatchTrashCopy</c> adapted with the established
/// Recycle Bin substitution (FD-3) — curly quotes and sentence shapes
/// byte-parallel to mac's, "the system Trash" → "the Recycle Bin".
/// </summary>
internal static class RecycleBinCopy
{
    internal const string DestructiveWarning = "Slate can't undo this action.";

    /// <summary>mac's actionLabel "Move to Trash", adapted. Pinned at
    /// the confirmation seam — never staged by callers.</summary>
    internal const string ActionLabel = "Move to the Recycle Bin";

    internal static string CountText(int count, string noun = "item") =>
        $"{count:N0} {(count == 1 ? noun : noun + "s")}";

    internal static string SingleFolderTitle(string name) =>
        $"Move “{name}” to the Recycle Bin?";

    internal static string SingleFolderMessage(string name, int itemCount) =>
        $"Move “{name}” and its {CountText(itemCount)} to the "
        + $"Recycle Bin. {DestructiveWarning}";

    internal static string BatchTitle(int itemCount) =>
        $"Move {CountText(itemCount)} to the Recycle Bin?";

    internal static string BatchMessage(int itemCount, int nonEmptyFolderCount)
    {
        string folderClause = nonEmptyFolderCount > 0
            ? $", including {CountText(nonEmptyFolderCount, "folder")} with contents,"
            : string.Empty;
        return $"Move {CountText(itemCount)}{folderClause} to the "
            + $"Recycle Bin. {DestructiveWarning}";
    }
}
