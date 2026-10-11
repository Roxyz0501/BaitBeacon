$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$size = 512
$bitmap = [System.Drawing.Bitmap]::new($size, $size)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::FromArgb(14, 28, 42))
$gold = [System.Drawing.Color]::FromArgb(255, 191, 72)
$pale = [System.Drawing.Color]::FromArgb(255, 234, 166)
$blue = [System.Drawing.Color]::FromArgb(55, 170, 202)
$ring = [System.Drawing.Pen]::new($gold, 17)
$ring.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$ring.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$graphics.DrawEllipse($ring, 48, 48, 416, 416)

$line = [System.Drawing.Pen]::new($pale, 25)
$line.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$line.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$graphics.DrawLine($line, 265, 116, 265, 300)
$graphics.DrawArc($line, 176, 226, 179, 179, 355, 190)
$graphics.DrawLine($line, 178, 310, 194, 270)

$baitBrush = [System.Drawing.SolidBrush]::new($blue)
$graphics.FillEllipse($baitBrush, 247, 138, 92, 92)
$shine = [System.Drawing.SolidBrush]::new($pale)
$graphics.FillEllipse($shine, 268, 158, 20, 20)

$star = [System.Drawing.Drawing2D.GraphicsPath]::new()
$points = [System.Drawing.Point[]]@(
    [System.Drawing.Point]::new(147, 130),
    [System.Drawing.Point]::new(158, 153),
    [System.Drawing.Point]::new(181, 164),
    [System.Drawing.Point]::new(158, 175),
    [System.Drawing.Point]::new(147, 198),
    [System.Drawing.Point]::new(136, 175),
    [System.Drawing.Point]::new(113, 164),
    [System.Drawing.Point]::new(136, 153)
)
$star.AddPolygon($points)
$graphics.FillPath($shine, $star)
$output = Join-Path $PSScriptRoot '..\BaitBeacon\images\icon.png'
New-Item -ItemType Directory -Force -Path (Split-Path $output) | Out-Null
$bitmap.Save($output, [System.Drawing.Imaging.ImageFormat]::Png)
$star.Dispose()
$shine.Dispose()
$baitBrush.Dispose()
$line.Dispose()
$ring.Dispose()
$graphics.Dispose()
$bitmap.Dispose()
