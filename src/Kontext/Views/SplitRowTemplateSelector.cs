using Kontext.Domain;
using Kontext.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Kontext.Views;

/// <summary>Picks the layout of a split-diff row: two sides, or one cell across both for a hunk header.</summary>
public sealed class SplitRowTemplateSelector : DataTemplateSelector
{
    public DataTemplate? PairTemplate { get; set; }

    public DataTemplate? SpanTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item) => item is SplitDiffRow { Spanning: not null } ? SpanTemplate : PairTemplate;

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) => SelectTemplateCore(item);
}
