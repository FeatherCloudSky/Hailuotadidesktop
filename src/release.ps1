param([string]$PythonExecutable = 'python', [switch]$SkipChecks)
$ErrorActionPreference = 'Stop'
$petRoot = Split-Path -Parent $PSScriptRoot
$petApp = Join-Path $petRoot 'build\app'
$petDist = Join-Path $petRoot 'dist'
New-Item -ItemType Directory -Force -Path $petApp, $petDist, (Join-Path $petApp 'assets') | Out-Null
& (Join-Path $PSScriptRoot 'build.ps1') -OutputPath (Join-Path $petApp '海洛塔帝桌宠.exe')
foreach ($petAsset in @('idle.png', 'happy.png', 'pressed.png', 'think.png', 'logo.svg', 'logo.png', 'pet.ico')) {
    Copy-Item -LiteralPath (Join-Path $petRoot ('assets\' + $petAsset)) -Destination (Join-Path $petApp ('assets\' + $petAsset)) -Force
}
Copy-Item -LiteralPath (Join-Path $petRoot '用户指南.md') -Destination (Join-Path $petApp '使用说明.md') -Force
foreach ($petDoc in @('LICENSE', 'NOTICE.md')) { Copy-Item -LiteralPath (Join-Path $petRoot $petDoc) -Destination (Join-Path $petApp $petDoc) -Force }
if (-not $SkipChecks) {
    foreach ($petCheck in @('--self-test', '--chat-self-test')) {
        $petProcess = Start-Process -FilePath (Join-Path $petApp '海洛塔帝桌宠.exe') -ArgumentList $petCheck -PassThru
        if (-not $petProcess.WaitForExit(60000)) { $petProcess.Kill(); throw ('Check timed out: ' + $petCheck) }
        if ($petProcess.ExitCode -ne 0) { throw ('Check failed: ' + $petCheck + '; inspect build/app/checks') }
    }
}
& (Join-Path $PSScriptRoot 'build_installer.ps1') -UninstallerOnly
& $PythonExecutable (Join-Path $PSScriptRoot 'package.py')
if ($LASTEXITCODE -ne 0) { throw 'Installation payload packaging failed.' }
& (Join-Path $PSScriptRoot 'build_installer.ps1') -OutputPath (Join-Path $petDist '海洛塔帝桌宠-一键安装.exe')
Copy-Item -LiteralPath (Join-Path $petApp '海洛塔帝桌宠.exe') -Destination (Join-Path $petRoot '海洛塔帝桌宠.exe') -Force
& $PythonExecutable (Join-Path $PSScriptRoot 'package_portable.py') (Join-Path $petDist '海洛塔帝桌宠-便携版.zip')
if ($LASTEXITCODE -ne 0) { throw 'Portable packaging failed.' }
$petSums = foreach ($petFile in @('海洛塔帝桌宠-一键安装.exe', '海洛塔帝桌宠-便携版.zip')) { ((Get-FileHash -LiteralPath (Join-Path $petDist $petFile) -Algorithm SHA256).Hash.ToLowerInvariant()) + '  ' + $petFile }
[IO.File]::WriteAllLines((Join-Path $petDist 'SHA256SUMS.txt'), $petSums, [Text.UTF8Encoding]::new($false))
Write-Output ('Release files: ' + $petDist)
