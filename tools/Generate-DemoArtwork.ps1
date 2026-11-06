# Original geometric illustrations for the offline demonstration library.
Add-Type -AssemblyName System.Drawing
$output = Join-Path $PSScriptRoot '..\src\Spectro.App\Assets\Demo'
New-Item -ItemType Directory -Path $output -Force | Out-Null
foreach ($kind in @('paper', 'chip', 'landscape', 'orbit', 'city')) {
    $bitmap = [Drawing.Bitmap]::new(480, 360)
    $g = [Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([Drawing.ColorTranslator]::FromHtml('#DFD7C6'))
    $ink = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#354B48'))
    $warm = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#B46D48'))
    $paper = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#F7F1E3'))
    $line = [Drawing.Pen]::new($ink, 5)
    switch ($kind) {
        'paper' {
            $g.FillRectangle($warm, 80, 68, 260, 248)
            $g.TranslateTransform(240, 175); $g.RotateTransform(-10)
            $g.FillRectangle($paper, -122, -136, 244, 272)
            $g.FillRectangle($ink, -88, -100, 62, 12)
            $g.FillRectangle($warm, -88, -68, 172, 60)
            foreach ($y in @(18, 38, 58, 78)) { $g.DrawLine($line, -88, $y, 88, $y) }
            $g.ResetTransform()
        }
        'chip' {
            $g.Clear([Drawing.ColorTranslator]::FromHtml('#2F4547'))
            $g.FillRectangle($warm, 154, 94, 172, 172)
            $g.FillRectangle($ink, 170, 110, 140, 140)
            $pin = [Drawing.Pen]::new($paper, 5)
            foreach ($x in (0..6)) {
                $p = 175 + $x * 20
                $g.DrawLine($pin, $p, 60, $p, 90); $g.DrawLine($pin, $p, 270, $p, 300)
                $g.DrawLine($pin, 120, ($p - 60), 150, ($p - 60)); $g.DrawLine($pin, 330, ($p - 60), 360, ($p - 60))
            }
            $g.DrawRectangle($pin, 194, 134, 92, 92)
            $pin.Dispose()
        }
        'landscape' {
            $g.Clear([Drawing.ColorTranslator]::FromHtml('#CCD9D5'))
            $g.FillEllipse($paper, 320, 40, 72, 72)
            $g.FillPolygon($warm, [Drawing.Point[]]@([Drawing.Point]::new(0,300),[Drawing.Point]::new(170,75),[Drawing.Point]::new(390,360),[Drawing.Point]::new(0,360)))
            $g.FillPolygon($ink, [Drawing.Point[]]@([Drawing.Point]::new(80,360),[Drawing.Point]::new(335,140),[Drawing.Point]::new(480,290),[Drawing.Point]::new(480,360)))
            $g.FillRectangle($paper, 280, 247, 55, 43)
            $g.FillPolygon($warm, [Drawing.Point[]]@([Drawing.Point]::new(270,248),[Drawing.Point]::new(307,217),[Drawing.Point]::new(345,248)))
        }
        'orbit' {
            $g.Clear([Drawing.ColorTranslator]::FromHtml('#293E48'))
            $g.FillEllipse($warm, 120, 60, 240, 240)
            $g.DrawEllipse([Drawing.Pens]::Wheat, 55, 115, 370, 125)
            $g.FillEllipse($paper, 350, 178, 24, 24)
            foreach ($p in @(@(35,50),@(430,72),@(390,310),@(70,290),@(275,25))) {
                $g.FillEllipse($paper, $p[0], $p[1], 4, 4)
            }
        }
        'city' {
            $g.Clear([Drawing.ColorTranslator]::FromHtml('#D4DCD6'))
            foreach ($i in (0..5)) {
                $height = @(145,225,180,260,200,160)[$i]
                $x = $i * 85 - 10
                $brush = if ($i % 2) { $warm } else { $ink }
                $g.FillRectangle($brush, $x, (360 - $height), 74, $height)
                foreach ($row in (0..3)) { $g.FillRectangle($paper, ($x + 16), (380 - $height + $row * 34), 12, 18) }
            }
        }
    }
    $bitmap.Save((Join-Path $output "$kind.png"), [Drawing.Imaging.ImageFormat]::Png)
    $line.Dispose(); $paper.Dispose(); $warm.Dispose(); $ink.Dispose(); $g.Dispose(); $bitmap.Dispose()
}
