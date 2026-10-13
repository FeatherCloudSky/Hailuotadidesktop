param([switch]$UninstallerOnly, [string]$OutputPath = '')
$ErrorActionPreference = 'Stop'
$petRoot = Split-Path -Parent $PSScriptRoot
$petFramework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$petCompiler = Join-Path $petFramework 'csc.exe'
$petCommon = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/codepage:65001',
    ('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')),
    ('/win32icon:' + (Join-Path $petRoot 'assets\pet.ico')),
    ('/reference:' + (Join-Path $petFramework 'System.dll')),
    ('/reference:' + (Join-Path $petFramework 'System.Core.dll')),
    ('/reference:' + (Join-Path $petFramework 'System.Drawing.dll')),
    ('/reference:' + (Join-Path $petFramework 'System.Windows.Forms.dll')),
    ('/reference:' + (Join-Path $petFramework 'System.IO.Compression.dll')),
    ('/reference:' + (Join-Path $petFramework 'System.IO.Compression.FileSystem.dll')),
    ('/reference:' + (Join-Path $petFramework 'Microsoft.CSharp.dll')))
$petUninstaller = Join-Path $petRoot 'build\app\卸载海洛塔帝桌宠.exe'
& $petCompiler @petCommon '/define:UNINSTALLER' ('/out:' + $petUninstaller) (Join-Path $PSScriptRoot 'Setup.cs') (Join-Path $PSScriptRoot 'AssemblyInfo.cs')
if ($LASTEXITCODE -ne 0) { throw 'Uninstaller build failed.' }
if ($UninstallerOnly) { Write-Output $petUninstaller; return }
$petSetup = if ($OutputPath) { [IO.Path]::GetFullPath($OutputPath) } else { Join-Path (Split-Path -Parent $petRoot) '海洛塔帝桌宠-一键安装.exe' }
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $petSetup) | Out-Null
& $petCompiler @petCommon ('/out:' + $petSetup) ('/resource:' + (Join-Path $petRoot 'build\payload.zip') + ',payload.zip') (Join-Path $PSScriptRoot 'Setup.cs') (Join-Path $PSScriptRoot 'AssemblyInfo.cs')
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
Write-Output $petSetup
