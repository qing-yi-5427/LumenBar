using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
namespace LumenBar.WinUI;
// The root stays transparent; each native Acrylic element supplies its own antialiased silhouette.
internal sealed class TransparentBackdrop : SystemBackdrop
{
    readonly Windows.UI.Composition.Compositor compositor;
    public TransparentBackdrop()
    {
        Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().EnsureSystemDispatcherQueue();
        compositor=new Windows.UI.Composition.Compositor();
    }
    Windows.UI.Composition.CompositionColorBrush? brush;
    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop target,XamlRoot root)
    {
        base.OnTargetConnected(target,root);
        brush=compositor.CreateColorBrush(Windows.UI.Color.FromArgb(0,0,0,0));target.SystemBackdrop=brush;
    }
    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop target)
    {
        target.SystemBackdrop=null;brush?.Dispose();brush=null;base.OnTargetDisconnected(target);
    }
    protected override void OnDefaultSystemBackdropConfigurationChanged(ICompositionSupportsSystemBackdrop target,XamlRoot root)
    {
        // Transparency is independent of focus and theme; Acrylic elements handle their own policy.
    }
}
