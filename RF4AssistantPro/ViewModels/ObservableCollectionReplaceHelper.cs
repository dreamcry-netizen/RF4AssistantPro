using System.Collections.ObjectModel;

namespace RF4AssistantPro.ViewModels;

public static class ObservableCollectionReplaceHelper
{
    public static void Replace<T>(
        ObservableCollection<T> target,
        IEnumerable<T> source)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);

        // Materialize before Clear(): source may be a lazy projection of target.
        var items = source.ToList();
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }
}