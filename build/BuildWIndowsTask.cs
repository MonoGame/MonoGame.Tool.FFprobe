namespace BuildScripts;

[TaskName("Build Windows")]
[IsDependentOn(typeof(PrepTask))]
[IsDependeeOf(typeof(BuildToolTask))]
public sealed class BuildWindowsTask : FrostingTask<BuildContext>
{
    public override bool ShouldRun(BuildContext context) => context.IsRunningOnWindows();

    public override void Run(BuildContext context)
    {
        // Absolute path to the artifact directory is needed for flags since they don't allow relative path
        var artifactDir = context.MakeAbsolute(new DirectoryPath(context.ArtifactsDir));

        // The directory that all dependencies that are built manually are output too. Originally this was output to the
        // artifacts directory but that started causing issues on the github runners, so it was moved back to the
        // project root directory.
        var dependencyDir = context.MakeAbsolute(new DirectoryPath($"{context.ArtifactsDir}/../dependencies-windows-x64"));

        // For Windows build, since we're using  mingw environment, we can't set environment variables as normal
        // since they would be set for the Windows side of things and not the mingw environment that everything is
        // running in.  Instead, we'll build an export statement that can be used at the start of every process call to
        // ensure the correct environment variables are set for each command executed.
        var cFlagsExport = "export CFLAGS=\"-w\";";
        var ccFlagsExport = "export CCFLAGS=\"x86_64-w64-mingw32-gcc\";";
        var ldFlagsExport = "export LDFLAGS=\"--static\";";
        var pathExport = "export PATH=\"/usr/bin:/mingw64/bin:$PATH\";";
        var pkgConfigExport = $"export PKG_CONFIG_PATH=\"/mingw64/lib/pkgconfig:$PKG_CONFIG_PATH\";";
        var exports = $"{pathExport}{cFlagsExport}{ccFlagsExport}{ldFlagsExport}{pkgConfigExport}";

        // The --prefix flag used for all ./configure commands to ensure that build dependencies are output to the
        // dependency directory specified
        var prefixFlag = $"--prefix=\"{dependencyDir}\"";

        // The --bindir flag used in the final ffprobe build so that the binary is output to the artifacts directory.
        var binDirFlag = $"--bindir=\"{artifactDir}\"";

        // Get the FFprobe ./configure flags specific for this windows build
        var configureFlags = GetFFProbConfigureFlags(context);

        // The command to execute in order to run the shell environment (mingw) needed for this build.
        var shellCommandPath = @"C:\msys64\usr\bin\bash.exe";

        // Reusuable process settings instance. As each dependency is built, we'll adjust the working directory and
        // arguments of this instance for each command.
        var processSettings = new ProcessSettings();

        // Build libogg
        processSettings.WorkingDirectory = "./ogg";
        processSettings.Arguments = $"-c \"{exports} make distclean\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"{exports} ./autogen.sh\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"{exports} ./configure --disable-shared {prefixFlag}\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"{exports} make -j{Environment.ProcessorCount}\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"{exports} make install\"";
        context.StartProcess(shellCommandPath, processSettings);

        // build libvorbis
        processSettings.WorkingDirectory = "./vorbis";
        processSettings.Arguments = $"-c \"{exports} make distclean\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"{exports} ./autogen.sh\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"{exports} ./configure --disable-examples --disable-docs --disable-shared {prefixFlag}\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"{exports} make -j{Environment.ProcessorCount}\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"{exports} make install\"";
        context.StartProcess(shellCommandPath, processSettings);

        // build lame
        processSettings.WorkingDirectory = "./lame";
        processSettings.Arguments = $"-c \"{exports} make distclean\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"{exports} ./configure --disable-frontend --disable-decoder --disable-shared {prefixFlag}\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"{exports} make -j{Environment.ProcessorCount}\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"{exports} make install\"";
        context.StartProcess(shellCommandPath, processSettings);

        // Build ffprobe
        processSettings.WorkingDirectory = "./ffmpeg";
        processSettings.Arguments = $"-c \"{exports} make distclean\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"{exports} ./configure {binDirFlag} {configureFlags}\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"{exports} make -j{Environment.ProcessorCount}\"";
        context.StartProcess(shellCommandPath, processSettings);
        processSettings.Arguments = $"-c \"{exports} make install\"";
        context.StartProcess(shellCommandPath, processSettings);
    }

    private static string GetFFProbConfigureFlags(BuildContext context)
    {
        var ignoreCommentsAndNewLines = (string line) => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#');
        var configureFlags = context.FileReadLines("ffprobe.config").Where(ignoreCommentsAndNewLines);
        var osConfigureFlags = context.FileReadLines($"ffprobe.windows-x64.config").Where(ignoreCommentsAndNewLines);
        return string.Join(' ', configureFlags) + " " + string.Join(' ', osConfigureFlags);
    }
}
