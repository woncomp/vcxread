# 测试脚本
$env:VSINSTALLDIR = "C:\Program Files\Microsoft Visual Studio\2022\Community"

Write-Host "=== 测试 list-units ==="
$output = dotnet run -- list-units "F:\Workspace\MantleDX11\Mantle11\Mantle11.vcxproj" -p x64 -c Debug 2>$null
$units = ($output | Select-String "\.cpp").Count
Write-Host "编译单元数量: $units"

Write-Host "`n=== 测试 generate (compile_commands.json) ==="
dotnet run -- generate "F:\Workspace\MantleDX11\Mantle11\Mantle11.vcxproj" -p x64 -c Debug --format compile_commands -o test_output.json 2>$null
if (Test-Path test_output.json) {
    $commands = (Get-Content test_output.json | Select-String "\"file\"").Count
    Write-Host "生成命令数量: $commands"
    Remove-Item test_output.json
} else {
    Write-Host "错误: 未生成输出文件"
}

Write-Host "`n测试完成!"
