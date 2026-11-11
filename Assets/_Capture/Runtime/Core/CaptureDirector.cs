#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KeceK.Capture
{
    /// <summary>
    /// Runs a sequence of <see cref="ICaptureAction"/>s, one after another, and nothing else: it does not know
    /// what a card, a swipe or a recording is. What a shot contains is data (<see cref="ShotDefinition"/>,
    /// <see cref="MatchScript"/>); what a step does is its own class.
    /// </summary>
    /// <remarks>
    /// Lives across scene loads (a gameplay take runs from the Main Menu into the match). Any exception inside
    /// an action stops the sequence and runs the same teardown as an abort, so a failed take never leaves a
    /// recording open or the game in slow motion.
    /// </remarks>
    public class CaptureDirector : MonoBehaviour
    {
        private static CaptureDirector s_instance;

        public static CaptureDirector Instance => s_instance;

        public static CaptureDirector Ensure()
        {
            if (s_instance != null) return s_instance;

            GameObject go = new GameObject("[CaptureDirector]");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<CaptureDirector>();
            return s_instance;
        }

        public CaptureContext Context { get; private set; }

        /// <summary>The director's own clock: capture time since it was created (see <see cref="CaptureTime"/>).</summary>
        public float Clock { get; private set; }

        public bool IsRunning { get; private set; }
        public string SequenceName { get; private set; } = "";
        public string CurrentStep { get; private set; } = "";
        public string LastError { get; private set; } = "";
        public int CompletedSequences { get; private set; }

        public event Action<string> SequenceFinished;

        private Coroutine _running;

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            Context = new CaptureContext(this);
        }

        private void Update() => Clock += CaptureTime.Delta;

        /// <summary>Starts a sequence. Refused (false) while another is running.</summary>
        public bool Play(string sequenceName, IEnumerable<ICaptureAction> actions)
        {
            if (IsRunning)
            {
                Context.Log($"Refused '{sequenceName}': '{SequenceName}' is still running.");
                return false;
            }

            SequenceName = sequenceName;
            LastError = "";
            IsRunning = true;
            _running = StartCoroutine(RunSequence(new List<ICaptureAction>(actions)));
            return true;
        }

        public void Abort(string reason = "aborted")
        {
            if (!IsRunning) return;
            if (_running != null) StopCoroutine(_running);
            Context.Finger.Release();
            Finish(reason);
        }

        /// <summary>One line for a CLI poll.</summary>
        public string Status =>
            $"{(IsRunning ? "RUNNING" : "IDLE")} | {SequenceName} | step: {CurrentStep} | clock {Clock:0.0}s" +
            $" | video {Context.VideoTime:0.0}s | done {CompletedSequences}" + (LastError.Length > 0 ? $" | ERROR {LastError}" : "");

        private IEnumerator RunSequence(List<ICaptureAction> actions)
        {
            Context.Log($"=== {SequenceName}: {actions.Count} steps");

            foreach (ICaptureAction action in actions)
            {
                if (action == null) continue;

                CurrentStep = action.Describe();
                Context.Log(CurrentStep);

                IEnumerator routine = Guard(action.Run(Context));
                while (true)
                {
                    if (!routine.MoveNext()) break;
                    yield return routine.Current;
                }

                if (LastError.Length > 0)
                {
                    Finish("failed");
                    yield break;
                }
            }

            Finish("finished");
        }

        /// <summary>
        /// Flattens nested enumerators (an action may yield another action's routine) and turns an exception
        /// thrown anywhere inside into <see cref="LastError"/> instead of a silently dead coroutine.
        /// </summary>
        private IEnumerator Guard(IEnumerator root)
        {
            Stack<IEnumerator> stack = new();
            stack.Push(root);

            while (stack.Count > 0)
            {
                IEnumerator top = stack.Peek();
                bool moved;
                object current = null;

                try
                {
                    moved = top.MoveNext();
                    if (moved) current = top.Current;
                }
                catch (Exception e)
                {
                    LastError = $"{CurrentStep}: {e.GetType().Name}: {e.Message}";
                    Debug.LogException(e);
                    yield break;
                }

                if (!moved)
                {
                    stack.Pop();
                    continue;
                }

                if (current is IEnumerator nested)
                {
                    stack.Push(nested);
                    continue;
                }

                yield return current;
            }
        }

        private void Finish(string how)
        {
            // Teardown that must hold whatever the sequence was doing when it stopped.
            if (Time.timeScale != 1f && how != "finished") Time.timeScale = 1f;

            ICaptureRecorder recorder = Context.Recorder;
            if (recorder != null && recorder.IsRecording && how != "finished")
            {
                recorder.End();
                Context.NotifyRecordingStopped();
            }

            Context.Log($"=== {SequenceName} {how}{(LastError.Length > 0 ? ": " + LastError : "")}");
            IsRunning = false;
            CurrentStep = "";
            _running = null;
            CompletedSequences++;
            SequenceFinished?.Invoke(SequenceName);
        }

        private void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }
    }
}
#endif
