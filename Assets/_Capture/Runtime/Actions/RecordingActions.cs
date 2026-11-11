#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;

namespace KeceK.Capture
{
    /// <summary>Opens a video file under the context's output folder. Without a recorder (a player build) it only logs.</summary>
    [Serializable]
    public class BeginRecording : ICaptureAction
    {
        public string FileName = "take";

        public BeginRecording() { }
        public BeginRecording(string fileName) => FileName = fileName;

        public IEnumerator Run(CaptureContext ctx)
        {
            ICaptureRecorder recorder = ctx.Recorder;
            string path = ctx.ResolveOutput(Path.GetFileNameWithoutExtension(FileName));

            if (recorder == null)
            {
                ctx.Log($"  no recorder available; '{FileName}' runs unrecorded");
                ctx.NotifyRecordingStarted(FileName);
                yield break;
            }

            if (recorder.IsRecording) recorder.End();
            recorder.Begin(path);
            ctx.NotifyRecordingStarted(FileName);

            // The Recorder starts on the next frame; give it one so the lead-in is really on tape.
            yield return null;
            yield return null;
        }

        public string Describe() => $"BeginRecording {FileName}";
    }

    [Serializable]
    public class EndRecording : ICaptureAction
    {
        public IEnumerator Run(CaptureContext ctx)
        {
            ICaptureRecorder recorder = ctx.Recorder;
            if (recorder != null && recorder.IsRecording) recorder.End();
            ctx.NotifyRecordingStopped();
            yield return null;
        }

        public string Describe() => "EndRecording";
    }
}
#endif
