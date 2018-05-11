#addin nuget:?package=Cake.Incubator&version=2.0.2 

#tool nuget:?package=Microsoft.TestPlatform&version=15.7.0

///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Task("CleanTask")
    .Does(() =>
    {
        var solution = ParseSolution("./SecurityDriven.Inferno.sln");
        foreach(var project in solution.Projects)
        {
            // check solution items and exclude solution folders, since they are virtual
            if(project.Name == "Solution Items")
                continue; 

            var customProject = ParseProject(project.Path, configuration: "Release");

            foreach(var directory in customProject.OutputPaths)
                CleanDirectory(directory);
        }
    });

Task("RestoreNugetTask")
    .IsDependentOn("CleanTask")
    .Does(() =>
    {
        var settings = new NuGetRestoreSettings()
        {
            DisableParallelProcessing = false,
            Verbosity = NuGetVerbosity.Quiet,
            NoCache = false,
        };

        NuGetRestore("./SecurityDriven.Inferno.sln", settings);
    });

Task("BuildTask")
    .IsDependentOn("RestoreNugetTask")
    .Does(() =>
    {
        if(IsRunningOnWindows())
        {
            var settings = new MSBuildSettings()
            {
                ToolVersion = MSBuildToolVersion.VS2017,
                Verbosity = Verbosity.Minimal,
            };

            settings.SetConfiguration(configuration)
                    .SetDetailedSummary(false)
                    .SetMaxCpuCount(0)
                    .SetMSBuildPlatform(MSBuildPlatform.Automatic);

            MSBuild("./SecurityDriven.Inferno.sln", settings);
        }

        // could be expanded with XBuild
    });

Task("UnitTestTask")
    .IsDependentOn("BuildTask")
    .Does(() =>
    {
        var solution = ParseSolution("./SecurityDriven.Inferno.sln");
        var testsDirectories = new List<string>();
        var testAssemblies = new List<FilePath>();

        foreach(var project in solution.Projects)
        {
            // check solution items and exclude solution folders, since they are virtual
            if(project.Name == "Solution Items")
                continue;

            var customProject = ParseProject(project.Path, configuration: "Release");

            if(!project.Name.Contains("Test")) // we only care about test assemblies
                continue;

            foreach(var directory in customProject.OutputPaths)
                testsDirectories.Add(directory.FullPath);
        }

        foreach(var path in testsDirectories)
        {
            var files = GetFiles(path + "/*.Test.dll");

            foreach(var file in files)
            {
                testAssemblies.Add(file);
                Information(file);
            }
        }

        if(testAssemblies.Count == 0) // obviously, since there are no public tests, this is not very useful yet
            return;

        var netCoreSettings = new VSTestSettings()
        {
            Parallel = true,
            InIsolation = true,
            Logger = "trx", // enable code coverage
            ToolPath = Context.Tools.Resolve("vstest.console.exe"),
            ArgumentCustomization  = args => args.Append(".NETCoreApp,Version=v2.1")
        };

        var netSettings = new VSTestSettings()
        {
            Parallel = true,
            InIsolation = true,
            Logger = "trx", // enable code coverage
            ToolPath = Context.Tools.Resolve("vstest.console.exe"),
            ArgumentCustomization  = args => args.Append(".NETFramework,Version=v4.6")
        };

        VSTest(testAssemblies, netCoreSettings);
        VSTest(testAssemblies, netSettings);
    });

// create nuspec file and nuget package?

///////////////////////////////////////////////////////////////////////////////
// TASKS
///////////////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("UnitTestTask");

RunTarget(target);