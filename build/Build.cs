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
using Fallout.Common.Tools.Git;
using Fallout.Common.Utilities.Collections;
using static Fallout.Common.EnvironmentInfo;
using static Fallout.Common.IO.PathConstruction;

class Build : FalloutBuild
{
    public static int Main() => Execute<Build>(x => x.Compile);

    [Parameter("Path to vvvvc.exe")] private string CompilerPath;
    [Parameter("Github release token")] [Secret] string GithubReleaseToken;
    
    private string ProductName = "VlDocInfo";
    
    private AbsolutePath DocumentFile => RootDirectory / $"{ProductName}.vl";
    private AbsolutePath VersionFile => RootDirectory / "version.props";
    private AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    private AbsolutePath VvvvcOutputDirectory => ArtifactsDirectory / $"{ProductName}";
    private AbsolutePath IconFile => RootDirectory / "img" / "icon.ico";
    
    private AbsolutePath VelopackReleasesDirectory => RootDirectory / "velopack-releases";
    
    string Version => XDocument.Load(VersionFile).Descendants("Version").FirstOrDefault()?.Value;
    
    private string RepositoryUrl = "https://github.com/sebescudie/VlDocInfo";
    
    private Target Clean => _ => _
        .Executes(() =>
        {
            ArtifactsDirectory.CreateOrCleanDirectory();
            VelopackReleasesDirectory.CreateOrCleanDirectory();
        });

    private Target Compile => _ => _
        .Before(DownloadPreviousRelease)
        .DependsOn(Clean)
        .Executes(() =>
        {
            var build = ProcessTasks.StartProcess(CompilerPath, $"{DocumentFile}");
            build.WaitForExit();
        });

    private Target DownloadPreviousRelease => _ => _
        .Before(Pack)
        .Executes(() =>
        {
            var downloadPreviousRelease = ProcessTasks.StartProcess("vpk",
                $"download github --repoUrl {RepositoryUrl} --outputDir {VelopackReleasesDirectory}");
            downloadPreviousRelease.WaitForExit();
        });
    
    private Target Pack => _ => _
        .DependsOn(Compile)
        .DependsOn(DownloadPreviousRelease)
        .Requires(() => !string.IsNullOrEmpty(Version))
        .Requires(() => File.Exists(IconFile))
        .Executes(() =>
        {
            var packCurrentRelease = ProcessTasks.StartProcess("vpk",
                $"pack --packId {ProductName} --packTitle {ProductName} --icon {IconFile} --packVersion {Version} --packDir {VvvvcOutputDirectory} --outputDir {VelopackReleasesDirectory} --mainExe {ProductName}.exe --framework net10.0-x64-desktop");
            packCurrentRelease.WaitForExit();
        });

    private Target Upload => _ => _
        .DependsOn(Pack)
        .Requires(() => !string.IsNullOrEmpty(GithubReleaseToken))
        .Requires(() => GitTasks.GitHasCleanWorkingCopy())
        .Executes(() =>
        {
            var uploadNewRelease = ProcessTasks.StartProcess("vpk", 
                $"upload github --repoUrl {RepositoryUrl} --outputDir {VelopackReleasesDirectory} --publish --releaseName {Version} --tag {Version} --token {GithubReleaseToken}");
            uploadNewRelease.WaitForExit();
        });

    private Target Release => _ => _
        .DependsOn(Pack)
        .DependsOn(Upload)
        .Executes(() =>
        {

        });
}
