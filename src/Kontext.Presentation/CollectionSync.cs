using System.Collections.ObjectModel;

namespace Kontext.Presentation;

/// <summary>
/// Updates an <see cref="ObservableCollection{T}"/> to match a desired list using only
/// insert, move and remove notifications (never a reset). New items are inserted before
/// stale ones are removed, so a non-empty collection never passes through empty.
/// Bound controls such as NavigationView then keep their containers (and expand state).
/// </summary>
internal static class CollectionSync
{
    public static void SyncInPlace<T>(ObservableCollection<T> target, IReadOnlyList<T> desired)
        where T : class
    {
        for (var index = 0; index < desired.Count; index++)
        {
            var wanted = desired[index];
            if (index < target.Count && ReferenceEquals(target[index], wanted))
            {
                continue;
            }

            var existing = IndexOf(target, wanted, from: index + 1);
            if (existing >= 0)
            {
                target.Move(existing, index);
            }
            else
            {
                target.Insert(index, wanted);
            }
        }

        while (target.Count > desired.Count)
        {
            target.RemoveAt(target.Count - 1);
        }
    }

    private static int IndexOf<T>(ObservableCollection<T> target, T item, int from)
        where T : class
    {
        for (var index = from; index < target.Count; index++)
        {
            if (ReferenceEquals(target[index], item))
            {
                return index;
            }
        }

        return -1;
    }
}
