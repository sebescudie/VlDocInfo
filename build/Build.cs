using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Fallout.Common;
using Fallout.Common.CI;
using Fallout.Common.Execution;
using Fallout.Common.IO;
using Fallout.Solutions;
using Fallout.Common.Tooling;
using Fallout.Common.Utilities.Collections;
using static Fallout.Common.EnvironmentInfo;
using static Fallout.Common.IO.PathConstruction;

class Build : FalloutBuild
{
    public static int Main() => Execute<Build>(x => x.Compile);

    [Parameter("Path to vvvvc.exe")] private string CompilerPath;
    [Parameter("Skip downloading previous Velopack version")] private bool SkipDownloadPreviousRelease = false;
    
    private string _productName = "VlDocInfo";
    
    private AbsolutePath DocumentFile => RootDirectory / $"{_productName}.vl";
    private AbsolutePath VersionFile => RootDirectory / "version.props";
    private AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    private AbsolutePath VvvvcOutputDirectory => ArtifactsDirectory / $"{_productName}";
    private AbsolutePath IconFile => RootDirectory / "img" / "icon.ico";
    
    private AbsolutePath VelopackReleasesDirectory => RootDirectory / "velopack-releases";
    
    string Version => XDocument.Load(VersionFile).Descendants("Version").FirstOrDefault()?.Value;
    
    private string _repositoryUrl = "https://github.com/sebescudie/VlDocInfo";
    
    
    Target Clean => _ => _
        .Executes(() =>
        {
            ArtifactsDirectory.CreateOrCleanDirectory();
            VelopackReleasesDirectory.CreateOrCleanDirectory();
        });

    Target Compile => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            var build = ProcessTasks.StartProcess(CompilerPath, $"{DocumentFile}");
            build.WaitForExit();
        });

    private Target Pack => _ => _
        .DependsOn(Compile)
        .Requires(() => !string.IsNullOrEmpty(Version))
        .Requires(() => File.Exists(IconFile))
        .Executes(() =>
        {
            var tool = "vpk";

            // Download previous release to compute deltas, if any
            if (!SkipDownloadPreviousRelease)
            {
                var downloadPreviousRelease = ProcessTasks.StartProcess(tool,
                    $"download github --repoUrl {_repositoryUrl} --outputDir {VelopackReleasesDirectory}");
                downloadPreviousRelease.WaitForExit();
            }
            
            // Pack current release
            var packCurrentRelease = ProcessTasks.StartProcess(tool,
                $"pack --packId {_productName} --packTitle {_productName} --icon {IconFile} --packVersion {Version} --packDir {VvvvcOutputDirectory} --outputDir {VelopackReleasesDirectory} --mainExe {_productName}.exe --framework net10.0-x64-desktop");
            packCurrentRelease.WaitForExit();
        });

}
