using System.Text.Json;
using VcxprojParser.Core;

namespace VcxprojParser.Commands;

public static class ListConfigsCommand
{
    public static int Run(ListConfigsOptions opts)
    {
        try
        {
            var analyzer = new VcxprojAnalyzer(opts.Project, opts.Verbose, opts.Strict);
            analyzer.LoadProjectWithoutConfig();
            
            var configs = analyzer.GetAvailableConfigurations();
            
            // 输出 JSON
            Console.WriteLine("{");
            Console.WriteLine($"  \"default\": {{ \"config\": \"{configs.FirstOrDefault()?.Config ?? "Debug"}\", \"platform\": \"{configs.FirstOrDefault()?.Platform ?? "Win32"}\" }},");
            Console.WriteLine("  \"available\": [");
            
            for (int i = 0; i < configs.Count; i++)
            {
                var config = configs[i];
                var comma = i < configs.Count - 1 ? "," : "";
                Console.WriteLine($"    {{ \"config\": \"{config.Config}\", \"platform\": \"{config.Platform}\" }}{comma}");
            }
            
            Console.WriteLine("  ]");
            Console.WriteLine("}");
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"错误: {ex.Message}");
            if (opts.Verbose) Console.WriteLine(ex.StackTrace);
            return opts.Strict ? 1 : 0;
        }
    }
}
