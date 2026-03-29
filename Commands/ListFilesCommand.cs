using System.Text.Json;
using VcxprojParser.Core;

namespace VcxprojParser.Commands;

public static class ListFilesCommand
{
    public static int Run(ListFilesOptions opts)
    {
        try
        {
            var analyzer = new VcxprojAnalyzer(opts.Project, opts.Verbose, opts.Strict);
            analyzer.LoadProject(opts.Config, opts.Platform, opts.SolutionPath);
            
            var files = analyzer.GetAllReferencedFiles();
            
            // Output JSON
            var options = new JsonSerializerOptions { WriteIndented = true };
            var output = files.Select(f => new 
            {
                file = f.File,
                type = f.Type,
                excluded = f.Excluded
            });
            
            Console.WriteLine(JsonSerializer.Serialize(output, options));
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            if (opts.Verbose) Console.WriteLine(ex.StackTrace);
            return opts.Strict ? 1 : 0;
        }
    }
}
