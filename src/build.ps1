param([string]$OutputPath = '')
$ErrorActionPreference = 'Stop'
$petRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputPath) { $OutputPath = Join-Path $petRoot '海洛塔帝桌宠.exe' }
New-Item -ItemType Directory -Force -Path (Split-Path -Parent ([IO.Path]::GetFullPath($OutputPath))) | Out-Null
$petCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$petFramework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$petWpf = Join-Path $petFramework 'WPF'
$petArgs = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/codepage:65001',
    ('/out:' + $OutputPath),
    ('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')),
    ('/win32icon:' + (Join-Path $petRoot 'assets\pet.ico')),
    ('/reference:' + (Join-Path $petFramework 'System.dll')),
    ('/reference:' + (Join-Path $petFramework 'System.Core.dll')),
    ('/reference:' + (Join-Path $petFramework 'System.Xaml.dll')),
    ('/reference:' + (Join-Path $petFramework 'System.Security.dll')),
    ('/reference:' + (Join-Path $petFramework 'System.Web.Extensions.dll')),
    ('/reference:' + (Join-Path $petFramework 'System.Drawing.dll')),
    ('/reference:' + (Join-Path $petFramework 'System.Windows.Forms.dll')),
    ('/reference:' + (Join-Path $petWpf 'WindowsBase.dll')),
    ('/reference:' + (Join-Path $petWpf 'PresentationCore.dll')),
    ('/reference:' + (Join-Path $petWpf 'PresentationFramework.dll')),
    (Join-Path $PSScriptRoot 'Pet.cs'),
    (Join-Path $PSScriptRoot 'Ai.cs'),
    (Join-Path $PSScriptRoot 'ChatUi.cs'),
    (Join-Path $PSScriptRoot 'BubbleChat.cs'),
    (Join-Path $PSScriptRoot 'ScreenCapture.cs'),
    (Join-Path $PSScriptRoot 'AiChecks.cs'),
    (Join-Path $PSScriptRoot 'AssemblyInfo.cs'))
& $petCompiler @petArgs
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Output $OutputPath
