$env:VSINSTALLDIR = "C:\Program Files\Microsoft Visual Studio\2022\Community"
dotnet run -- generate "F:\Workspace\MantleDX11\Mantle11\Mantle11.vcxproj" --platform x64 --config Debug --format compile_commands --output test.json 2>$null
if (Test-Path test.json) {
    Write-Host "成功生成 test.json"
    $count = (Get-Content test.json | Select-String '"file"').Count
    Write-Host "包含 $count 个编译命令"
    Remove-Item test.json
} else {
    Write-Host "错误: 未生成输出文件"
}