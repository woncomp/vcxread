using System.Text.Encodings.Web;
using System.Text.Json;
using VcxprojParser.Core;

namespace VcxprojParser.Commands;

public static class GenerateCommand
{
    public static int Run(GenerateOptions opts)
    {
        try
        {
            if (opts.Format != "compile_commands" && opts.Format != "clangd")
            {
                Console.WriteLine("Error: --format must be 'compile_commands' or 'clangd'");
                return 1;
            }
            
            var analyzer = new VcxprojAnalyzer(opts.Project, opts.Verbose, opts.Strict);
            analyzer.LoadProject(opts.Config, opts.Platform, opts.SolutionPath);
            
            // Determine output path
            string outputPath;
            if (string.IsNullOrEmpty(opts.Output))
            {
                outputPath = opts.Format == "compile_commands" ? "compile_commands.json" : ".clangd";
            }
            else
            {
                // Smart detection: if path exists and is a directory, use default filename
                if (Directory.Exists(opts.Output))
                {
                    var fileName = opts.Format == "compile_commands" ? "compile_commands.json" : ".clangd";
                    outputPath = Path.Combine(opts.Output, fileName);
                }
                else
                {
                    outputPath = opts.Output;
                }
            }
            
            if (opts.Format == "compile_commands")
            {
                GenerateCompileCommands(analyzer, outputPath, opts.Compiler);
            }
            else
            {
                GenerateClangdConfig(analyzer, outputPath, opts.Compiler);
            }
            
            Console.WriteLine($"Generated: {outputPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            if (opts.Verbose) Console.WriteLine(ex.StackTrace);
            return opts.Strict ? 1 : 0;
        }
    }
    
    private static void GenerateCompileCommands(VcxprojAnalyzer analyzer, string outputPath, string? compilerPath)
    {
        var commands = analyzer.GetCompileCommands(compilerPath);
        
        var output = commands.Select(c => new 
        {
            directory = c.Directory,
            file = c.File,
            command = c.Command
        });
        
        var options = new JsonSerializerOptions 
        { 
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        var json = JsonSerializer.Serialize(output, options);
        
        File.WriteAllText(outputPath, json);
    }
    
    private static void GenerateClangdConfig(VcxprojAnalyzer analyzer, string outputPath, string? compilerPath)
    {
        var commands = analyzer.GetCompileCommands(compilerPath);
        
        if (commands.Count == 0)
        {
            throw new InvalidOperationException("No compilation units found");
        }
        
        // Analyze all commands and extract common arguments
        var allArgs = new HashSet<string>();
        foreach (var cmd in commands)
        {
            // Parse command line arguments
            var args = ParseCommandLine(cmd.Command);
            foreach (var arg in args.Where(a => a.StartsWith("/I") || a.StartsWith("/D") || a.StartsWith("/std:")))
            {
                allArgs.Add(arg);
            }
        }
        
        // Generate YAML format .clangd
        var lines = new List<string>();
        lines.Add("CompileFlags:");
        lines.Add("  Add:");
        
        foreach (var arg in allArgs.OrderBy(a => a))
        {
            lines.Add($"    - {arg}");
        }
        
        File.WriteAllLines(outputPath, lines);
    }
    
    private static List<string> ParseCommandLine(string command)
    {
        var args = new List<string>();
        var currentArg = "";
        var inQuotes = false;
        
        for (int i = 0; i < command.Length; i++)
        {
            var c = command[i];
            
            if (c == '"')
            {
                inQuotes = !inQuotes;
                currentArg += c;
            }
            else if (c == ' ' && !inQuotes)
            {
                if (!string.IsNullOrWhiteSpace(currentArg))
                {
                    args.Add(currentArg);
                    currentArg = "";
                }
            }
            else
            {
                currentArg += c;
            }
        }
        
        if (!string.IsNullOrWhiteSpace(currentArg))
        {
            args.Add(currentArg);
        }
        
        return args;
    }
}
