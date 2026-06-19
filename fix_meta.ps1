$files = Get-ChildItem -Path "Assets\Resources" -Recurse -Filter "*.png.meta"
foreach ($f in $files) {
    $content = Get-Content $f.FullName
    $content = $content -replace 'isReadable: 0', 'isReadable: 1'
    if ($f.FullName -match "TransparentBlocks") {
        $content = $content -replace 'alphaIsTransparency: 0', 'alphaIsTransparency: 1'
    }
    Set-Content -Path $f.FullName -Value $content
}
