using Microsoft.Build.Evaluation;
using System.Text.RegularExpressions;

namespace VcxprojParser.Core;

public class VcxprojAnalyzer
{
    private readonly string _vcxprojPath;
    private readonly bool _verbose;
    private readonly bool _strict;
    private Project? _project;
    
    public VcxprojAnalyzer(string vcxprojPath, bool verbose, bool strict)
    {
        _vcxprojPath = Path.GetFullPath(vcxprojPath);
        _verbose = verbose;
        _strict = strict;
        
        if (!File.Exists(_vcxprojPath))
        {
            throw new FileNotFoundException($"找不到 vcxproj 文件: {_vcxprojPath}");
        }
    }
    
    /// <summary>
    /// 加载项目但不指定配置（用于 list-configs）
    /// </summary>
    public void LoadProjectWithoutConfig()
    {
        // 使用空全局属性加载，只获取配置列表
        _project = new Project(_vcxprojPath, null, null);
    }
    
    /// <summary>
    /// 加载项目并指定配置
    /// </summary>
    public void LoadProject(string? config = null, string? platform = null, string? solutionPath = null)
    {
        var globalProperties = new Dictionary<string, string>();
        
        // 查找并验证 solution
        var solutionDir = FindAndValidateSolution(solutionPath);
        
        // 设置全局属性
        if (!string.IsNullOrEmpty(config))
        {
            globalProperties["Configuration"] = config;
        }
        
        if (!string.IsNullOrEmpty(platform))
        {
            globalProperties["Platform"] = platform;
        }
        
        if (!string.IsNullOrEmpty(solutionDir))
        {
            globalProperties["SolutionDir"] = solutionDir.Replace('\\', '/');
            
            // 查找 solution 文件路径
            var solutionFile = Directory.GetFiles(Path.GetDirectoryName(solutionDir.TrimEnd('/'))!, "*.sln").FirstOrDefault();
            if (!string.IsNullOrEmpty(solutionFile))
            {
                globalProperties["SolutionPath"] = solutionFile.Replace('\\', '/');
                globalProperties["SolutionName"] = Path.GetFileNameWithoutExtension(solutionFile);
            }
        }
        
        if (_verbose)
        {
            Console.WriteLine("全局属性:");
            foreach (var prop in globalProperties)
            {
                Console.WriteLine($"  {prop.Key} = {prop.Value}");
            }
        }
        
        _project = new Project(_vcxprojPath, globalProperties, null);
    }
    
    /// <summary>
    /// 查找并验证 solution
    /// </summary>
    private string FindAndValidateSolution(string? manualSolutionPath)
    {
        string? solutionFile = null;
        
        // 如果手动指定了 solution 路径
        if (!string.IsNullOrEmpty(manualSolutionPath))
        {
            if (!File.Exists(manualSolutionPath))
            {
                throw new FileNotFoundException($"找不到指定的 solution 文件: {manualSolutionPath}");
            }
            solutionFile = Path.GetFullPath(manualSolutionPath);
        }
        else
        {
            // 自动向上查找 .sln 文件
            var currentDir = Path.GetDirectoryName(_vcxprojPath);
            for (int i = 0; i < 3 && currentDir != null; i++)
            {
                var slnFiles = Directory.GetFiles(currentDir, "*.sln");
                if (slnFiles.Length > 0)
                {
                    solutionFile = slnFiles[0]; // 取第一个
                    if (slnFiles.Length > 1 && _verbose)
                    {
                        Console.WriteLine($"警告: 发现多个 .sln 文件，使用: {Path.GetFileName(solutionFile)}");
                    }
                    break;
                }
                currentDir = Directory.GetParent(currentDir)?.FullName;
            }
        }
        
        if (string.IsNullOrEmpty(solutionFile))
        {
            throw new FileNotFoundException("未找到 .sln 文件。请使用 --solution-path 手动指定。");
        }
        
        // 验证 solution 是否包含此 vcxproj
        if (!ValidateSolutionContainsProject(solutionFile))
        {
            throw new InvalidOperationException($"Solution 文件不包含项目: {Path.GetFileName(_vcxprojPath)}");
        }
        
        if (_verbose)
        {
            Console.WriteLine($"使用 Solution: {solutionFile}");
        }
        
        return Path.GetDirectoryName(solutionFile)!.Replace('\\', '/') + "/";
    }
    
    /// <summary>
    /// 验证 solution 是否包含目标 vcxproj
    /// </summary>
    private bool ValidateSolutionContainsProject(string solutionFile)
    {
        try
        {
            var solutionContent = File.ReadAllText(solutionFile);
            var projectName = Path.GetFileName(_vcxprojPath);
            
            // 在 solution 文件中查找项目引用
            // 格式: Project("{GUID}") = "Name", "Relative\Path\To\Project.vcxproj", "{ProjectGUID}"
            return solutionContent.Contains(projectName);
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// 获取所有可用配置
    /// </summary>
    public List<ConfigurationInfo> GetAvailableConfigurations()
    {
        if (_project == null)
        {
            throw new InvalidOperationException("项目未加载");
        }
        
        var configs = new List<ConfigurationInfo>();
        
        // 从 ProjectConfiguration 项获取配置列表
        foreach (ProjectItem item in _project.GetItems("ProjectConfiguration"))
        {
            var include = item.EvaluatedInclude;
            var parts = include.Split('|');
            if (parts.Length == 2)
            {
                configs.Add(new ConfigurationInfo(parts[0], parts[1]));
            }
        }
        
        return configs;
    }
    
    /// <summary>
    /// 获取编译单元（ClCompile）
    /// </summary>
    public List<CompileUnit> GetCompilationUnits()
    {
        if (_project == null)
        {
            throw new InvalidOperationException("项目未加载");
        }
        
        var units = new List<CompileUnit>();
        var projectDir = Path.GetDirectoryName(_vcxprojPath)!;
        
        foreach (ProjectItem item in _project.GetItems("ClCompile"))
        {
            var filePath = item.EvaluatedInclude;
            var fullPath = Path.GetFullPath(Path.Combine(projectDir, filePath));
            
            // 检查是否被排除
            var excludedFromBuild = item.GetMetadataValue("ExcludedFromBuild");
            var isExcluded = excludedFromBuild.Equals("true", StringComparison.OrdinalIgnoreCase);
            
            units.Add(new CompileUnit
            {
                File = fullPath.Replace('\\', '/'),
                Type = "ClCompile",
                Excluded = isExcluded
            });
        }
        
        return units;
    }
    
    /// <summary>
    /// 获取所有引用文件
    /// </summary>
    public List<ReferencedFile> GetAllReferencedFiles()
    {
        if (_project == null)
        {
            throw new InvalidOperationException("项目未加载");
        }
        
        var files = new List<ReferencedFile>();
        var projectDir = Path.GetDirectoryName(_vcxprojPath)!;
        
        // 获取所有 Item 类型
        var itemTypes = new[] { "ClCompile", "ClInclude", "ResourceCompile", "None", "Text", "Image", "Manifest", "FxCompile" };
        
        foreach (var itemType in itemTypes)
        {
            foreach (ProjectItem item in _project.GetItems(itemType))
            {
                var filePath = item.EvaluatedInclude;
                var fullPath = Path.GetFullPath(Path.Combine(projectDir, filePath));
                
                // 检查是否被排除
                var excludedFromBuild = item.GetMetadataValue("ExcludedFromBuild");
                var isExcluded = excludedFromBuild.Equals("true", StringComparison.OrdinalIgnoreCase);
                
                // 映射类型名称
                var displayType = itemType switch
                {
                    "FxCompile" => "hlsl",
                    _ => itemType
                };
                
                files.Add(new ReferencedFile
                {
                    File = fullPath.Replace('\\', '/'),
                    Type = displayType,
                    Excluded = isExcluded
                });
            }
        }
        
        return files;
    }
    
    /// <summary>
    /// 获取编译命令（用于 generate）
    /// </summary>
    public List<CompileCommand> GetCompileCommands(string? compilerPath = null)
    {
        if (_project == null)
        {
            throw new InvalidOperationException("项目未加载");
        }
        
        var commands = new List<CompileCommand>();
        var projectDir = Path.GetDirectoryName(_vcxprojPath)!;
        
        // 获取编译器路径
        if (string.IsNullOrEmpty(compilerPath))
        {
            // 从项目属性获取或使用默认
            compilerPath = _project.GetPropertyValue("VCExecutablePath");
            if (string.IsNullOrEmpty(compilerPath))
            {
                compilerPath = "cl.exe";
            }
        }
        
        // 获取通用编译参数
        var includeDirs = _project.GetPropertyValue("AdditionalIncludeDirectories");
        var preprocessorDefs = _project.GetPropertyValue("PreprocessorDefinitions");
        var additionalOptions = _project.GetPropertyValue("AdditionalOptions");
        var cppStandard = _project.GetPropertyValue("LanguageStandard");
        
        // 构建基本参数
        var baseArgs = new List<string>();
        
        // 包含目录
        if (!string.IsNullOrEmpty(includeDirs))
        {
            foreach (var inc in includeDirs.Split(';'))
            {
                if (!string.IsNullOrWhiteSpace(inc))
                {
                    var expandedInc = inc.Trim();
                    if (!expandedInc.Contains(':'))
                    {
                        // 相对路径转为绝对路径
                        expandedInc = Path.GetFullPath(Path.Combine(projectDir, expandedInc));
                    }
                    baseArgs.Add($"/I\"{expandedInc.Replace('\\', '/')}\"");
                }
            }
        }
        
        // 预处理器定义
        if (!string.IsNullOrEmpty(preprocessorDefs))
        {
            foreach (var def in preprocessorDefs.Split(';'))
            {
                if (!string.IsNullOrWhiteSpace(def))
                {
                    baseArgs.Add($"/D{def.Trim()}");
                }
            }
        }
        
        // C++ 标准
        if (!string.IsNullOrEmpty(cppStandard))
        {
            if (cppStandard.StartsWith("stdcpp"))
            {
                baseArgs.Add($"/std:c++{cppStandard.Substring(6)}");
            }
        }
        
        // 遍历编译单元
        foreach (ProjectItem item in _project.GetItems("ClCompile"))
        {
            var filePath = item.EvaluatedInclude;
            var fullPath = Path.GetFullPath(Path.Combine(projectDir, filePath));
            
            // 检查是否被排除
            var excludedFromBuild = item.GetMetadataValue("ExcludedFromBuild");
            if (excludedFromBuild.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            
            var args = new List<string>(baseArgs);
            
            // 移除 PCH 参数
            // args.RemoveAll(a => a.StartsWith("/Yu") || a.StartsWith("/Yc") || a.StartsWith("/Fp"));
            
            // 添加文件
            args.Add($"\"{fullPath.Replace('\\', '/')}\"");
            
            commands.Add(new CompileCommand
            {
                Directory = projectDir.Replace('\\', '/'),
                File = fullPath.Replace('\\', '/'),
                Command = $"\"{compilerPath}\" {string.Join(" ", args)}"
            });
        }
        
        return commands;
    }
}

public class ConfigurationInfo
{
    public string Config { get; }
    public string Platform { get; }
    
    public ConfigurationInfo(string config, string platform)
    {
        Config = config;
        Platform = platform;
    }
}

public class CompileUnit
{
    public string File { get; set; } = "";
    public string Type { get; set; } = "";
    public bool Excluded { get; set; }
}

public class ReferencedFile
{
    public string File { get; set; } = "";
    public string Type { get; set; } = "";
    public bool Excluded { get; set; }
}

public class CompileCommand
{
    public string Directory { get; set; } = "";
    public string File { get; set; } = "";
    public string Command { get; set; } = "";
}
