namespace ArtifactBrowser.Client.Services;

/// <summary>Tracks the set of currently selected item virtual paths within the current folder view.</summary>
public sealed class SelectionService
{
    private readonly HashSet<string> _selected = new();

    public event Action? Changed;

    public IReadOnlySet<string> Selected => _selected;

    public bool IsSelected(string path) => _selected.Contains(path);

    public void Toggle(string path)
    {
        if (!_selected.Remove(path))
        {
            _selected.Add(path);
        }

        Changed?.Invoke();
    }

    public void Select(string path)
    {
        _selected.Clear();
        _selected.Add(path);
        Changed?.Invoke();
    }

    public void SelectRange(IEnumerable<string> paths)
    {
        _selected.Clear();
        foreach (var p in paths)
        {
            _selected.Add(p);
        }

        Changed?.Invoke();
    }

    public void Clear()
    {
        if (_selected.Count == 0)
        {
            return;
        }

        _selected.Clear();
        Changed?.Invoke();
    }
}
