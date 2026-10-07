using System.Runtime.InteropServices;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace LumenBar.WinUI;

// A temporary connection captures the element's external link, then yields to
// the built-in DesktopAcrylicBackdrop. Native WinUI retains the full material
// lifecycle, accessibility policy, theme and focus handling.
internal sealed class BackdropEdgeSmoothing : SystemBackdrop
{
    ICompositionSupportsSystemBackdrop? target;
    public bool IsConnected=>target is not null;
    public int? OriginalMode {get;private set;}
    public int? CurrentMode=>target is null?null:AccessMode(target,false);
    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget,XamlRoot root)
    {
        target=connectedTarget;OriginalMode=AccessMode(target,true);
    }
    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget) { }
    protected override void OnDefaultSystemBackdropConfigurationChanged(ICompositionSupportsSystemBackdrop connectedTarget,XamlRoot root) { }
    public void Detach()
    {
        // Close the external link on the UI thread before its managed wrapper is
        // finalized; the link owns UI-thread-affine native input resources.
        if(target is not null)WinRT.CastExtensions.As<IDisposable>(target).Dispose();
        target=null;
    }

    static int? AccessMode(ICompositionSupportsSystemBackdrop target,bool apply)
    {
        // ExternalBackdropBorderMode is documented separately from Visual.BorderMode
        // and omitted from stable C# projections. QI guards this narrow ABI bridge.
        var unknown=WinRT.MarshalInspectable<ICompositionSupportsSystemBackdrop>.FromManaged(target);
        nint link=0;
        try
        {
            var iid=new Guid("1054BF83-B35B-5FDE-8DD7-AC3BB3E6CE27");
            int result=Marshal.QueryInterface(unknown,in iid,out link);
            if(result==unchecked((int)0x80004002))return null;
            Marshal.ThrowExceptionForHR(result);
            var table=Marshal.ReadIntPtr(link);
            var get=Marshal.GetDelegateForFunctionPointer<GetMode>(Marshal.ReadIntPtr(table,7*IntPtr.Size));
            Marshal.ThrowExceptionForHR(get(link,out var mode));
            if(apply)
            {
                var set=Marshal.GetDelegateForFunctionPointer<SetMode>(Marshal.ReadIntPtr(table,8*IntPtr.Size));
                Marshal.ThrowExceptionForHR(set(link,(int)CompositionBorderMode.Soft));
            }
            return mode;
        }
        finally{if(link!=0)Marshal.Release(link);Marshal.Release(unknown);}
    }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetMode(nint instance,out int mode);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int SetMode(nint instance,int mode);
}
