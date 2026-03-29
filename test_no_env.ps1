# 测试不设置环境变量
Write-Host "=== 测试不设置 VSINSTALLDIR ===" -ForegroundColor Green
dotnet run -- list-configs "F:\Workspace\MantleDX11\Mantle11\Mantle11.vcxproj" 2>$null | Select-String "从常见路径找到|错误|{"

Write-Host "`n=== 测试完成 ===" -ForegroundColor Green