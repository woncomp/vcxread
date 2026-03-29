using CommandLine;
using Microsoft.Build.Locator;
using VcxprojParser.Commands;

namespace VcxprojParser;

class Program
{
    static int Main(string[] args)
    {
        // Check for verbose parameter (before parsing arguments)
        bool verbose = args.Contains("-v") || args.Contains("--verbose");
        
        // Register MSBuild locator
        if (!MSBuildLocator.IsRegistered)
        {
            try
            {
                // Check if VS path is manually specified via environment variable
                var vsPath = Environment.GetEnvironmentVariable("VSINSTALLDIR")?.Trim();
                if (!string.IsNullOrEmpty(vsPath) && Directory.Exists(vsPath))
                {
                    if (verbose) Console.WriteLine($"Using VS path from environment variable: {vsPath}");
                    // Try to register MSBuild from this path
                    var msbuildPath = Path.Combine(vsPath, "MSBuild", "Current", "Bin", "MSBuild.exe");
                    if (File.Exists(msbuildPath))
                    {
                        // Use RegisterMSBuildPath to register the specified path
                        MSBuildLocator.RegisterMSBuildPath(Path.GetDirectoryName(msbuildPath)!);
                    }
                    else
                    {
                        if (verbose) Console.WriteLine($"Error: MSBuild not found in {vsPath}");
                        return 1;
                    }
                }
                else
                {
                    // Try to automatically find Visual Studio
                    var instance = TryFindVisualStudio();
                    bool vsFound = false;
                    
                    if (instance == null)
                    {
                        // Auto-detection failed, try common installation paths
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
                                    if (verbose) Console.WriteLine($"Found VS2022 from common path: {path}");
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
                        Console.WriteLine("Error: Visual Studio 2022 or higher not found.");
                        Console.WriteLine("");
                        Console.WriteLine("Note: vcxproj files require Visual Studio C++ toolset, .NET SDK alone is not sufficient.");
                        Console.WriteLine("Please ensure Visual Studio 2022 is installed with the 'Desktop development with C++' workload.");
                        Console.WriteLine("");
                        Console.WriteLine("If VS2022 is installed but not detected, you can:");
                        Console.WriteLine("1. Set the VSINSTALLDIR environment variable to point to your VS installation directory, e.g.:");
                        Console.WriteLine(@"   set VSINSTALLDIR=C:\Program Files\Microsoft Visual Studio\2022\Community\");
                        Console.WriteLine("");
                        Console.WriteLine("2. Or re-run the Visual Studio installer and ensure 'Desktop development with C++' workload is installed");
                        return 1;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: Unable to register MSBuild - {ex.Message}");
                Console.WriteLine($"Detailed error: {ex}");
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
            
            // Filter VS2022+ (version 17+) and name contains "Visual Studio"
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

[Verb("list-configs", HelpText = "List all available configuration combinations")]
public class ListConfigsOptions
{
    [Value(0, Required = true, HelpText = "Path to the vcxproj file")]
    public string Project { get; set; } = "";
    
    [Option('v', "verbose", HelpText = "Enable verbose output")]
    public bool Verbose { get; set; }
    
    [Option("strict", HelpText = "Strict mode, stop on error")]
    public bool Strict { get; set; }
}

[Verb("list-units", HelpText = "List all compilation units (ClCompile)")]
public class ListUnitsOptions
{
    [Value(0, Required = true, HelpText = "Path to the vcxproj file")]
    public string Project { get; set; } = "";
    
    [Option('p', "platform", HelpText = "Specify platform (e.g., x64, Win32)")]
    public string? Platform { get; set; }
    
    [Option('c', "config", HelpText = "Specify configuration (e.g., Debug, Release)")]
    public string? Config { get; set; }
    
    [Option('s', "solution-path", HelpText = "Manually specify solution path")]
    public string? SolutionPath { get; set; }
    
    [Option('v', "verbose", HelpText = "Enable verbose output")]
    public bool Verbose { get; set; }
    
    [Option("strict", HelpText = "Strict mode, stop on error")]
    public bool Strict { get; set; }
}

[Verb("list-files", HelpText = "List all referenced files")]
public class ListFilesOptions
{
    [Value(0, Required = true, HelpText = "Path to the vcxproj file")]
    public string Project { get; set; } = "";
    
    [Option('p', "platform", HelpText = "Specify platform (e.g., x64, Win32)")]
    public string? Platform { get; set; }
    
    [Option('c', "config", HelpText = "Specify configuration (e.g., Debug, Release)")]
    public string? Config { get; set; }
    
    [Option('s', "solution-path", HelpText = "Manually specify solution path")]
    public string? SolutionPath { get; set; }
    
    [Option('v', "verbose", HelpText = "Enable verbose output")]
    public bool Verbose { get; set; }
    
    [Option("strict", HelpText = "Strict mode, stop on error")]
    public bool Strict { get; set; }
}

[Verb("generate", HelpText = "Generate configuration file")]
public class GenerateOptions
{
    [Value(0, Required = true, HelpText = "Path to the vcxproj file")]
    public string Project { get; set; } = "";
    
    [Option('p', "platform", HelpText = "Specify platform (e.g., x64, Win32)")]
    public string? Platform { get; set; }
    
    [Option('c', "config", HelpText = "Specify configuration (e.g., Debug, Release)")]
    public string? Config { get; set; }
    
    [Option('s', "solution-path", HelpText = "Manually specify solution path")]
    public string? SolutionPath { get; set; }
    
    [Option('v', "verbose", HelpText = "Enable verbose output")]
    public bool Verbose { get; set; }
    
    [Option("strict", HelpText = "Strict mode, stop on error")]
    public bool Strict { get; set; }
    
    [Option('f', "format", Default = "compile_commands", HelpText = "Output format (compile_commands or clangd)")]
    public string Format { get; set; } = "compile_commands";
    
    [Option('o', "output", HelpText = "Output path")]
    public string? Output { get; set; }
    
    [Option("compiler", HelpText = "Specify compiler path")]
    public string? Compiler { get; set; }
}
