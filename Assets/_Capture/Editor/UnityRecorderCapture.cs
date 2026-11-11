using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace KeceK.Capture.EditorTools
{
    /// <summary>
    /// The Unity Recorder behind <see cref="ICaptureRecorder"/>: Movie Recorder, H.264 MP4, a constant 60 fps
    /// (the Recorder then drives <see cref="Time.captureDeltaTime"/>, so slow motion and cuts stay stable
    /// whatever the Editor manages in wall time), game audio, Game View at 1080x1920.
    /// </summary>
    public class UnityRecorderCapture : ICaptureRecorder
    {
        public const int Width = 1080;
        public const int Height = 1920;
        public const float FrameRate = 60f;

        private RecorderController _controller;

        public bool IsRecording => _controller != null && _controller.IsRecording();
        public string CurrentPath { get; private set; } = "";

        public void Begin(string absolutePathWithoutExtension)
        {
            if (IsRecording) End();

            RecorderControllerSettings controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            MovieRecorderSettings movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name = "Capture Movie";
            movie.Enabled = true;
            movie.EncoderSettings = new CoreEncoderSettings
            {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
            };
            movie.CaptureAlpha = false;
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = Width, OutputHeight = Height };
            movie.AudioInputSettings.PreserveAudio = true;
            movie.OutputFile = absolutePathWithoutExtension.Replace('\\', '/');

            controllerSettings.AddRecorderSettings(movie);
            controllerSettings.SetRecordModeToManual();
            controllerSettings.FrameRatePlayback = FrameRatePlayback.Constant;
            controllerSettings.FrameRate = FrameRate;
            controllerSettings.CapFrameRate = true;

            _controller = new RecorderController(controllerSettings);
            _controller.PrepareRecording();
            if (!_controller.StartRecording())
                Debug.LogError($"[Capture] Recorder refused to start for {absolutePathWithoutExtension}");

            CurrentPath = absolutePathWithoutExtension + ".mp4";
        }

        public void End()
        {
            if (_controller == null) return;
            if (_controller.IsRecording()) _controller.StopRecording();
            _controller = null;
        }
    }

    [InitializeOnLoad]
    internal static class UnityRecorderCaptureInstaller
    {
        static UnityRecorderCaptureInstaller()
        {
            CaptureRecorderHub.Factory = () => new UnityRecorderCapture();
        }
    }
}
