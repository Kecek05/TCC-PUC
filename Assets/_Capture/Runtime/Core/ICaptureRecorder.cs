#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace KeceK.Capture
{
    /// <summary>
    /// Starts and stops a video file. The runtime side only knows this interface; the Unity Recorder
    /// implementation lives in the Editor assembly and installs itself into <see cref="CaptureRecorderHub"/>.
    /// </summary>
    public interface ICaptureRecorder
    {
        bool IsRecording { get; }

        /// <summary>Absolute path of the file being written, without extension decisions left to the caller.</summary>
        string CurrentPath { get; }

        void Begin(string absolutePathWithoutExtension);
        void End();
    }

    public static class CaptureRecorderHub
    {
        /// <summary>Set by the Editor (InitializeOnLoad). Null in a player build: shots then run unrecorded.</summary>
        public static Func<ICaptureRecorder> Factory;

        private static ICaptureRecorder s_instance;

        public static ICaptureRecorder Get()
        {
            if (s_instance == null && Factory != null) s_instance = Factory();
            return s_instance;
        }
    }
}
#endif
