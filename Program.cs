using CommandLine;
using Microsoft.Build.Locator;
using VcxprojParser.Commands;

namespace VcxprojParser;

class Program
{
    static int Main(string[] args)
    {
        // 检查是否有 verbose 参数（在解析参数前检查）
        bool verbose = args.Contains("-v") || args.Contains("--verbose");
        
        // 注册 MSBuild 定位器
        if (!MSBuildLocator.IsRegistered)
        {
            try
            {
                // 检查环境变量是否手动指定了 VS 路径
                var vsPath = Environment.GetEnvironmentVariable("VSINSTALLDIR");
                if (!string.IsNullOrEmpty(vsPath) && Directory.Exists(vsPath))
                {
                    if (verbose) Console.WriteLine($"使用环境变量指定的 VS 路径: {vsPath}");
                    // 尝试从该路径注册 MSBuild
                    var msbuildPath = Path.Combine(vsPath, "MSBuild", "Current", "Bin", "MSBuild.exe");
                    if (File.Exists(msbuildPath))
                    {
                        // 使用 RegisterMSBuildPath 注册指定路径
                        MSBuildLocator.RegisterMSBuildPath(Path.GetDirectoryName(msbuildPath)!);
                    }
                    else
                    {
                        if (verbose) Console.WriteLine($"错误：在 {vsPath} 中未找到 MSBuild");
                        return 1;
                    }
                }
                else
                {
                    // 尝试自动查找 Visual Studio
                    var instance = TryFindVisualStudio();
                    bool vsFound = false;
                    
                    if (instance == null)
                    {
                        // 自动查找失败，尝试常见安装路径
                        var commonPaths = new[]
                        {
                            @"C:\Program Files\Microsoft Visual Studio\2022\Community",
                            @"C:\Program Files\Microsoft Visual Studio\2022\Professional",
                            @"C:\Program Files\Microsoft Visual Studio\2022\Enterprise",
                            @"C:\Program Files (x86)\Microsoft Visual Studio\2022\Community",
                            @"C:\Program Files (x86)\Microsoft Visual Studio\2022\Professional",
                            @"C:\Program Files (x86)\Microsoft Visual Studio\2022\Enterprise"
                        };
                        
                        foreach (var path in commonPaths)
                        {
                            if (Directory.Exists(path))
                            {
                                var msbuildPath = Path.Combine(path, "MSBuild", "Current", "Bin");
                                if (Directory.Exists(msbuildPath))
                                {
                                    if (verbose) Console.WriteLine($"从常见路径找到 VS2022: {path}");
                                    MSBuildLocator.RegisterMSBuildPath(msbuildPath);
                                    vsFound = true;
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        MSBuildLocator.RegisterInstance(instance);
                        vsFound = true;
                    }
                    
                    if (!vsFound)
                    {
                        Console.WriteLine("错误：未找到 Visual Studio 2022 或更高版本。");
                        Console.WriteLine("");
                        Console.WriteLine("注意：vcxproj 文件需要 Visual Studio C++ 工具集，不能仅使用 .NET SDK。");
                        Console.WriteLine("请确保已安装 Visual Studio 2022 并包含 '使用 C++ 的桌面开发' 工作负载。");
                        Console.WriteLine("");
                        Console.WriteLine("如果 VS2022 已安装但未被检测到，您可以：");
                        Console.WriteLine("1. 设置环境变量 VSINSTALLDIR 指向 VS 安装目录，例如：");
                        Console.WriteLine(@"   set VSINSTALLDIR=C:\Program Files\Microsoft Visual Studio\2022\Community\");
                        Console.WriteLine("");
                        Console.WriteLine("2. 或重新运行 Visual Studio 安装程序，确保安装了 '使用 C++ 的桌面开发' 工作负载");
                        return 1;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误：无法注册 MSBuild - {ex.Message}");
                Console.WriteLine($"详细错误：{ex}");
                return 1;
            }
        }

        var parser = new Parser(with => with.HelpWriter = Console.Out);
        
        return parser.ParseArguments<
            ListConfigsOptions,
            ListUnitsOptions,
            ListFilesOptions,
            GenerateOptions>(args)
            .MapResult(
                (ListConfigsOptions opts) => ListConfigsCommand.Run(opts),
                (ListUnitsOptions opts) => ListUnitsCommand.Run(opts),
                (ListFilesOptions opts) => ListFilesCommand.Run(opts),
                (GenerateOptions opts) => GenerateCommand.Run(opts),
                errs => 1
            );
    }
    
    static VisualStudioInstance? TryFindVisualStudio()
    {
        try
        {
            var instances = MSBuildLocator.QueryVisualStudioInstances().ToList();
            
            // 筛选 VS2022+ (版本 17+) 且名称包含 "Visual Studio"
            return instances
                .Where(vs => vs.Version.Major >= 17 && vs.Name.Contains("Visual Studio"))
                .OrderByDescending(vs => vs.Version)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }
}

[Verb("list-configs", HelpText = "列出所有可用的配置组合")]
public class ListConfigsOptions
{
    [Value(0, Required = true, HelpText = "vcxproj 文件路径")]
    public string Project { get; set; } = "";
    
    [Option('v', "verbose", HelpText = "详细输出")]
    public bool Verbose { get; set; }
    
    [Option("strict", HelpText = "严格模式，遇错即停")]
    public bool Strict { get; set; }
}

[Verb("list-units", HelpText = "列出所有编译单元（ClCompile）")]
public class ListUnitsOptions
{
    [Value(0, Required = true, HelpText = "vcxproj 文件路径")]
    public string Project { get; set; } = "";
    
    [Option('p', "platform", HelpText = "指定平台（如 x64, Win32）")]
    public string? Platform { get; set; }
    
    [Option('c', "config", HelpText = "指定配置（如 Debug, Release）")]
    public string? Config { get; set; }
    
    [Option('s', "solution-path", HelpText = "手动指定解决方案路径")]
    public string? SolutionPath { get; set; }
    
    [Option('v', "verbose", HelpText = "详细输出")]
    public bool Verbose { get; set; }
    
    [Option("strict", HelpText = "严格模式，遇错即停")]
    public bool Strict { get; set; }
}

[Verb("list-files", HelpText = "列出所有被引用的文件")]
public class ListFilesOptions
{
    [Value(0, Required = true, HelpText = "vcxproj 文件路径")]
    public string Project { get; set; } = "";
    
    [Option('p', "platform", HelpText = "指定平台（如 x64, Win32）")]
    public string? Platform { get; set; }
    
    [Option('c', "config", HelpText = "指定配置（如 Debug, Release）")]
    public string? Config { get; set; }
    
    [Option('s', "solution-path", HelpText = "手动指定解决方案路径")]
    public string? SolutionPath { get; set; }
    
    [Option('v', "verbose", HelpText = "详细输出")]
    public bool Verbose { get; set; }
    
    [Option("strict", HelpText = "严格模式，遇错即停")]
    public bool Strict { get; set; }
}

[Verb("generate", HelpText = "生成配置文件")]
public class GenerateOptions
{
    [Value(0, Required = true, HelpText = "vcxproj 文件路径")]
    public string Project { get; set; } = "";
    
    [Option('p', "platform", HelpText = "指定平台（如 x64, Win32）")]
    public string? Platform { get; set; }
    
    [Option('c', "config", HelpText = "指定配置（如 Debug, Release）")]
    public string? Config { get; set; }
    
    [Option('s', "solution-path", HelpText = "手动指定解决方案路径")]
    public string? SolutionPath { get; set; }
    
    [Option('v', "verbose", HelpText = "详细输出")]
    public bool Verbose { get; set; }
    
    [Option("strict", HelpText = "严格模式，遇错即停")]
    public bool Strict { get; set; }
    
    [Option('f', "format", Default = "compile_commands", HelpText = "输出格式 (compile_commands 或 clangd)")]
    public string Format { get; set; } = "compile_commands";
    
    [Option('o', "output", HelpText = "输出路径")]
    public string? Output { get; set; }
    
    [Option("compiler", HelpText = "指定编译器路径")]
    public string? Compiler { get; set; }
}
