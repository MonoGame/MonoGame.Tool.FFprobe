namespace BuildScripts;

[TaskName("Build Windows")]
[IsDependentOn(typeof(PrepTask))]
[IsDependeeOf(typeof(BuildToolTask))]
public sealed class BuildWindowsTask : FrostingTask<BuildContext>
{
    public override bool ShouldRun(BuildContext context) => context.IsRunningOnWindows();

    public override void Run(BuildContext context)
    {
        //  Patch vcpkg files for windows build
        context.StartProcess("patch", "./buildscripts/vcpkg/ports/ffmpeg/portfile.cmake ./patches/ffmpeg-portfile.patch");
        context.StartProcess("patch", "./buildscripts/vcpkg/triplets/x64-windows-static.cmake ./patches/x64-windows-static-cmake.patch");

        //  Bootstrap vcpkg
        context.StartProcess("buildscripts/vcpkg/bootstrap-vcpkg.bat");

        //  Perform x64-windows build
        context.StartProcess("buildscripts/vcpkg/vcpkg.exe", "install ffmpeg[mp3lame,vorbis]:x64-windows-static");

        //  Copy build to artifacts
        context.CopyFile("buildscripts/vcpkg/installed/x64-windows-static/tools/ffmpeg/ffprobe.exe", $"{context.ArtifactsDir}/ffprobe.exe");
    }

    public override void Finally(BuildContext context)
    {
        //  Ensure we revert the patched files so when running/testing locally they are put back in original state
        context.StartProcess("patch", "-R ./buildscripts/vcpkg/ports/ffmpeg/portfile.cmake ./patches/ffmpeg-portfile.patch");
        context.StartProcess("patch", "-R ./buildscripts/vcpkg/triplets/x64-windows-static.cmake ./patches/x64-windows-static-cmake.patch");
    }
}
