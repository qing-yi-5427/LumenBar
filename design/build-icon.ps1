$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assets = Join-Path $PSScriptRoot '../src/LumenBar.WinUI/Assets'
$source = [Drawing.Bitmap]::new((Join-Path $assets 'LumenBar.png'))
try {
    $sizes = @(16,20,24,32,40,48,64,128,256)
    $frames = foreach ($size in $sizes) {
        $frame = [Drawing.Bitmap]::new($size,$size,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [Drawing.Graphics]::FromImage($frame)
        $stream = [IO.MemoryStream]::new()
        try {
            $graphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.DrawImage($source,[Drawing.Rectangle]::new(0,0,$size,$size))
            $frame.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
            [pscustomobject]@{Size=$size;Bytes=$stream.ToArray()}
        } finally {$graphics.Dispose();$frame.Dispose();$stream.Dispose()}
    }
    $file = [IO.File]::Create((Join-Path $assets 'LumenBar.ico'))
    $writer = [IO.BinaryWriter]::new($file)
    try {
        $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$frames.Count)
        $offset = 6 + 16*$frames.Count
        foreach ($frame in $frames) {
            $side = if($frame.Size -eq 256){0}else{$frame.Size}
            $writer.Write([byte]$side);$writer.Write([byte]$side);$writer.Write([byte]0);$writer.Write([byte]0)
            $writer.Write([uint16]1);$writer.Write([uint16]32)
            $writer.Write([uint32]$frame.Bytes.Length);$writer.Write([uint32]$offset)
            $offset += $frame.Bytes.Length
        }
        foreach ($frame in $frames) {$writer.Write([byte[]]$frame.Bytes)}
    } finally {$writer.Dispose();$file.Dispose()}
} finally {$source.Dispose()}
