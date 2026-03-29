# vcxread 用户手册

## 简介

vcxread 是一个用于解析 Visual Studio C++ 项目文件（.vcxproj）的工具，能够生成 `compile_commands.json` 或 `.clangd` 配置文件，用于配合 clangd 等工具进行代码分析和智能提示。

## 系统要求

- Windows 10/11
- Visual Studio 2022（必须安装"使用 C++ 的桌面开发"工作负载）
- .NET 7.0 SDK（用于编译）

## 1. 如何编译

### 方法一：从源代码编译（推荐开发者）

1. **克隆或下载源代码**
   ```bash
   cd vcxread
   ```

2. **还原 NuGet 包**
   ```bash
   dotnet restore
   ```

3. **编译项目**
   ```bash
   dotnet build
   ```

4. **（可选）发布**
   ```bash
   dotnet publish -c Release -r win-x64 --self-contained -o publish
   ```
   
   发布后，可执行文件将位于 `publish/vcxread.exe`

### 方法二：使用 Visual Studio 编译

1. 打开 `vcxread.csproj` 文件
2. 选择 Release 配置
3. 点击"生成"菜单 → "生成解决方案"

## 2. 如何使用

### 环境准备（可选）

vcxread 会自动尝试查找 Visual Studio 2022。如果无法自动找到，可以设置环境变量：

```cmd
set VSINSTALLDIR=C:\Program Files\Microsoft Visual Studio\2022\Community\
```

或使用 PowerShell:
```powershell
$env:VSINSTALLDIR = "C:\Program Files\Microsoft Visual Studio\2022\Community\"
```

### 基本用法

```bash
vcxread <subcommand> [options] <vcxproj-file-path>
```

### 子命令说明

#### 2.1 list-configs - List Available Configurations

显示项目中所有可用的配置组合（Debug/Release × Win32/x64）。

```bash
vcxread list-configs "C:\MyProject\MyApp.vcxproj"
```

**输出示例：**
```json
{
  "default": { "config": "Debug", "platform": "Win32" },
  "available": [
    { "config": "Debug", "platform": "Win32" },
    { "config": "Debug", "platform": "x64" },
    { "config": "Release", "platform": "Win32" },
    { "config": "Release", "platform": "x64" }
  ]
}
```

#### 2.2 list-units - List Compilation Units

列出项目中所有的 C++ 源文件（.cpp, .c）。

```bash
vcxread list-units "C:\MyProject\MyApp.vcxproj" -p x64 -c Debug
```

**选项：**
- `-p, --platform`：Specify platform (x64 or Win32)
- `-c, --config`：Specify configuration (Debug or Release)
- `-s, --solution-path`：Manually specify solution path (optional)

**输出示例：**
```json
[
  {
    "file": "C:/MyProject/src/main.cpp",
    "type": "ClCompile",
    "excluded": false
  },
  {
    "file": "C:/MyProject/src/utils.cpp",
    "type": "ClCompile",
    "excluded": false
  }
]
```

#### 2.3 list-files - List All Referenced Files

列出项目中引用的所有文件（包括头文件、资源文件等）。

```bash
vcxread list-files "C:\MyProject\MyApp.vcxproj" -p x64 -c Debug
```

**输出示例：**
```json
[
  {
    "file": "C:/MyProject/src/main.cpp",
    "type": "ClCompile",
    "excluded": false
  },
  {
    "file": "C:/MyProject/include/main.h",
    "type": "ClInclude",
    "excluded": false
  },
  {
    "file": "C:/MyProject/shaders/effect.hlsl",
    "type": "hlsl",
    "excluded": false
  }
]
```

#### 2.4 generate - Generate Configuration Files

生成 `compile_commands.json` 或 `.clangd` 配置文件。

**生成 compile_commands.json：**
```bash
vcxread generate "C:\MyProject\MyApp.vcxproj" ^
  --platform x64 ^
  --config Debug ^
  --format compile_commands ^
  --output compile_commands.json
```

**生成 .clangd 配置文件：**
```bash
vcxread generate "C:\MyProject\MyApp.vcxproj" ^
  --platform x64 ^
  --config Debug ^
  --format clangd ^
  --output .clangd
```

**选项：**
- `-p, --platform`：Specify platform (x64 or Win32)
- `-c, --config`：Specify configuration (Debug or Release)
- `-f, --format`：Output format (compile_commands or clangd, default: compile_commands)
- `-o, --output`：Output file path
- `--compiler`：Specify compiler path (optional, default uses MSVC cl.exe)

**输出示例（compile_commands.json）：**
```json
[
  {
    "directory": "C:/MyProject",
    "command": "C:/.../cl.exe /I\"C:/MyProject/include\" /DWIN32 /D_DEBUG src/main.cpp",
    "file": "C:/MyProject/src/main.cpp"
  }
]
```

**输出示例（.clangd）：**
```yaml
CompileFlags:
  Add:
    - /I"C:/MyProject/include"
    - /DWIN32
    - /D_DEBUG
    - /std:c++17
```

### 全局选项

所有子命令都支持以下选项：

- `-v, --verbose`：Show verbose output
- `--strict`：Strict mode, exit immediately on error
- `-h, --help`：Show help information

### 使用示例

#### 示例 1：快速生成 compile_commands.json

```cmd
cd C:\MyProject
vcxread generate MyApp.vcxproj -p x64 -c Debug -o compile_commands.json
```

#### 示例 2：为 clangd 生成配置

```cmd
vcxread generate "C:\MyProject\MyApp.vcxproj" --format clangd --output .clangd
```

#### 示例 3：查看项目有哪些源文件

```cmd
vcxread list-units "C:\MyProject\MyApp.vcxproj" -p x64 -c Debug > files.json
```

#### 示例 4：使用特定编译器（如 clang-cl）

```cmd
vcxread generate "C:\MyProject\MyApp.vcxproj" ^
  --platform x64 ^
  --config Debug ^
  --compiler "C:\LLVM\bin\clang-cl.exe" ^
  --output compile_commands.json
```

## 3. 配置 clangd

### 使用 compile_commands.json

将生成的 `compile_commands.json` 放在项目根目录，clangd 会自动识别。

```
MyProject/
├── src/
├── include/
├── compile_commands.json  <-- 放在这里
└── MyApp.vcxproj
```

### 使用 .clangd

将生成的 `.clangd` 放在项目根目录。

```
MyProject/
├── src/
├── include/
├── .clangd               <-- 放在这里
└── MyApp.vcxproj
```

## 4. 故障排除

### 问题 1："Visual Studio 2022 not found"

**错误信息：**
```
Error: Visual Studio 2022 or higher not found.
```

**解决方案：**
确保已安装 Visual Studio 2022 并包含"使用 C++ 的桌面开发"工作负载。如果仍无法自动检测，手动设置环境变量：

```cmd
set VSINSTALLDIR=C:\Program Files\Microsoft Visual Studio\2022\Community\
```

### 问题 2："Could not load file or assembly 'Microsoft.Build'"

**原因：** 使用了 .NET SDK 的 MSBuild，而不是 Visual Studio 的 MSBuild。

**解决方案：** 确保正确设置了 `VSINSTALLDIR` 环境变量指向 Visual Studio 安装目录。

### 问题 3：生成的 compile_commands.json 为空

**检查清单：**
1. 确认 vcxproj 文件路径正确
2. 确认指定的 platform 和 config 在项目中存在
3. 使用 `--verbose` 查看详细信息

### 问题 4：路径中包含空格

如果路径包含空格，请使用双引号包裹：

```bash
vcxread generate "C:\My Projects\App\App.vcxproj" --output "C:\Output\compile_commands.json"
```

## 5. 注意事项

1. **PCH 处理**：工具会自动移除所有预编译头（PCH）相关参数（/Yu, /Yc, /Fp），因为 clangd 不需要这些参数。

2. **路径格式**：所有路径都使用正斜杠（/），这是为了与 clangd 更好地兼容。

3. **外部依赖**：如果项目依赖外部库（如 DirectXTK），确保这些库的头文件路径在 vcxproj 中正确配置。

4. **多项目解决方案**：如果 vcxproj 是解决方案的一部分，工具会自动查找同目录下的 .sln 文件。如果找不到，可以使用 `--solution-path` 手动指定。

## 6. 项目结构

```
vcxread/
├── Program.cs              # Main entry
├── Commands/               # Subcommand implementations
│   ├── ListConfigsCommand.cs
│   ├── ListUnitsCommand.cs
│   ├── ListFilesCommand.cs
│   └── GenerateCommand.cs
├── Core/                   # Core analyzer
│   └── VcxprojAnalyzer.cs
├── test/                   # Test outputs (ignored by git)
├── publish/                # Publish outputs (ignored by git)
├── vcxread.csproj          # Project file
├── README.md               # This file
└── .gitignore              # Git ignore rules
```

## 7. 支持

如有问题，请检查：
1. Visual Studio 2022 是否正确安装
2. `VSINSTALLDIR` 环境变量是否正确设置（如果需要）
3. vcxproj 文件是否有效

## 附录：完整命令参考

```
vcxread <subcommand> [options] <vcxproj-file>

Subcommands:
  list-configs    List all available configuration combinations
  list-units     List all compilation units (ClCompile)
  list-files     List all referenced files
  generate       Generate configuration files (compile_commands.json or .clangd)

Global Options:
  -v, --verbose      Show verbose output
  --strict           Strict mode, exit on error
  -h, --help         Show help

generate Options:
  -p, --platform     Specify platform (x64, Win32)
  -c, --config       Specify configuration (Debug, Release)
  -f, --format       Output format (compile_commands, clangd)
  -o, --output       Output file path
  --compiler         Specify compiler path
```

---

*版本：1.0*
*最后更新：2026-03-29*
