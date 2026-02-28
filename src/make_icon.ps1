Add-Type -AssemblyName System.Drawing
$size = 32
$bmp = New-Object System.Drawing.Bitmap($size, $size)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::FromArgb(255, 76, 142, 248))
$tb = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$f = New-Object System.Drawing.Font('Arial', 11, [System.Drawing.FontStyle]::Bold)
$sf = New-Object System.Drawing.StringFormat
$sf.Alignment = [System.Drawing.StringAlignment]::Center
$sf.LineAlignment = [System.Drawing.StringAlignment]::Center
$g.DrawString('AP', $f, $tb, [System.Drawing.RectangleF]::new(0, 0, $size, $size), $sf)
$g.Dispose()
$hIcon = $bmp.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($hIcon)
$fs = [System.IO.File]::Create('D:\Nirav\Projects\Working\AppPilot\src\Assets\app.ico')
$icon.Save($fs)
$fs.Close()
$icon.Dispose()
[System.Runtime.InteropServices.Marshal]::DestroyIcon($hIcon)
$bmp.Dispose()
$f.Dispose()
$tb.Dispose()
Write-Host "Done: $((Get-Item 'D:\Nirav\Projects\Working\AppPilot\src\Assets\app.ico').Length) bytes"
