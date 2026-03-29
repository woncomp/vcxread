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
            throw new FileNotFoundException($"vcxproj file not found: {_vcxprojPath}");
        }
    }
    
    /// <summary>
    /// Load project without specifying configuration (used for list-configs)
    /// </summary>
    public void LoadProjectWithoutConfig()
    {
        // Load with empty global properties to get configuration list only
        _project = new Project(_vcxprojPath, null, null);
    }
    
    /// <summary>
    /// Load project with specified configuration
    /// </summary>
    public void LoadProject(string? config = null, string? platform = null, string? solutionPath = null)
    {
        var globalProperties = new Dictionary<string, string>();
        
        // Find and validate solution
        var solutionDir = FindAndValidateSolution(solutionPath);
        
        // Set global properties
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
            
            // Find solution file path
            var solutionFile = Directory.GetFiles(Path.GetDirectoryName(solutionDir.TrimEnd('/'))!, "*.sln").FirstOrDefault();
            if (!string.IsNullOrEmpty(solutionFile))
            {
                globalProperties["SolutionPath"] = solutionFile.Replace('\\', '/');
                globalProperties["SolutionName"] = Path.GetFileNameWithoutExtension(solutionFile);
            }
        }
        
        if (_verbose)
        {
            Console.WriteLine("Global properties:");
            foreach (var prop in globalProperties)
            {
                Console.WriteLine($"  {prop.Key} = {prop.Value}");
            }
        }
        
        _project = new Project(_vcxprojPath, globalProperties, null);
    }
    
    /// <summary>
    /// Find and validate solution
    /// </summary>
    private string FindAndValidateSolution(string? manualSolutionPath)
    {
        string? solutionFile = null;
        
        // If solution path is manually specified
        if (!string.IsNullOrEmpty(manualSolutionPath))
        {
            if (!File.Exists(manualSolutionPath))
            {
                throw new FileNotFoundException($"Specified solution file not found: {manualSolutionPath}");
            }
            solutionFile = Path.GetFullPath(manualSolutionPath);
        }
        else
        {
            // Auto-search upward for .sln file
            var currentDir = Path.GetDirectoryName(_vcxprojPath);
            for (int i = 0; i < 3 && currentDir != null; i++)
            {
                var slnFiles = Directory.GetFiles(currentDir, "*.sln");
                if (slnFiles.Length > 0)
                {
                    solutionFile = slnFiles[0]; // Use the first one
                    if (slnFiles.Length > 1 && _verbose)
                    {
                        Console.WriteLine($"Warning: Multiple .sln files found, using: {Path.GetFileName(solutionFile)}");
                    }
                    break;
                }
                currentDir = Directory.GetParent(currentDir)?.FullName;
            }
        }
        
        if (string.IsNullOrEmpty(solutionFile))
        {
            throw new FileNotFoundException(".sln file not found. Please specify manually using --solution-path.");
        }
        
        // Validate if solution contains this vcxproj
        if (!ValidateSolutionContainsProject(solutionFile))
        {
            throw new InvalidOperationException($"Solution file does not contain project: {Path.GetFileName(_vcxprojPath)}");
        }
        
        if (_verbose)
        {
            Console.WriteLine($"Using Solution: {solutionFile}");
        }
        
        return Path.GetDirectoryName(solutionFile)!.Replace('\\', '/') + "/";
    }
    
    /// <summary>
    /// Validate if solution contains target vcxproj
    /// </summary>
    private bool ValidateSolutionContainsProject(string solutionFile)
    {
        try
        {
            var solutionContent = File.ReadAllText(solutionFile);
            var projectName = Path.GetFileName(_vcxprojPath);
            
            // Search for project reference in solution file
            // Format: Project("{GUID}") = "Name", "Relative\Path\To\Project.vcxproj", "{ProjectGUID}"
            return solutionContent.Contains(projectName);
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Get all available configurations
    /// </summary>
    public List<ConfigurationInfo> GetAvailableConfigurations()
    {
        if (_project == null)
        {
            throw new InvalidOperationException("Project not loaded");
        }
        
        var configs = new List<ConfigurationInfo>();
        
        // Get configuration list from ProjectConfiguration items
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
    /// Get compilation units (ClCompile)
    /// </summary>
    public List<CompileUnit> GetCompilationUnits()
    {
        if (_project == null)
        {
            throw new InvalidOperationException("Project not loaded");
        }
        
        var units = new List<CompileUnit>();
        var projectDir = Path.GetDirectoryName(_vcxprojPath)!;
        
        foreach (ProjectItem item in _project.GetItems("ClCompile"))
        {
            var filePath = item.EvaluatedInclude;
            var fullPath = Path.GetFullPath(Path.Combine(projectDir, filePath));
            
            // Check if excluded from build
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
    /// Get all referenced files
    /// </summary>
    public List<ReferencedFile> GetAllReferencedFiles()
    {
        if (_project == null)
        {
            throw new InvalidOperationException("Project not loaded");
        }
        
        var files = new List<ReferencedFile>();
        var projectDir = Path.GetDirectoryName(_vcxprojPath)!;
        
        // Get all Item types
        var itemTypes = new[] { "ClCompile", "ClInclude", "ResourceCompile", "None", "Text", "Image", "Manifest", "FxCompile" };
        
        foreach (var itemType in itemTypes)
        {
            foreach (ProjectItem item in _project.GetItems(itemType))
            {
                var filePath = item.EvaluatedInclude;
                var fullPath = Path.GetFullPath(Path.Combine(projectDir, filePath));
                
                // Check if excluded from build
                var excludedFromBuild = item.GetMetadataValue("ExcludedFromBuild");
                var isExcluded = excludedFromBuild.Equals("true", StringComparison.OrdinalIgnoreCase);
                
                // Map type names
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
    /// Get compile commands (used for generate)
    /// </summary>
    public List<CompileCommand> GetCompileCommands(string? compilerPath = null)
    {
        if (_project == null)
        {
            throw new InvalidOperationException("Project not loaded");
        }
        
        var commands = new List<CompileCommand>();
        var projectDir = Path.GetDirectoryName(_vcxprojPath)!;
        
        // Get compiler path
        if (string.IsNullOrEmpty(compilerPath))
        {
            // Get from project properties or use default
            compilerPath = _project.GetPropertyValue("VCExecutablePath");
            if (string.IsNullOrEmpty(compilerPath))
            {
                compilerPath = "cl.exe";
            }
        }
        
        // Get common compile arguments (from project level)
        var baseArgs = BuildCompileArgs(_project, projectDir);
        
        // Iterate through compilation units
        foreach (ProjectItem item in _project.GetItems("ClCompile"))
        {
            var filePath = item.EvaluatedInclude;
            var fullPath = Path.GetFullPath(Path.Combine(projectDir, filePath));
            
            // Check if excluded from build
            var excludedFromBuild = item.GetMetadataValue("ExcludedFromBuild");
            if (excludedFromBuild.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            
            // Build arguments for each file (inherit base args + file-specific args)
            var args = new List<string>(baseArgs);
            
            // Add file-specific compile options
            AddFileSpecificArgs(item, args, projectDir);
            
            // Add file
            args.Add($"-c \"{fullPath.Replace('\\', '/')}\"");
            
            commands.Add(new CompileCommand
            {
                Directory = projectDir.Replace('\\', '/'),
                File = fullPath.Replace('\\', '/'),
                Command = $"\"{compilerPath}\" {string.Join(" ", args)}"
            });
        }
        
        return commands;
    }
    
    /// <summary>
    /// Build project-level compile arguments
    /// </summary>
    private List<string> BuildCompileArgs(Project project, string projectDir)
    {
        var args = new List<string>();
        
        // 1. Include directories - from IncludePath and AdditionalIncludeDirectories
        var includePath = project.GetPropertyValue("IncludePath");
        var additionalIncludeDirs = project.GetPropertyValue("AdditionalIncludeDirectories");
        
        // Merge and deduplicate
        var allIncludes = new HashSet<string>();
        
        if (!string.IsNullOrEmpty(includePath))
        {
            foreach (var inc in includePath.Split(';'))
            {
                if (!string.IsNullOrWhiteSpace(inc) && !inc.Contains("$(")) // Skip unexpanded macros
                {
                    allIncludes.Add(inc.Trim());
                }
            }
        }
        
        if (!string.IsNullOrEmpty(additionalIncludeDirs))
        {
            foreach (var inc in additionalIncludeDirs.Split(';'))
            {
                if (!string.IsNullOrWhiteSpace(inc) && !inc.Contains("$("))
                {
                    allIncludes.Add(inc.Trim());
                }
            }
        }
        
        foreach (var inc in allIncludes)
        {
            var expandedInc = inc;
            if (!expandedInc.Contains(':') && !expandedInc.StartsWith("/"))
            {
                // Convert relative path to absolute path
                expandedInc = Path.GetFullPath(Path.Combine(projectDir, expandedInc));
            }
            args.Add($"/I\"{expandedInc.Replace('\\', '/')}\"");
        }
        
        // 2. Preprocessor definitions
        var preprocessorDefs = project.GetPropertyValue("PreprocessorDefinitions");
        if (!string.IsNullOrEmpty(preprocessorDefs))
        {
            foreach (var def in preprocessorDefs.Split(';'))
            {
                if (!string.IsNullOrWhiteSpace(def) && !def.Contains("$("))
                {
                    args.Add($"/D{def.Trim()}");
                }
            }
        }
        
        // 3. C++ standard
        var cppStandard = project.GetPropertyValue("LanguageStandard");
        if (!string.IsNullOrEmpty(cppStandard))
        {
            if (cppStandard.StartsWith("stdcpp"))
            {
                args.Add($"/std:c++{cppStandard.Substring(6)}");
            }
        }
        
        // 4. Other important options
        var warningLevel = project.GetPropertyValue("WarningLevel");
        if (!string.IsNullOrEmpty(warningLevel))
        {
            args.Add($"/W{warningLevel}");
        }
        
        var treatWarningAsError = project.GetPropertyValue("TreatWarningAsError");
        if (treatWarningAsError.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            args.Add("/WX");
        }
        
        // 5. AdditionalOptions
        var additionalOptions = project.GetPropertyValue("AdditionalOptions");
        if (!string.IsNullOrEmpty(additionalOptions))
        {
            // Filter out PCH-related parameters
            var filteredOptions = additionalOptions
                .Split(' ')
                .Where(opt => !string.IsNullOrWhiteSpace(opt) 
                    && !opt.StartsWith("/Yu", StringComparison.OrdinalIgnoreCase)
                    && !opt.StartsWith("/Yc", StringComparison.OrdinalIgnoreCase)
                    && !opt.StartsWith("/Fp", StringComparison.OrdinalIgnoreCase));
            args.AddRange(filteredOptions);
        }
        
        return args;
    }
    
    /// <summary>
    /// Add file-specific compile arguments
    /// </summary>
    private void AddFileSpecificArgs(ProjectItem item, List<string> args, string projectDir)
    {
        // File-specific include directories
        var fileIncludes = item.GetMetadataValue("AdditionalIncludeDirectories");
        if (!string.IsNullOrEmpty(fileIncludes))
        {
            foreach (var inc in fileIncludes.Split(';'))
            {
                if (!string.IsNullOrWhiteSpace(inc) && !inc.Contains("$("))
                {
                    var expandedInc = inc.Trim();
                    if (!expandedInc.Contains(':'))
                    {
                        expandedInc = Path.GetFullPath(Path.Combine(projectDir, expandedInc));
                    }
                    args.Add($"/I\"{expandedInc.Replace('\\', '/')}\"");
                }
            }
        }
        
        // File-specific preprocessor definitions
        var fileDefs = item.GetMetadataValue("PreprocessorDefinitions");
        if (!string.IsNullOrEmpty(fileDefs))
        {
            foreach (var def in fileDefs.Split(';'))
            {
                if (!string.IsNullOrWhiteSpace(def) && !def.Contains("$("))
                {
                    args.Add($"/D{def.Trim()}");
                }
            }
        }
        
        // File-specific additional options
        var fileOptions = item.GetMetadataValue("AdditionalOptions");
        if (!string.IsNullOrEmpty(fileOptions))
        {
            var filteredOptions = fileOptions
                .Split(' ')
                .Where(opt => !string.IsNullOrWhiteSpace(opt) 
                    && !opt.StartsWith("/Yu", StringComparison.OrdinalIgnoreCase)
                    && !opt.StartsWith("/Yc", StringComparison.OrdinalIgnoreCase)
                    && !opt.StartsWith("/Fp", StringComparison.OrdinalIgnoreCase));
            args.AddRange(filteredOptions);
        }
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
