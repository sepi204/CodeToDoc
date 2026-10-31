<#
.SYNOPSIS
    Creates sample ZIP archives used to manually exercise the Phase 1 endpoint.

.EXAMPLE
    pwsh -File .\create-sample-zips.ps1

    The script writes the following files next to this script:
      - valid-dotnet.zip   (.NET project with a bin/ folder that should be skipped)
      - valid-next.zip     (Next.js/React project with node_modules and a logo that should be skipped)
      - zip-slip.zip       (entry named "../outside.txt" that must be rejected)
#>

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$outDir = $PSScriptRoot

function New-ZipArchive {
    param(
        [string]$Path,
        [hashtable]$Entries
    )

    if (Test-Path $Path) { Remove-Item $Path -Force }

    $fileStream = [System.IO.File]::Open($Path, [System.IO.FileMode]::CreateNew)
    try {
        $zip = [System.IO.Compression.ZipArchive]::new($fileStream, [System.IO.Compression.ZipArchiveMode]::Create)
        try {
            foreach ($name in $Entries.Keys) {
                $entry = $zip.CreateEntry($name)
                $writer = [System.IO.StreamWriter]::new($entry.Open())
                try { $writer.Write($Entries[$name]) } finally { $writer.Dispose() }
            }
        }
        finally { $zip.Dispose() }
    }
    finally { $fileStream.Dispose() }
}

# 1. valid-dotnet.zip -> .NET detected; bin/ content skipped
New-ZipArchive -Path (Join-Path $outDir 'valid-dotnet.zip') -Entries @{
    'src/App.csproj'       = '<Project Sdk="Microsoft.NET.Sdk" />'
    'src/Program.cs'       = 'Console.WriteLine("Hello");'
    'src/bin/Debug/App.dll' = 'binary-content'
}

# 2. valid-next.zip -> Node.js, Next.js and React detected; node_modules + logo skipped
New-ZipArchive -Path (Join-Path $outDir 'valid-next.zip') -Entries @{
    'package.json'                    = '{"name":"app","dependencies":{"next":"14.0.0","react":"18.2.0"}}'
    'src/app/page.tsx'                = 'export default function Page() { return <div />; }'
    'node_modules/ignored/index.js'   = '// ignored'
    'public/logo.png'                 = 'png-binary'
}

# 3. zip-slip.zip -> must be rejected; ../outside.txt must never be created
New-ZipArchive -Path (Join-Path $outDir 'zip-slip.zip') -Entries @{
    '../outside.txt' = 'should-not-be-written'
}

Write-Host "Created sample archives in $outDir :"
Get-ChildItem -Path $outDir -Filter '*.zip' | ForEach-Object { Write-Host "  - $($_.Name)" }

Write-Host ""
Write-Host "Try them with:"
Write-Host '  curl -X POST "http://localhost:5000/api/projects/process-phase-one" -F "archives=@valid-dotnet.zip;type=application/zip"'
Write-Host '  curl -X POST "http://localhost:5000/api/projects/process-phase-one" -F "archives=@valid-dotnet.zip;type=application/zip" -F "archives=@valid-next.zip;type=application/zip"'
Write-Host '  curl -X POST "http://localhost:5000/api/projects/process-phase-one" -F "archives=@zip-slip.zip;type=application/zip"'
