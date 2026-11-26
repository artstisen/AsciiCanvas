namespace AsciiCanvas.Editor.Tests;

public sealed class FileBrowserModelTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "asciicanvas-" + Guid.NewGuid().ToString("N"));

    public FileBrowserModelTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "beta"));
        Directory.CreateDirectory(Path.Combine(_root, "Alpha"));
        File.WriteAllText(Path.Combine(_root, "zeta.txt"), "z");
        File.WriteAllText(Path.Combine(_root, "apple.txt"), "apple");
        File.WriteAllText(Path.Combine(_root, "Beta.md"), "b");
        File.WriteAllText(Path.Combine(_root, "beta", "inner.txt"), "inner");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Constructor_FolderWithFilesAndFolders_ListsWayUpThenFoldersThenFilesSorted()
    {
        var model = new FileBrowserModel(_root);

        var names = model.Entries.Select(e => e.Name).ToArray();

        Assert.Equal(["..", "Alpha", "beta", "apple.txt", "Beta.md", "zeta.txt"], names);
        Assert.Equal(
            [FileEntryKind.Parent, FileEntryKind.Directory, FileEntryKind.Directory, FileEntryKind.File, FileEntryKind.File, FileEntryKind.File],
            model.Entries.Select(e => e.Kind).ToArray());
        Assert.Equal(5, model.Entries[3].Size);
    }

    [Fact]
    public void Constructor_FolderWithEntries_SelectsFirstRealEntryNotTheWayUp()
    {
        var model = new FileBrowserModel(_root);

        Assert.Equal("Alpha", model.SelectedEntry?.Name);
    }

    [Fact]
    public void ActivateSelected_OnFolder_EntersItAndReturnsNull()
    {
        var model = new FileBrowserModel(_root);
        model.Select(2);

        var file = model.ActivateSelected();

        Assert.Null(file);
        Assert.Equal(Path.Combine(_root, "beta"), model.Folder);
        Assert.Contains(model.Entries, e => e.Name == "inner.txt");
    }

    [Fact]
    public void ActivateSelected_OnFile_ReturnsItsFullPath()
    {
        var model = new FileBrowserModel(_root);
        model.Select(3);

        var file = model.ActivateSelected();

        Assert.Equal(Path.Combine(_root, "apple.txt"), file);
        Assert.Equal(_root, model.Folder);
    }

    [Fact]
    public void GoUp_FromSubfolder_ShowsParentAndSelectsTheFolderWeCameFrom()
    {
        var model = new FileBrowserModel(Path.Combine(_root, "beta"));

        model.GoUp();

        Assert.Equal(_root, model.Folder);
        Assert.Equal("beta", model.SelectedEntry?.Name);
    }

    [Fact]
    public void Navigate_MissingFolder_KeepsCurrentFolderAndReportsError()
    {
        var model = new FileBrowserModel(_root);

        model.Navigate(Path.Combine(_root, "does-not-exist"));

        Assert.Equal(_root, model.Folder);
        Assert.NotNull(model.Error);
    }

    [Fact]
    public void Select_IndexOutOfRange_IsClamped()
    {
        var model = new FileBrowserModel(_root);

        model.Select(999);
        var last = model.Selected;
        model.Select(-5);

        Assert.Equal(model.Entries.Count - 1, last);
        Assert.Equal(0, model.Selected);
    }
}
