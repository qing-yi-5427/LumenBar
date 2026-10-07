using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
namespace LumenBar;
public sealed class LiquidEffect : ShaderEffect
{
    static PixelShader? shader;
    public static readonly DependencyProperty InputProperty=RegisterPixelShaderSamplerProperty("Input",typeof(LiquidEffect),0);
    public static readonly DependencyProperty GeometryProperty=DependencyProperty.Register("Geometry",typeof(System.Windows.Media.Media3D.Point4D),typeof(LiquidEffect),new UIPropertyMetadata(new System.Windows.Media.Media3D.Point4D(200,80,40,0),PixelShaderConstantCallback(0)));
    public static readonly DependencyProperty TintProperty=DependencyProperty.Register("Tint",typeof(Color),typeof(LiquidEffect),new UIPropertyMetadata(Colors.Transparent,PixelShaderConstantCallback(1)));
    public Brush Input {get=>(Brush)GetValue(InputProperty);set=>SetValue(InputProperty,value);}
    public System.Windows.Media.Media3D.Point4D Geometry {get=>(System.Windows.Media.Media3D.Point4D)GetValue(GeometryProperty);set=>SetValue(GeometryProperty,value);}
    public Color Tint {get=>(Color)GetValue(TintProperty);set=>SetValue(TintProperty,value);}
    public LiquidEffect()
    {
        if(shader is null)
        {
            using var text=typeof(LiquidEffect).Assembly.GetManifestResourceStream("LumenBar.LiquidGlass.hlsl")!;
            using var reader=new StreamReader(text);var bytes=Encoding.UTF8.GetBytes(reader.ReadToEnd());
            int hr=D3DCompile(bytes,(nuint)bytes.Length,"LiquidGlass",IntPtr.Zero,IntPtr.Zero,"main","ps_2_0",1<<15,0,out var code,out var errors);
            try
            {
                if(hr<0)throw new InvalidOperationException(errors is null?"Glass shader compilation failed":Marshal.PtrToStringAnsi(errors.GetBufferPointer()));
                var compiled=new byte[(int)code!.GetBufferSize()];Marshal.Copy(code.GetBufferPointer(),compiled,0,compiled.Length);
                shader=new PixelShader();shader.SetStreamSource(new MemoryStream(compiled));shader.Freeze();
            }
            finally{if(code is not null)Marshal.ReleaseComObject(code);if(errors is not null)Marshal.ReleaseComObject(errors);}
        }
        PixelShader=shader;UpdateShaderValue(InputProperty);UpdateShaderValue(GeometryProperty);UpdateShaderValue(TintProperty);
    }
    [ComImport,Guid("8BA5FB08-5195-40E2-AC58-0D989C3A0102"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface Blob {[PreserveSig] IntPtr GetBufferPointer();[PreserveSig] nuint GetBufferSize();}
    [DllImport("d3dcompiler_47.dll",CallingConvention=CallingConvention.StdCall)]
    static extern int D3DCompile(byte[] source,nuint length,[MarshalAs(UnmanagedType.LPStr)] string name,IntPtr defines,IntPtr include,[MarshalAs(UnmanagedType.LPStr)] string entry,[MarshalAs(UnmanagedType.LPStr)] string target,uint flags,uint flags2,out Blob? code,out Blob? errors);
}
