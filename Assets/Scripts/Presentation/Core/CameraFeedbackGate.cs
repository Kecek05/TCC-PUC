using MoreMountains.Feedbacks;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// The one switch between game feel and the camera. Feel's camera feedbacks broadcast events that the
/// shakers on this camera pick up, so gating the shakers here silences every shake and zoom punch in the
/// game at once — no feedback has to check the setting itself.
/// </summary>
/// <remarks>
/// Lives on the Main Camera, which is a child of the camera rig: <c>CameraSlide</c> and the end cutscene move
/// the rig, the position shaker only ever offsets the camera locally, so a shake can never fight a slide.
/// Zoom punches drive <see cref="Camera.orthographicSize"/> directly, which the end cutscene also owns while
/// it frames its target — hence the second, separate hold on them.
/// </remarks>
public class CameraFeedbackGate : MonoBehaviour
{
    [Title("Shakers")]
    [SerializeField, Required] private MMCameraShaker positionShaker;
    [SerializeField, Required] private MMCameraOrthographicSizeShaker zoomShaker;

    private BaseGameSettingsService _settings;
    private bool _zoomSuppressed;

    // MMShaker starts listening in its own Awake, which has run by the time Start applies anything here.
    private bool _zoomListening = true;

    private void Awake()
    {
        ServiceLocator.Register(this);
    }

    private void Start()
    {
        // Absent in scenes launched without the boot flow; everything then simply stays on.
        if (ServiceLocator.TryGet(out _settings))
            _settings.OnSettingsChanged += Apply;

        Apply();
    }

    private void OnDestroy()
    {
        if (_settings != null) _settings.OnSettingsChanged -= Apply;
        ServiceLocator.Unregister<CameraFeedbackGate>();
    }

    /// <summary>
    /// Holds zoom punches off while something else owns the camera's size. A punch already running is
    /// stopped, which hands the size back to what it was before the punch.
    /// </summary>
    public void SetZoomPunchesSuppressed(bool suppressed)
    {
        _zoomSuppressed = suppressed;
        Apply();
    }

    private void Apply()
    {
        bool shakeAllowed = _settings == null || _settings.ScreenShakeEnabled;

        // MMCameraShaker registers for its event in OnEnable, so disabling it is what stops it listening.
        if (positionShaker != null) positionShaker.enabled = shakeAllowed;

        SetZoomListening(shakeAllowed && !_zoomSuppressed);
    }

    private void SetZoomListening(bool listen)
    {
        if (zoomShaker == null || _zoomListening == listen) return;

        _zoomListening = listen;

        if (listen)
        {
            zoomShaker.StartListening();
            return;
        }

        zoomShaker.StopListening();
        if (zoomShaker.Shaking) zoomShaker.Stop();
    }
}
