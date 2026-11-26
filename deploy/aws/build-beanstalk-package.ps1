# Genera el paquete para AWS Elastic Beanstalk (plataforma .NET en Amazon Linux 2023).
#
# La aplicación se publica "self-contained" para linux-x64: el paquete trae su
# propio runtime de .NET 9, así que no depende de la versión de .NET que tenga
# la plataforma de Elastic Beanstalk. El Procfile arranca la aplicación en el
# puerto 5000, al que nginx de Elastic Beanstalk reenvía las peticiones.
#
# Uso (desde la raíz del repositorio):
#   powershell -ExecutionPolicy Bypass -File deploy\aws\build-beanstalk-package.ps1
# Resultado: artifacts\tiendaflow-beanstalk.zip

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$publish = Join-Path $root "artifacts\beanstalk"
$zip = Join-Path $root "artifacts\tiendaflow-beanstalk.zip"

if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
if (Test-Path $zip) { Remove-Item $zip -Force }

dotnet publish (Join-Path $root "src\TiendaFlow.Web\TiendaFlow.Web.csproj") `
    --configuration Release `
    --runtime linux-x64 `
    --self-contained true `
    --output $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish falló" }

# Procfile y configuración de Elastic Beanstalk junto a la aplicación publicada.
Copy-Item (Join-Path $PSScriptRoot "Procfile") $publish
Copy-Item (Join-Path $PSScriptRoot ".ebextensions") $publish -Recurse

# Elastic Beanstalk necesita rutas con "/" dentro del zip; Compress-Archive de
# Windows PowerShell 5.1 usa "\", así que se arma con System.IO.Compression.
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem $publish -Recurse -File | ForEach-Object {
        $entry = $_.FullName.Substring($publish.Length + 1).Replace("\", "/")
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive, $_.FullName, $entry, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally {
    $archive.Dispose()
}

$size = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Output "Paquete listo: $zip ($size MB)"
