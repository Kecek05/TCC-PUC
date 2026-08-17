using System.Threading.Tasks;

using UnityEngine;

public abstract class BaseHostManager : MonoBehaviour
{
    public HostConnectionData CurrentHostConnectionData { get; protected set; }
    
    public abstract Task<bool> StartHostAsync();

    /// <summary>
    /// Stop advertising the match to new players (stop the lobby heartbeat + delete the discovery lobby)
    /// while keeping the current host session running. Called when the match commits (2nd human joins or
    /// a bot fills the slot). Idempotent. Default no-op for stand-ins.
    /// </summary>
    public virtual void CloseLobbyToNewPlayers() { }

    /// <summary>
    /// Deletes the lobby, shuts down the host's NetworkManager and completes only
    /// once Netcode has fully stopped, so a subsequent StartHost (replay) begins
    /// from a clean state. Does not change scene.
    /// </summary>
    public abstract Task ShutdownHostAsync();
}
