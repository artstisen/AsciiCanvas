namespace AsciiCanvas.Editor;

/// <summary>
/// A folder being browsed: its folders and files, one selected entry, and ways to move around. No drawing; the file dialog
/// shows it. On Windows, going up from the root of a drive lists the drives.
/// </summary>
public sealed class FileBrowserModel
{
    private static readonly EnumerationOptions _options = new()
    {
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
    };

    private List<FileEntry> _entries = [];

    /// <summary>Creates a browser and lists a folder.</summary>
    /// <param name="startDirectory">The folder to start in. If it does not exist, the current directory is used.</param>
    public FileBrowserModel(string startDirectory)
    {
        ArgumentNullException.ThrowIfNull(startDirectory);
        Navigate(Directory.Exists(startDirectory) ? startDirectory : Environment.CurrentDirectory);
    }

    /// <summary>The folder shown, or <see langword="null"/> while the list of drives is shown.</summary>
    public string? Folder { get; private set; }

    /// <summary>The entries: the way up first, then folders, then files, each sorted by name.</summary>
    public IReadOnlyList<FileEntry> Entries => _entries;

    /// <summary>The index of the selected entry.</summary>
    public int Selected { get; private set; }

    /// <summary>The selected entry, or <see langword="null"/> if there are no entries.</summary>
    public FileEntry? SelectedEntry => Selected >= 0 && Selected < _entries.Count ? _entries[Selected] : null;

    /// <summary>Why the last move could not be made, or <see langword="null"/>.</summary>
    public string? Error { get; private set; }

    /// <summary>Shows another folder. If it cannot be read, the current folder stays and <see cref="Error"/> says why.</summary>
    /// <param name="directory">The full path of the folder.</param>
    public void Navigate(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        try
        {
            var full = Path.GetFullPath(directory);
            var entries = List(full);
            var previous = Folder;
            Folder = full;
            _entries = entries;
            Error = null;

            // Coming up from a folder, select the folder we came from; otherwise the first real entry (skipping "..").
            var cameFrom = previous is null ? -1 : _entries.ToList().FindIndex(e => e.Kind == FileEntryKind.Directory && string.Equals(e.FullPath, previous, StringComparison.OrdinalIgnoreCase));
            var firstReal = _entries.Count > 1 && _entries[0].Kind == FileEntryKind.Parent ? 1 : 0;
            Selected = cameFrom >= 0 ? cameFrom : firstReal;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Error = ex.Message;
        }
    }

    /// <summary>Goes up one folder; from the root of a drive on Windows, to the list of drives.</summary>
    public void GoUp()
    {
        if (Folder is null)
        {
            return;
        }

        var current = Folder;
        var parent = Path.GetDirectoryName(current.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (parent is not null)
        {
            Navigate(parent);
        }
        else if (OperatingSystem.IsWindows())
        {
            ShowDrives();
        }
    }

    /// <summary>Enters the selected folder or goes up if the selection is the way up.</summary>
    /// <returns>The path of the selected file, or <see langword="null"/> if the selection was a folder or drive.</returns>
    public string? ActivateSelected()
    {
        var entry = SelectedEntry;
        switch (entry?.Kind)
        {
            case FileEntryKind.Parent:
                GoUp();
                return null;
            case FileEntryKind.Directory or FileEntryKind.Drive:
                Navigate(entry.FullPath);
                return null;
            case FileEntryKind.File:
                return entry.FullPath;
            default:
                return null;
        }
    }

    /// <summary>Selects an entry; out-of-range values are clamped.</summary>
    /// <param name="index">The index.</param>
    public void Select(int index) => Selected = _entries.Count == 0 ? 0 : Math.Clamp(index, 0, _entries.Count - 1);

    private static List<FileEntry> List(string directory)
    {
        var root = new DirectoryInfo(directory);
        var parentExists = Path.GetDirectoryName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) is not null || OperatingSystem.IsWindows();
        var entries = new List<FileEntry>();
        if (parentExists)
        {
            entries.Add(new FileEntry("..", "", FileEntryKind.Parent, 0));
        }

        var all = root.EnumerateFileSystemInfos("*", _options).ToList();
        entries.AddRange(all
            .OfType<DirectoryInfo>()
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .Select(d => new FileEntry(d.Name, d.FullName, FileEntryKind.Directory, 0)));
        entries.AddRange(all
            .OfType<FileInfo>()
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Select(f => new FileEntry(f.Name, f.FullName, FileEntryKind.File, f.Length)));
        return entries;
    }

    private void ShowDrives()
    {
        Folder = null;
        _entries =
        [
            .. DriveInfo.GetDrives()
                .Where(d => d.IsReady)
                .Select(d => new FileEntry(d.Name, d.RootDirectory.FullName, FileEntryKind.Drive, 0)),
        ];
        Selected = 0;
        Error = null;
    }
}
