using System.Runtime.InteropServices;

namespace BuildScripts;

[TaskName("Build macOS")]
[IsDependentOn(typeof(PrepTask))]
[IsDependeeOf(typeof(BuildToolTask))]
public sealed class BuildMacOSTask : FrostingTask<BuildContext>
{
    public override bool ShouldRun(BuildContext context) => context.IsRunningOnMacOs();

    public override void Run(BuildContext context)
    {
        // There is an issue with the configure.ac script for mac where it uses the -force_cpusubtype_ALL flag which was
        // only valid for powerpc macs.  There is an issue opened at https://gitlab.xiph.org/xiph/vorbis/-/issues/2348
        // for reference.  For now, we'll just patch the flag out
        context.ReplaceTextInFiles("./vorbis/configure.ac", "-force_cpusubtype_ALL", string.Empty);

        // Determine which mac architecture(s) to build for.
        var buildX64 = context.IsUniversalBinary || RuntimeInformation.ProcessArchitecture is not Architecture.Arm or Architecture.Arm64;
        var buildArm64 = context.IsUniversalBinary || RuntimeInformation.ProcessArchitecture is Architecture.Arm or Architecture.Arm64;

        if (buildX64)
            BuildX64(context);

        if (buildArm64)
            BuildArm64(context);

        // If this is a universal build, we'll need to combine both binaries using lipo and output the result of that.
        if (buildX64 && buildArm64)
        {
            context.StartProcess("lipo", new ProcessSettings()
            {
                WorkingDirectory = context.ArtifactsDir,
                Arguments = $"-create ffprobe-x64 ffprobe-arm64 -output ffprobe"
            });

            // Remove the individual binaries now 
            context.StartProcess("rm", new ProcessSettings()
            {
                WorkingDirectory = context.ArtifactsDir,
                Arguments = $"-f ffprobe-x64 ffprobe-arm64"
            });
        }
    }

    private static void BuildArm64(BuildContext context)
    {
        // Absolute path to the artifact directory is needed for flags since they don't allow relative path
        var artifactDir = context.MakeAbsolute(new DirectoryPath(context.ArtifactsDir));
        var dependencyDir = context.MakeAbsolute(new DirectoryPath($"{context.ArtifactsDir}/../dependencies-arm64"));
        var prefixFlag = $"--prefix=\"{dependencyDir}\"";
        var hostFlag = "--host=\"aarch64-apple-darwin\"";
        var progsSuffixFlag = context.IsUniversalBinary ? "--progs-suffix=\"-arm64\"" : string.Empty;
        var binDirFlag = $"--bindir=\"{artifactDir}\"";

        var envVariables = new Dictionary<string, string>
        {
            {"CFLAGS", $"-mmacosx-version-min=10.5 -w -arch arm64 -I{dependencyDir}/include"},
            {"CPPFLAGS", $"-mmacosx-version-min=10.5 -arch arm64 -I{dependencyDir}/include"},
            {"CXXFLAGS", "-mmacosx-version-min=10.5 -arch arm64"},
            {"LDFLAGS", $"-mmacosx-version-min=10.5 -arch arm64 -L{dependencyDir}/lib"},
            {"PKG_CONFIG_PATH", $"{dependencyDir}/lib/pkgconfig"}
        };

        var configureFlags = GetFFMpegConfigureFlags(context, "osx-arm64");
        var processSettings = new ProcessSettings
        {
            EnvironmentVariables = envVariables
        };

        var shellCommandPath = "zsh";

        // Build libogg
        processSettings.WorkingDirectory = "./ogg";
        processSettings.Arguments = $"-c \"make distclean\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"./autogen.sh\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"./configure --disable-shared {prefixFlag} {hostFlag}\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"make -j{Environment.ProcessorCount}\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"make install\"";
        context.StartProcess(shellCommandPath, processSettings);

        // build libvorbis
        processSettings.WorkingDirectory = "./vorbis";
        processSettings.Arguments = $"-c \"make distclean\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"./autogen.sh\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"./configure --disable-examples --disable-docs --disable-shared {prefixFlag} {hostFlag}\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"make -j{Environment.ProcessorCount}\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"make install\"";
        context.StartProcess(shellCommandPath, processSettings);

        // build lame
        processSettings.WorkingDirectory = "./lame";
        processSettings.Arguments = $"-c \"make distclean\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"./configure --disable-frontend --disable-decoder --disable-shared {prefixFlag} {hostFlag}\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"make -j{Environment.ProcessorCount}\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"make install\"";
        context.StartProcess(shellCommandPath, processSettings);

        // Build ffprobe
        processSettings.WorkingDirectory = "./ffmpeg";
        processSettings.Arguments = $"-c \"make distclean\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"./configure {binDirFlag} {configureFlags} {progsSuffixFlag}\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"make -j{Environment.ProcessorCount}\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"make install\"";
        context.StartProcess(shellCommandPath, processSettings);
    }

    private static void BuildX64(BuildContext context)
    {
        // Absolute path to the artifact directory is needed for flags since they don't allow relative path
        var artifactDir = context.MakeAbsolute(new DirectoryPath(context.ArtifactsDir));
        var dependencyDir = context.MakeAbsolute(new DirectoryPath($"{context.ArtifactsDir}/../dependencies-x64"));
        var prefixFlag = $"--prefix=\"{dependencyDir}\"";
        var hostFlag = "--host=\"x86_64-apple-darwin\"";
        var progsSuffixFlag = context.IsUniversalBinary ? "--progs-suffix=\"-x64\"" : string.Empty;
        var binDirFlag = $"--bindir=\"{artifactDir}\"";

        var envVariables = new Dictionary<string, string>
        {
            {"CFLAGS", $"-mmacosx-version-min=10.5 -w -arch x86_64 -I{dependencyDir}/include"},
            {"CPPFLAGS", $"-mmacosx-version-min=10.5 -arch x86_64 -I{dependencyDir}/include"},
            {"CXXFLAGS", "-mmacosx-version-min=10.5 -arch x86_64"},
            {"LDFLAGS", $"-mmacosx-version-min=10.5 -arch x86_64 -L{dependencyDir}/lib"},
            {"PKG_CONFIG_PATH", $"{dependencyDir}/lib/pkgconfig"}
        };

        var configureFlags = GetFFMpegConfigureFlags(context, "osx-x64");
        var processSettings = new ProcessSettings
        {
            EnvironmentVariables = envVariables
        };

        var shellCommandPath = "zsh";

        // Build libogg
        processSettings.WorkingDirectory = "./ogg";
        processSettings.Arguments = $"-c \"make distclean\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"./autogen.sh\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"./configure --disable-shared {prefixFlag} {hostFlag}\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"make -j{Environment.ProcessorCount}\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"make install\"";
        context.StartProcess(shellCommandPath, processSettings);

        // build libvorbis
        processSettings.WorkingDirectory = "./vorbis";
        processSettings.Arguments = $"-c \"make distclean\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"./autogen.sh\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"./configure --disable-examples --disable-docs --disable-shared {prefixFlag} {hostFlag}\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"make -j{Environment.ProcessorCount}\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"make install\"";
        context.StartProcess(shellCommandPath, processSettings);

        // build lame
        processSettings.WorkingDirectory = "./lame";
        processSettings.Arguments = $"-c \"make distclean\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"./configure --disable-frontend --disable-decoder --disable-shared {prefixFlag} {hostFlag}\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"make -j{Environment.ProcessorCount}\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"make install\"";
        context.StartProcess(shellCommandPath, processSettings);

        // Build ffprobe
        processSettings.WorkingDirectory = "./ffmpeg";
        processSettings.Arguments = $"-c \"make distclean\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"./configure {binDirFlag} {configureFlags} {progsSuffixFlag}\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"make -j{Environment.ProcessorCount}\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"make install\"";
        context.StartProcess(shellCommandPath, processSettings);
    }

    private static string GetFFMpegConfigureFlags(BuildContext context, string rid)
    {
        var ignoreCommentsAndNewLines = (string line) => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#');
        var configureFlags = context.FileReadLines("ffprobe.config").Where(ignoreCommentsAndNewLines);
        var osConfigureFlags = context.FileReadLines($"ffprobe.{rid}.config").Where(ignoreCommentsAndNewLines);
        return string.Join(' ', configureFlags) + " " + string.Join(' ', osConfigureFlags);
    }
}
