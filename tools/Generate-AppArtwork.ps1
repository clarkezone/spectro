# Spectro's original folded-page mark, rendered at native asset sizes.
Add-Type -AssemblyName System.Drawing
$assets = Join-Path $PSScriptRoot '..\src\Spectro.App\Assets'
function New-Mark([int]$width, [int]$height) {
    $image = [Drawing.Bitmap]::new($width, $height)
    $g = [Drawing.Graphics]::FromImage($image)
    $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([Drawing.Color]::Transparent)
    $size = [Math]::Min($width, $height) * 0.7
    $g.TranslateTransform(($width - $size) / 2, ($height - $size) / 2)
    $g.ScaleTransform($size / 100, $size / 100)
    $rust = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#A44B25'))
    $paper = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#FFFEFB'))
    $fold = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#E1AD8C'))
    $g.FillRectangle($rust, 6, 4, 88, 92)
    $g.FillPolygon($paper, [Drawing.Point[]]@([Drawing.Point]::new(27,18),[Drawing.Point]::new(58,18),[Drawing.Point]::new(77,37),[Drawing.Point]::new(77,82),[Drawing.Point]::new(27,82)))
    $g.FillPolygon($fold, [Drawing.Point[]]@([Drawing.Point]::new(58,18),[Drawing.Point]::new(58,37),[Drawing.Point]::new(77,37)))
    $g.FillRectangle($rust, 37, 48, 30, 4)
    $g.FillRectangle($rust, 37, 59, 30, 4)
    $g.FillRectangle($rust, 37, 70, 20, 4)
    $fold.Dispose(); $paper.Dispose(); $rust.Dispose(); $g.Dispose()
    return $image
}
$sizes = @{
    'Square44x44Logo.scale-200.png' = @(88,88)
    'Square44x44Logo.targetsize-24_altform-unplated.png' = @(24,24)
    'Square44x44Logo.targetsize-48_altform-lightunplated.png' = @(48,48)
    'Square150x150Logo.scale-200.png' = @(300,300)
    'StoreLogo.png' = @(50,50)
    'Wide310x150Logo.scale-200.png' = @(620,300)
    'SplashScreen.scale-200.png' = @(1240,600)
    'LockScreenLogo.scale-200.png' = @(48,48)
}
foreach ($name in $sizes.Keys) {
    $image = New-Mark $sizes[$name][0] $sizes[$name][1]
    $image.Save((Join-Path $assets $name), [Drawing.Imaging.ImageFormat]::Png)
    $image.Dispose()
}
$iconSizes = @(16,32,48,256)
$blobs = foreach ($size in $iconSizes) {
    $image = New-Mark $size $size
    $stream = [IO.MemoryStream]::new()
    $image.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
    ,$stream.ToArray()
    $stream.Dispose(); $image.Dispose()
}
$output = [IO.File]::Create((Join-Path $assets 'AppIcon.ico'))
$writer = [IO.BinaryWriter]::new($output)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]4)
$offset = 6 + 16 * 4
for ($i = 0; $i -lt 4; $i++) {
    $dimension = if ($iconSizes[$i] -eq 256) { 0 } else { $iconSizes[$i] }
    $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
    $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$blobs[$i].Length); $writer.Write([uint32]$offset)
    $offset += $blobs[$i].Length
}
foreach ($blob in $blobs) { $writer.Write([byte[]]$blob) }
$writer.Dispose()
