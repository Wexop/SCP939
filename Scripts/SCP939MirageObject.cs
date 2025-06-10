using System;
using System.Linq;
using GameNetcodeStuff;
using Mirage.Unity;
using Unity.Netcode;

namespace SCP939.Scripts;

public class SCP939MirageObject : NetworkBehaviour
{
    public ulong playerID;

    public MimicPlayer.MimicPlayer mimicPlayer;
    public AudioStream.AudioStream audioStream;

    public void Init(ulong playerID)
    {
        if (!IsServer) return;

        mimicPlayer = gameObject.AddComponent<Mirage.Unity.MimicPlayer.MimicPlayer>();
        gameObject.AddComponent<Mirage.Unity.MimicVoice.MimicVoice>();
        audioStream = gameObject.AddComponent<Mirage.Unity.AudioStream.AudioStream>();
        gameObject.AddComponent<Mirage.Unity.MaskedAnimator.MaskedAnimator>();

        mimicPlayer.MimickingPlayer = GetPlayerObject(playerID);
        NetworkObject.Spawn();
        SetPlayerIdServerRpc(playerID);
    }

    private PlayerControllerB GetPlayerObject(ulong playerID)
    {
        PlayerControllerB player = null;

        StartOfRound.Instance.allPlayerScripts.ToList().ForEach(p =>
        {
            if (p.playerClientId == playerID) player = p;
        });

        return player;
    }

    [ServerRpc]
    private void SetPlayerIdServerRpc(ulong player)
    {
        SetPlayerIdClientRpc(player);
    }

    [ClientRpc]
    private void SetPlayerIdClientRpc(ulong player)
    {
        playerID = player;
        audioStream.AudioSource.mute = true;
    }
}