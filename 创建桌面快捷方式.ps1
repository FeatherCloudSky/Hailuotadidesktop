$ErrorActionPreference = 'Stop'
$petRoot = $PSScriptRoot
$petDesktop = [Environment]::GetFolderPath('Desktop')
$petTarget = Join-Path $petRoot '海洛塔帝桌宠.exe'
$petLinkPath = Join-Path $petDesktop '海洛塔帝桌宠.lnk'
$petShell = New-Object -ComObject WScript.Shell
if (Test-Path -LiteralPath $petLinkPath) {
    $petOldLink = $petShell.CreateShortcut($petLinkPath)
    if ($petOldLink.TargetPath -ne $petTarget) {
        $petLinkPath = Join-Path $petDesktop '海洛塔帝桌宠（新版）.lnk'
        if (Test-Path -LiteralPath $petLinkPath) { throw '已有同名快捷方式，请手动选择其他名称。' }
    }
}
$petLink = $petShell.CreateShortcut($petLinkPath)
$petLink.TargetPath = $petTarget
$petLink.WorkingDirectory = $petRoot
$petLink.IconLocation = $petTarget + ',0'
$petLink.Description = '海洛塔帝桌宠：点击互动、拖动、滚轮缩放；右键或托盘退出。'
$petLink.WindowStyle = 1
$petLink.Save()
$petVerify = $petShell.CreateShortcut($petLinkPath)
if ($petVerify.TargetPath -ne $petTarget) { throw '快捷方式目标验证失败。' }
[pscustomobject]@{ Shortcut = $petLinkPath; Target = $petVerify.TargetPath; Icon = $petVerify.IconLocation } | ConvertTo-Json
