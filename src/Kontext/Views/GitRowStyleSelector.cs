using Kontext.Domain;
using Kontext.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Kontext.Views;

/// <summary>Gives group headers a container that cannot be selected.</summary>
public sealed class GitRowStyleSelector : StyleSelector
{
    public Style? HeaderStyle { get; set; }

    public Style? FileStyle { get; set; }

    protected override Style? SelectStyleCore(object item, DependencyObject container) => item is GitChangeRow { IsHeader: true } ? HeaderStyle : FileStyle;
}
