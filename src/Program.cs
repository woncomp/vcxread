using CommandLine;
using Microsoft.Build.Locator;
using VcxprojParser.Commands;

namespace VcxprojParser;

class Program
{
    static int Main(string[] args)
    {
        // 注册 MSBuild 定位器
        if (!MSBuildLocator.IsRegistered)
        {
            try
            {
                var instance = MSBuildLocator.QueryVisualStudioInstances()
                    .Where(vs => vs.Version.Major >= 17)  // VS2022
                    .OrderBy(vs => vs.Version)
                    .FirstOrDefault();
                
                if (instance == null)
                {
                    Console.WriteLine("错误：未找到 Visual Studio 2022 或更高版本。");
                    return 1;
                }
                
                MSBuildLocator.RegisterInstance(instance);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误：无法注册 MSBuild - {ex.Message}");
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
public class ListFilesOptions : ListUnitsOptions
{
}

[Verb("generate", HelpText = "生成配置文件")]
public class GenerateOptions : ListUnitsOptions
{
    [Option('f', "format", Default = "compile_commands", HelpText = "输出格式 (compile_commands 或 clangd)")]
    public string Format { get; set; } = "compile_commands";
    
    [Option('o', "output", HelpText = "输出路径")]
    public string? Output { get; set; }
    
    [Option("compiler", HelpText = "指定编译器路径")]
    public string? Compiler { get; set; }
}
