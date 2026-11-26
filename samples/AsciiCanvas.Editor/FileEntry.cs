namespace AsciiCanvas.Editor;

/// <summary>What an entry in the file browser is.</summary>
public enum FileEntryKind
{
    /// <summary>The way up: the parent folder, or the list of drives from the root of a drive on Windows.</summary>
    Parent,

    /// <summary>A folder.</summary>
    Directory,

    /// <summary>A file.</summary>
    File,

    /// <summary>A drive, in the list of drives.</summary>
    Drive,
}

/// <summary>One line in the file browser.</summary>
/// <param name="Name">The name shown.</param>
/// <param name="FullPath">The full path; empty for <see cref="FileEntryKind.Parent"/>.</param>
/// <param name="Kind">What the entry is.</param>
/// <param name="Size">The size in bytes for files, otherwise 0.</param>
public sealed record FileEntry(string Name, string FullPath, FileEntryKind Kind, long Size);
