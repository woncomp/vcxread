# vcxproj-parser 项目计划

## 项目概述
解析 Visual Studio vcxproj 文件，生成 compile_commands.json 或 .clangd 配置文件

## 技术栈
- 语言：C# (ReadyToRun 发布)
- 核心库：Microsoft.Build + Microsoft.Build.Locator
- 输出格式：.clangd 或 compile_commands.json

## 关键设计决策

### 1. 路径格式
- 全部使用正斜杠 `/`

### 2. PCH 处理
- 完全移除所有 PCH 参数（/Yu, /Yc, /Fp）

### 3. SolutionDir 处理
- 自动向上查找 .sln 文件
- 验证 .sln 包含目标 vcxproj
- 支持 --solution-path 手动指定

### 4. HLSL 文件
- list-files 中显示为 "type": "hlsl"
- 不包含在 list-units 中

### 5. MSBuild 版本
- 仅支持 VS2022+

## CLI 结构

```
vcxproj-parser <subcommand> [options] <vcxproj-path>

子命令：
  list-configs    列出可用配置
  list-units     列出编译单元
  list-files     列出所有文件
  generate       生成配置文件

选项：
  -p, --platform <x86/x64>
  -c, --config <Debug/Release>
  -s, --solution-path <path>
  -o, --output <path>
  -f, --format <clangd|compile_commands>
  --compiler <cl|clang-cl>
  --strict
  -v, --verbose
```

## 测试文件
F:\Workspace\MantleDX11\Mantle11\Mantle11.vcxproj

## 编译器路径
C:\Develop\Android\sdk\ndk\27.0.11902837\toolchains\llvm\prebuilt\windows-x86_64\bin\clang-cl.exe

## 子任务列表

### Phase 1: 项目脚手架
- [x] 创建设计文档
- [ ] 初始化 .NET 项目
- [ ] 添加 NuGet 包

### Phase 2: 核心解析器
- [ ] 实现 Solution 查找和验证
- [ ] 实现全局属性设置
- [ ] 实现 VcxprojAnalyzer

### Phase 3: 输出模块
- [ ] 实现 list-configs 子命令
- [ ] 实现 list-units 子命令
- [ ] 实现 list-files 子命令
- [ ] 实现 generate 子命令

### Phase 4: 测试验证
- [ ] 创建测试脚本
- [ ] 解析验证
- [ ] 编译验证
- [ ] ReadyToRun 发布
