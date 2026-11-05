using Kontext.Domain;
using Kontext.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Kontext.Views;

/// <summary>Picks the template for a group header or a changed file in the change list.</summary>
public sealed class GitRowTemplateSelector : DataTemplateSelector
{
    public DataTemplate? HeaderTemplate { get; set; }

    public DataTemplate? FileTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item) => item is GitChangeRow { IsHeader: true } ? HeaderTemplate : FileTemplate;

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) => SelectTemplateCore(item);
}

/// <summary>Gives group headers a container that cannot be selected.</summary>
public sealed class GitRowStyleSelector : StyleSelector
{
    public Style? HeaderStyle { get; set; }

    public Style? FileStyle { get; set; }

    protected override Style? SelectStyleCore(object item, DependencyObject container) => item is GitChangeRow { IsHeader: true } ? HeaderStyle : FileStyle;
}

/// <summary>Picks the layout of a split-diff row: two sides, or one cell across both for a hunk header.</summary>
public sealed class SplitRowTemplateSelector : DataTemplateSelector
{
    public DataTemplate? PairTemplate { get; set; }

    public DataTemplate? SpanTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item) => item is SplitDiffRow { Spanning: not null } ? SpanTemplate : PairTemplate;

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) => SelectTemplateCore(item);
}

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
