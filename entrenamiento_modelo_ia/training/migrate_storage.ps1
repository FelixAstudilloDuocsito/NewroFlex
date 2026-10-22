$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source = [IO.Path]::GetFullPath((Join-Path $workspace '.cache-training'))
$destination = 'D:\NeuroFlex\cache-training'
if ($source -ne (Join-Path $workspace '.cache-training')) { throw 'Origen inesperado' }
if ((Get-Volume -DriveLetter D).FileSystemLabel -ne 'HDD') { throw 'Verificar disco D' }
if (Test-Path -LiteralPath $destination) { throw 'El destino ya existe; no se sobrescribe' }
$files = @(Get-ChildItem -LiteralPath $source -Recurse -Force)
if ($files | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) { throw 'Origen contiene enlaces; requiere revisión' }
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$verified = @()
$discarded = @()
foreach ($file in ($files | Where-Object { -not $_.PSIsContainer })) {
    if ($file.Name.EndsWith('.incomplete')) { $discarded += $file.FullName; continue }
    $relative = $file.FullName.Substring($source.Length + 1)
    $target = [IO.Path]::GetFullPath((Join-Path $destination $relative))
    if (-not $target.StartsWith($destination + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Destino fuera de carpeta' }
    New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $target
    $originalHash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    if ($originalHash -ne (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash) { throw "Copia incorrecta: $relative" }
    $verified += @{path=$relative; bytes=$file.Length; sha256=$originalHash}
}
if ($verified.Count -eq 0) { throw 'No hay archivos verificados' }
@{source=$source; destination=$destination; verified=$verified; discarded_incomplete=$discarded} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $workspace 'results/storage_migration.json') -Encoding UTF8
# Solo se elimina el origen exacto, bajo el workspace, después de verificar cada copia.
if ([IO.Path]::GetFullPath($source) -ne (Join-Path $workspace '.cache-training')) { throw 'Origen cambió' }
Remove-Item -LiteralPath $source -Recurse -Force
New-Item -ItemType Directory -Path 'D:\NeuroFlex\runs' -Force | Out-Null
@{hf_home=$destination; runs_dir='D:\NeuroFlex\runs'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $workspace 'storage.local.json') -Encoding UTF8
Get-Volume -DriveLetter C,D | Select-Object DriveLetter,SizeRemaining | ConvertTo-Json
