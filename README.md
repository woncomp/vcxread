# vcxproj-parser 用户手册

## 简介

vcxproj-parser 是一个用于解析 Visual Studio C++ 项目文件（.vcxproj）的工具，能够生成 `compile_commands.json` 或 `.clangd` 配置文件，用于配合 clangd 等工具进行代码分析和智能提示。

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

4. **（可选）发布为单文件可执行程序**
   ```bash
   dotnet publish -c Release -r win-x64 --self-contained true ^
     -p:PublishReadyToRun=true -p:PublishSingleFile=true -o publish
   ```
   
   发布后，可执行文件将位于 `publish/vcxproj-parser.exe`

### 方法二：使用 Visual Studio 编译

1. 打开 `VcxprojParser.csproj` 文件
2. 选择 Release 配置
3. 点击"生成"菜单 → "生成解决方案"

## 2. 如何使用

### 环境准备

**重要：** 使用前必须设置 Visual Studio 安装路径环境变量

```cmd
set VSINSTALLDIR=C:\Program Files\Microsoft Visual Studio\2022\Community\
```

或使用 PowerShell:
```powershell
$env:VSINSTALLDIR = "C:\Program Files\Microsoft Visual Studio\2022\Community\"
```

### 基本用法

```bash
vcxproj-parser <子命令> [选项] <vcxproj文件路径>
```

### 子命令说明

#### 2.1 list-configs - 列出可用配置

显示项目中所有可用的配置组合（Debug/Release × Win32/x64）。

```bash
vcxproj-parser list-configs "C:\MyProject\MyApp.vcxproj"
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

#### 2.2 list-units - 列出编译单元

列出项目中所有的 C++ 源文件（.cpp, .c）。

```bash
vcxproj-parser list-units "C:\MyProject\MyApp.vcxproj" -p x64 -c Debug
```

**选项：**
- `-p, --platform`：指定平台（x64 或 Win32）
- `-c, --config`：指定配置（Debug 或 Release）
- `-s, --solution-path`：手动指定解决方案路径（可选）

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

#### 2.3 list-files - 列出所有引用文件

列出项目中引用的所有文件（包括头文件、资源文件等）。

```bash
vcxproj-parser list-files "C:\MyProject\MyApp.vcxproj" -p x64 -c Debug
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

#### 2.4 generate - 生成配置文件

生成 `compile_commands.json` 或 `.clangd` 配置文件。

**生成 compile_commands.json：**
```bash
vcxproj-parser generate "C:\MyProject\MyApp.vcxproj" ^
  --platform x64 ^
  --config Debug ^
  --format compile_commands ^
  --output compile_commands.json
```

**生成 .clangd 配置文件：**
```bash
vcxproj-parser generate "C:\MyProject\MyApp.vcxproj" ^
  --platform x64 ^
  --config Debug ^
  --format clangd ^
  --output .clangd
```

**选项：**
- `-p, --platform`：指定平台（x64 或 Win32）
- `-c, --config`：指定配置（Debug 或 Release）
- `-f, --format`：输出格式（compile_commands 或 clangd，默认 compile_commands）
- `-o, --output`：输出文件路径
- `--compiler`：指定编译器路径（可选，默认使用 MSVC cl.exe）

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

- `-v, --verbose`：显示详细输出信息
- `--strict`：严格模式，遇到错误立即退出
- `-h, --help`：显示帮助信息

### 使用示例

#### 示例 1：快速生成 compile_commands.json

```cmd
set VSINSTALLDIR=C:\Program Files\Microsoft Visual Studio\2022\Community\
cd C:\MyProject
vcxproj-parser generate MyApp.vcxproj -p x64 -c Debug -o compile_commands.json
```

#### 示例 2：为 clangd 生成配置

```cmd
set VSINSTALLDIR=C:\Program Files\Microsoft Visual Studio\2022\Community\
vcxproj-parser generate "C:\MyProject\MyApp.vcxproj" --format clangd --output .clangd
```

#### 示例 3：查看项目有哪些源文件

```cmd
set VSINSTALLDIR=C:\Program Files\Microsoft Visual Studio\2022\Community\
vcxproj-parser list-units "C:\MyProject\MyApp.vcxproj" -p x64 -c Debug > files.json
```

#### 示例 4：使用特定编译器（如 clang-cl）

```cmd
set VSINSTALLDIR=C:\Program Files\Microsoft Visual Studio\2022\Community\
vcxproj-parser generate "C:\MyProject\MyApp.vcxproj" ^
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

### 问题 1："未找到 Visual Studio 2022"

**错误信息：**
```
错误：未找到 Visual Studio 2022 或更高版本。
```

**解决方案：**
确保已安装 Visual Studio 2022 并包含"使用 C++ 的桌面开发"工作负载，然后设置环境变量：

```cmd
set VSINSTALLDIR=C:\Program Files\Microsoft Visual Studio\2022\Community\
```

### 问题 2："Could not load file or assembly 'Microsoft.Build'"

**原因：** 使用了 .NET SDK 的 MSBuild，而不是 Visual Studio 的 MSBuild。

**解决方案：** 确保正确设置了 `VSINSTALLDIR` 环境变量。

### 问题 3：生成的 compile_commands.json 为空

**检查清单：**
1. 确认 vcxproj 文件路径正确
2. 确认指定的 platform 和 config 在项目中存在
3. 使用 `--verbose` 查看详细信息

### 问题 4：路径中包含空格

如果路径包含空格，请使用双引号包裹：

```bash
vcxproj-parser generate "C:\My Projects\App\App.vcxproj" --output "C:\Output\compile_commands.json"
```

## 5. 注意事项

1. **PCH 处理**：工具会自动移除所有预编译头（PCH）相关参数（/Yu, /Yc, /Fp），因为 clangd 不需要这些参数。

2. **路径格式**：所有路径都使用正斜杠（/），这是为了与 clangd 更好地兼容。

3. **外部依赖**：如果项目依赖外部库（如 DirectXTK），确保这些库的头文件路径在 vcxproj 中正确配置。

4. **多项目解决方案**：如果 vcxproj 是解决方案的一部分，工具会自动查找同目录下的 .sln 文件。如果找不到，可以使用 `--solution-path` 手动指定。

## 6. 支持

如有问题，请检查：
1. Visual Studio 2022 是否正确安装
2. `VSINSTALLDIR` 环境变量是否正确设置
3. vcxproj 文件是否有效

## 附录：完整命令参考

```
vcxproj-parser <子命令> [选项] <vcxproj文件>

子命令：
  list-configs    列出所有可用的配置组合
  list-units     列出所有编译单元（ClCompile）
  list-files     列出所有被引用的文件
  generate       生成配置文件（compile_commands.json 或 .clangd）

全局选项：
  -v, --verbose      详细输出
  --strict           严格模式，遇错即停
  -h, --help         显示帮助

generate 选项：
  -p, --platform     指定平台（x64, Win32）
  -c, --config       指定配置（Debug, Release）
  -f, --format       输出格式（compile_commands, clangd）
  -o, --output       输出文件路径
  --compiler         指定编译器路径
```

---

*版本：1.0*
*最后更新：2026-03-29*
