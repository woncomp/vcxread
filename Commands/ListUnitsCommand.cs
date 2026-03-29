using System.Text.Json;
using VcxprojParser.Core;

namespace VcxprojParser.Commands;

public static class ListUnitsCommand
{
    public static int Run(ListUnitsOptions opts)
    {
        try
        {
            var analyzer = new VcxprojAnalyzer(opts.Project, opts.Verbose, opts.Strict);
            analyzer.LoadProject(opts.Config, opts.Platform, opts.SolutionPath);
            
            var units = analyzer.GetCompilationUnits();
            
            // Output JSON
            var options = new JsonSerializerOptions { WriteIndented = true };
            var output = units.Select(u => new 
            {
                file = u.File,
                type = u.Type,
                excluded = u.Excluded
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
