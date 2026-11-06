# Deterministic rasterization of the code-owned PennCraft enclosure mark. Requires Windows.
param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../src/PennCraft.Desktop/Assets'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
function RoundedPath([single]$x, [single]$y, [single]$width, [single]$height, [single]$radius) {
    $shape = [Drawing.Drawing2D.GraphicsPath]::new()
    $d = 2 * $radius
    $shape.AddArc($x, $y, $d, $d, 180, 90)
    $shape.AddArc(($x + $width - $d), $y, $d, $d, 270, 90)
    $shape.AddArc(($x + $width - $d), ($y + $height - $d), $d, $d, 0, 90)
    $shape.AddArc($x, ($y + $height - $d), $d, $d, 90, 90)
    $shape.CloseFigure()
    return $shape
}
$images = @()
foreach ($size in @(16, 20, 24, 32, 40, 48, 64, 128, 256)) {
    $bitmap = [Drawing.Bitmap]::new($size, $size)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.ScaleTransform(($size / 64.0), ($size / 64.0))
    $navy = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#0f172a'))
    $cyan = [Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#38bdf8'), 3)
    $white = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#e2e8f0'))
    $mint = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#2dd4bf'))
    $background = RoundedPath 2 2 60 60 14
    $enclosure = RoundedPath 11 12 42 42 6
    try {
        $graphics.FillPath($navy, $background)
        $graphics.DrawPath($cyan, $enclosure)
        $graphics.DrawLine($cyan, 12, 24, 52, 24)
        $graphics.FillRectangle($white, 19, 31, 8, 8)
        $graphics.FillRectangle($white, 34, 43, 8, 8)
        $graphics.FillRectangle($mint, 34, 31, 8, 8)
        $graphics.FillRectangle($mint, 19, 43, 8, 8)
        $stream = [IO.MemoryStream]::new()
        try {
            $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
            $images += ,@{ Size = $size; Data = $stream.ToArray() }
            if ($size -eq 256) { [IO.File]::WriteAllBytes((Join-Path $OutputDirectory 'PennCraft.png'), $stream.ToArray()) }
        } finally { $stream.Dispose() }
    } finally {
        $graphics.Dispose(); $bitmap.Dispose(); $navy.Dispose(); $cyan.Dispose()
        $white.Dispose(); $mint.Dispose(); $background.Dispose(); $enclosure.Dispose()
    }
}
$icon = [IO.MemoryStream]::new()
$writer = [IO.BinaryWriter]::new($icon)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$images.Count)
    $offset = 6 + 16 * $images.Count
    foreach ($entry in $images) {
        $dimension = if ($entry.Size -eq 256) { 0 } else { $entry.Size }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$entry.Data.Length); $writer.Write([uint32]$offset)
        $offset += $entry.Data.Length
    }
    foreach ($entry in $images) { $writer.Write([byte[]]$entry.Data) }
    [IO.File]::WriteAllBytes((Join-Path $OutputDirectory 'PennCraft.ico'), $icon.ToArray())
} finally { $writer.Dispose(); $icon.Dispose() }
