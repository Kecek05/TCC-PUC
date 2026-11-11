#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;

namespace KeceK.Capture
{
    /// <summary>
    /// One step of a capture: a drag, a swipe, a wait, a time-scale ramp. The director runs them in order and
    /// knows nothing about what any of them does, so a new kind of step is a new class and nothing else.
    /// </summary>
    /// <remarks>
    /// Implementations are plain [Serializable] classes stored with [SerializeReference] in a
    /// <see cref="ShotDefinition"/> or <see cref="MatchScript"/>. Waits inside them are measured in
    /// unscaled time, which under the Recorder's constant frame rate is video time.
    /// </remarks>
    public interface ICaptureAction
    {
        IEnumerator Run(CaptureContext ctx);

        /// <summary>One line for the capture log.</summary>
        string Describe();
    }
}
#endif
