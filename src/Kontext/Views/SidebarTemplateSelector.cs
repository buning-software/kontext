using Kontext.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Kontext.Views;

public sealed class SidebarTemplateSelector : DataTemplateSelector
{
    public DataTemplate? GroupTemplate { get; set; }

    public DataTemplate? IssueTemplate { get; set; }

    public DataTemplate? GeneralTemplate { get; set; }

    public DataTemplate? SeparatorTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item) => item switch
    {
        SidebarSeparator => SeparatorTemplate,
        GeneralSessionViewModel => GeneralTemplate,
        RepositoryGroupViewModel => GroupTemplate,
        _ => IssueTemplate,
    };

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) => SelectTemplateCore(item);
}
