namespace BuildScripts;

[TaskName("Build Linux")]
[IsDependentOn(typeof(PrepTask))]
[IsDependeeOf(typeof(BuildToolTask))]
public sealed class BuildLinuxTask : FrostingTask<BuildContext>
{
    public override bool ShouldRun(BuildContext context) => context.IsRunningOnLinux();

    public override void Run(BuildContext context)
    {
        //  Patch vcpkg files for linux build
        context.StartProcess("patch", "./buildscripts/vcpkg/ports/ffmpeg/portfile.cmake ./patches/ffmpeg-portfile.patch");
        context.StartProcess("patch", "./buildscripts/vcpkg/triplets/x64-linux.cmake ./patches/x64-linux-cmake.patch");

        //  Bootstrap vcpkg
        context.StartProcess("buildscripts/vcpkg/bootstrap-vcpkg.sh");

        //  Perform x64-linux build
        context.StartProcess("buildscripts/vcpkg/vcpkg", "install ffmpeg[mp3lame,vorbis]:x64-linux");

        //  Copy build to artifacts
        context.CopyFile("buildscripts/vcpkg/installed/x64-linux/tools/ffmpeg/ffprobe", $"{context.ArtifactsDir}/ffprobe");
    }

    public override void Finally(BuildContext context)
    {
        //  Ensure we revert the patched files so when running/testing locally they are put back in original state
        context.StartProcess("patch", "-R ./buildscripts/vcpkg/ports/ffmpeg/portfile.cmake ./patches/ffmpeg-portfile.patch");
        context.StartProcess("patch", "-R ./buildscripts/vcpkg/triplets/x64-linux.cmake ./patches/x64-linux-cmake.patch");
    }
}
