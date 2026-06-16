using Unity.Netcode;

public class ClientSlamTowerCombat : BaseClientTowerCombat
{
    /// <summary>
    /// Server → clients: play the slam pulse. Cosmetic only — the damage is applied server-side in
    /// <see cref="ServerSlamTowerCombat"/>. Placeholder: reuses the tower's shoot feedback until a
    /// dedicated shockwave VFX exists.
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    public void PlaySlamRpc()
    {
        if (clientTowerGFX != null) clientTowerGFX.FireBulletFeedback();
    }
}
