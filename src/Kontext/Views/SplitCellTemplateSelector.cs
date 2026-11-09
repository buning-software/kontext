using Kontext.Domain;
using Kontext.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Kontext.Views;

/// <summary>Picks the template (and so the background) for one side of a split-diff row.</summary>
public sealed class SplitCellTemplateSelector : DataTemplateSelector
{
    public DataTemplate? AddedTemplate { get; set; }

    public DataTemplate? RemovedTemplate { get; set; }

    public DataTemplate? ContextTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item) => item is SplitCell cell
        ? cell.Kind switch
        {
            DiffLineKind.Added => AddedTemplate,
            DiffLineKind.Removed => RemovedTemplate,
            _ => ContextTemplate,
        }
        : ContextTemplate;

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) => SelectTemplateCore(item);
}
