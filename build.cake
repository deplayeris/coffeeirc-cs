#tool nuget:?package=Cake.Tool&version=4.0.0

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var projectPath = "./io.github.deplayeris.coffeeirc/io.github.deplayeris.coffeeirc.csproj";

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

Setup(context =>
{
    Information("Building CoffeeIRC...");
    Information("Configuration: {0}", configuration);
});

Teardown(context =>
{
    Information("Finished running tasks.");
});

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Description("Cleans all build output directories")
    .Does(() =>
{
    var directoriesToDelete = new[] {
        "./io.github.deplayeris.coffeeirc/bin",
        "./io.github.deplayeris.coffeeirc/obj"
    };
    
    foreach(var dir in directoriesToDelete)
    {
        if (DirectoryExists(dir))
        {
            DeleteDirectory(dir, new DeleteDirectorySettings { 
                Recursive = true, 
                Force = true 
            });
        }
    }
});

Task("Restore")
    .Description("Restores NuGet packages")
    .IsDependentOn("Clean")
    .Does(() =>
{
    DotNetRestore(projectPath);
});

Task("Build")
    .Description("Builds the project")
    .IsDependentOn("Restore")
    .Does(() =>
{
    DotNetBuild(projectPath, new DotNetBuildSettings {
        Configuration = configuration,
        NoRestore = true
    });
});

Task("Test")
    .Description("Runs unit tests")
    .IsDependentOn("Build")
    .Does(() =>
{
    DotNetTest(projectPath, new DotNetTestSettings {
        Configuration = configuration,
        NoBuild = true,
        NoRestore = true
    });
});

Task("Publish-Linux")
    .Description("Publishes for Linux x64 with NativeAOT")
    .IsDependentOn("Build")
    .Does(() =>
{
    var outputPath = $"./io.github.deplayeris.coffeeirc/bin/{configuration}/net10.0/linux-x64/native";
    
    DotNetPublish(projectPath, new DotNetPublishSettings {
        Configuration = configuration,
        Runtime = "linux-x64",
        OutputDirectory = outputPath,
        MSBuildSettings = new DotNetMSBuildSettings {
            MaxCpuCount = 1
        }
        .WithProperty("PublishAot", "true")
        .WithProperty("OutputType", "Library")
    });
    
    Information($"Published to: {outputPath}");
});

Task("Publish-Windows")
    .Description("Publishes for Windows x64 with NativeAOT")
    .IsDependentOn("Build")
    .Does(() =>
{
    var outputPath = $"./io.github.deplayeris.coffeeirc/bin/{configuration}/net10.0/win-x64/native";
    
    DotNetPublish(projectPath, new DotNetPublishSettings {
        Configuration = configuration,
        Runtime = "win-x64",
        OutputDirectory = outputPath,
        MSBuildSettings = new DotNetMSBuildSettings {
            MaxCpuCount = 1
        }
        .WithProperty("PublishAot", "true")
        .WithProperty("OutputType", "Library")
    });
    
    Information($"Published to: {outputPath}");
});

Task("Publish-Windows-Arm64")
    .Description("Publishes for Windows ARM64 with NativeAOT")
    .IsDependentOn("Build")
    .Does(() =>
{
    var outputPath = $"./io.github.deplayeris.coffeeirc/bin/{configuration}/net10.0/win-arm64/native";
    
    DotNetPublish(projectPath, new DotNetPublishSettings {
        Configuration = configuration,
        Runtime = "win-arm64",
        OutputDirectory = outputPath,
        MSBuildSettings = new DotNetMSBuildSettings {
            MaxCpuCount = 1
        }
        .WithProperty("PublishAot", "true")
        .WithProperty("OutputType", "Library")
    });
    
    Information($"Published to: {outputPath}");
});

Task("Publish-MacOS-x64")
    .Description("Publishes for macOS x64 with NativeAOT")
    .IsDependentOn("Build")
    .Does(() =>
{
    var outputPath = $"./io.github.deplayeris.coffeeirc/bin/{configuration}/net10.0/osx-x64/native";
    
    DotNetPublish(projectPath, new DotNetPublishSettings {
        Configuration = configuration,
        Runtime = "osx-x64",
        OutputDirectory = outputPath,
        MSBuildSettings = new DotNetMSBuildSettings {
            MaxCpuCount = 1
        }
        .WithProperty("PublishAot", "true")
        .WithProperty("OutputType", "Library")
    });
    
    Information($"Published to: {outputPath}");
});

Task("Publish-MacOS-Arm64")
    .Description("Publishes for macOS ARM64 with NativeAOT")
    .IsDependentOn("Build")
    .Does(() =>
{
    var outputPath = $"./io.github.deplayeris.coffeeirc/bin/{configuration}/net10.0/osx-arm64/native";
    
    DotNetPublish(projectPath, new DotNetPublishSettings {
        Configuration = configuration,
        Runtime = "osx-arm64",
        OutputDirectory = outputPath,
        MSBuildSettings = new DotNetMSBuildSettings {
            MaxCpuCount = 1
        }
        .WithProperty("PublishAot", "true")
        .WithProperty("OutputType", "Library")
    });
    
    Information($"Published to: {outputPath}");
});

Task("Publish-Linux-Arm64")
    .Description("Publishes for Linux ARM64 with NativeAOT")
    .IsDependentOn("Build")
    .Does(() =>
{
    var outputPath = $"./io.github.deplayeris.coffeeirc/bin/{configuration}/net10.0/linux-arm64/native";
    
    DotNetPublish(projectPath, new DotNetPublishSettings {
        Configuration = configuration,
        Runtime = "linux-arm64",
        OutputDirectory = outputPath,
        MSBuildSettings = new DotNetMSBuildSettings {
            MaxCpuCount = 1
        }
        .WithProperty("PublishAot", "true")
        .WithProperty("OutputType", "Library")
        .WithProperty("CppCompilerAndLinker", "clang")
        .WithProperty("ClangFlags", "--target=aarch64-linux-gnu")
    });
    
    Information($"Published to: {outputPath}");
});

Task("Package-Linux")
    .Description("Creates tar.gz archive for Linux")
    .IsDependentOn("Publish-Linux")
    .Does(() =>
{
    var sourceDir = $"./io.github.deplayeris.coffeeirc/bin/{configuration}/net10.0/linux-x64/native";
    var outputFile = "./artifacts/coffeeirc-linux-x64.tar.gz";
    
    EnsureDirectoryExists("./artifacts");
    
    if (IsRunningOnUnix())
    {
        StartProcess("tar", new ProcessSettings {
            Arguments = $"-czf {MakeAbsolute(File(outputFile)).FullPath} -C {MakeAbsolute(Directory(sourceDir)).FullPath} .",
            WorkingDirectory = Directory(sourceDir)
        });
    }
    
    Information($"Created package: {outputFile}");
});

Task("Package-Linux-Arm64")
    .Description("Creates tar.gz archive for Linux ARM64")
    .IsDependentOn("Publish-Linux-Arm64")
    .Does(() =>
{
    var sourceDir = $"./io.github.deplayeris.coffeeirc/bin/{configuration}/net10.0/linux-arm64/native";
    var outputFile = "./artifacts/coffeeirc-linux-arm64.tar.gz";
    
    EnsureDirectoryExists("./artifacts");
    
    if (IsRunningOnUnix())
    {
        StartProcess("tar", new ProcessSettings {
            Arguments = $"-czf {MakeAbsolute(File(outputFile)).FullPath} -C {MakeAbsolute(Directory(sourceDir)).FullPath} .",
            WorkingDirectory = Directory(sourceDir)
        });
    }
    
    Information($"Created package: {outputFile}");
});

Task("Package-Windows")
    .Description("Creates ZIP archive for Windows")
    .IsDependentOn("Publish-Windows")
    .Does(() =>
{
    var sourceDir = $"./io.github.deplayeris.coffeeirc/bin/{configuration}/net10.0/win-x64/native";
    var outputFile = "./artifacts/coffeeirc-windows-x64.zip";
    
    EnsureDirectoryExists("./artifacts");
    
    Zip(sourceDir, outputFile);
    
    Information($"Created package: {outputFile}");
});

Task("Package-Windows-Arm64")
    .Description("Creates ZIP archive for Windows ARM64")
    .IsDependentOn("Publish-Windows-Arm64")
    .Does(() =>
{
    var sourceDir = $"./io.github.deplayeris.coffeeirc/bin/{configuration}/net10.0/win-arm64/native";
    var outputFile = "./artifacts/coffeeirc-windows-arm64.zip";
    
    EnsureDirectoryExists("./artifacts");
    
    Zip(sourceDir, outputFile);
    
    Information($"Created package: {outputFile}");
});

Task("Package-MacOS")
    .Description("Creates tar.gz archives for macOS")
    .IsDependentOn("Publish-MacOS-x64")
    .IsDependentOn("Publish-MacOS-Arm64")
    .Does(() =>
{
    EnsureDirectoryExists("./artifacts");
    
    // x64
    var sourceDirX64 = $"./io.github.deplayeris.coffeeirc/bin/{configuration}/net10.0/osx-x64/native";
    var outputFileX64 = "./artifacts/coffeeirc-macos-x64.tar.gz";
    
    if (IsRunningOnUnix())
    {
        StartProcess("tar", new ProcessSettings {
            Arguments = $"-czf {MakeAbsolute(File(outputFileX64)).FullPath} -C {MakeAbsolute(Directory(sourceDirX64)).FullPath} .",
            WorkingDirectory = Directory(sourceDirX64)
        });
    }
    
    // ARM64
    var sourceDirArm64 = $"./io.github.deplayeris.coffeeirc/bin/{configuration}/net10.0/osx-arm64/native";
    var outputFileArm64 = "./artifacts/coffeeirc-macos-arm64.tar.gz";
    
    if (IsRunningOnUnix())
    {
        StartProcess("tar", new ProcessSettings {
            Arguments = $"-czf {MakeAbsolute(File(outputFileArm64)).FullPath} -C {MakeAbsolute(Directory(sourceDirArm64)).FullPath} .",
            WorkingDirectory = Directory(sourceDirArm64)
        });
    }
    
    Information($"Created packages: {outputFileX64}, {outputFileArm64}");
});

Task("Package-All")
    .Description("Creates packages for all platforms")
    .IsDependentOn("Package-Linux")
    .IsDependentOn("Package-Linux-Arm64")
    .IsDependentOn("Package-Windows")
    .IsDependentOn("Package-Windows-Arm64")
    .IsDependentOn("Package-MacOS");

Task("CI")
    .Description("Complete CI pipeline: Build, Test, Package All")
    .IsDependentOn("Build")
    .IsDependentOn("Test")
    .IsDependentOn("Package-All");

Task("Default")
    .Description("Default task: Build and Test")
    .IsDependentOn("Build")
    .IsDependentOn("Test");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
