namespace BuildScripts;

[TaskName("Build macOS")]
[IsDependentOn(typeof(PrepTask))]
[IsDependeeOf(typeof(BuildToolTask))]
public sealed class BuildMacOSTask : FrostingTask<BuildContext>
{
    public override bool ShouldRun(BuildContext context) => context.IsRunningOnMacOs();

    public override void Run(BuildContext context)
    {
        //  Patch vcpkg files for mac build
        context.StartProcess("patch", "./buildscripts/vcpkg/ports/ffmpeg/portfile.cmake ./patches/ffmpeg-portfile.patch");
        context.StartProcess("patch", "./buildscripts/vcpkg/triplets/x64-osx.cmake ./patches/x64-osx-cmake.patch");
        context.StartProcess("patch", "./buildscripts/vcpkg/triplets/arm64-osx.cmake ./patches/arm64-osx-cmake.patch");

        //  Bootstrap vcpkg
        context.StartProcess("buildscripts/vcpkg/bootstrap-vcpkg.sh");

        //  Perform x64-osx build
        context.StartProcess("buildscripts/vcpkg/vcpkg", "install ffmpeg[mp3lame,vorbis]:x64-osx");

        //  Perform arm64-osx build
        context.StartProcess("buildscripts/vcpkg/vcpkg", "install ffmpeg[mp3lame,vorbis]:arm64-osx");

        //  Use lipo to combine into universal binary and output in the artifacts directory
        string x64 = "buildscripts/vcpkg/installed/x64-osx/tools/ffmpeg/ffprobe";
        string arm64 = "buildscripts/vcpkg/installed/arm64-osx/tools/ffmpeg/ffprobe";
        context.StartProcess("lipo", new ProcessSettings()
        {
            Arguments = $"-create {x64} {arm64} -output {context.ArtifactsDir}/ffprobe"
        });
    }

    public override void Finally(BuildContext context)
    {
        //  Ensure we revert the patched files so when running/testing locally they are put back in original state
        context.StartProcess("patch", "-R ./buildscripts/vcpkg/ports/ffmpeg/portfile.cmake ./patches/ffmpeg-portfile.patch");
        context.StartProcess("patch", "-R ./buildscripts/vcpkg/triplets/x64-osx.cmake ./patches/x64-osx-cmake.patch");
        context.StartProcess("patch", "-R ./buildscripts/vcpkg/triplets/arm64-osx.cmake ./patches/arm64-osx-cmake.patch");
    }
}
