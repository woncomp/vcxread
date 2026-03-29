$output = dotnet run -- list-files 'F:\Workspace\MantleDX11\Mantle11\Mantle11.vcxproj' -p x64 -c Debug 2>$null
$json = $output | ConvertFrom-Json
Write-Host "文件类型统计："
$json | Group-Object -Property type | Select-Object Name, Count | Format-Table