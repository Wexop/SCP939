using System;
using System.Linq;
using GameNetcodeStuff;
using Mirage.Unity;
using Unity.Netcode;
using UnityEngine;

namespace SCP939.Scripts;

public class SCP939MirageObject : NetworkBehaviour
{
    public ulong playerID;

    public MimicPlayer.MimicPlayer mimicPlayer;
    public AudioStream.AudioStream audioStream;

    public void Init(ulong player_Id)
    {
        if (SCP939Plugin.instance.debug.Value) Debug.Log($"INIT MIRAGE OBJECT FOR PLAYER : {player_Id}");

        playerID = player_Id;

        mimicPlayer = gameObject.AddComponent<Mirage.Unity.MimicPlayer.MimicPlayer>();
        gameObject.AddComponent<Mirage.Unity.MimicVoice.MimicVoice>();
        audioStream = gameObject.AddComponent<Mirage.Unity.AudioStream.AudioStream>();
        gameObject.AddComponent<Mirage.Unity.MaskedAnimator.MaskedAnimator>();

        SetPlayerMimicking(player_Id);

        NetworkObject.Spawn();
        SetPlayerIdServerRpc(player_Id);
    }

    private PlayerControllerB GetPlayerObject(ulong player_Id)
    {
        PlayerControllerB player = null;

        StartOfRound.Instance.allPlayerScripts.ToList().ForEach(p =>
        {
            if (p.playerClientId == player_Id) player = p;
        });

        return player;
    }

    private void SetPlayerMimicking(ulong player_Id)
    {
        var player = GetPlayerObject(player_Id);

        var mimicType = typeof(Mirage.Unity.MimicPlayer.MimicPlayer);

        var field = mimicType.GetField("mimickingPlayer",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (field != null)
        {
            field.SetValue(mimicPlayer, player);
            if (SCP939Plugin.instance.debug.Value) Debug.Log($"Value of mimicPlayer changed to {player_Id}");
        }
        else
        {
            Debug.LogError("Impossible to find 'MimickingPlayer' in MimicPlayer.");
        }
    }

    [ServerRpc]
    private void SetPlayerIdServerRpc(ulong player)
    {
        SetPlayerIdClientRpc(player);
    }

    [ClientRpc]
    private void SetPlayerIdClientRpc(ulong player_Id)
    {
        playerID = player_Id;
        audioStream.AudioSource.mute = true;
        SetPlayerMimicking(player_Id);
    }

    [ServerRpc(RequireOwnership = false)]
    public void EnableAudioServerRpc()
    {
        EnableAudioClientRpc();
    }

    [ClientRpc]
    private void EnableAudioClientRpc()
    {
        if (SCP939Plugin.instance.debug.Value) Debug.Log($"ENABLE MIMIC PLAYER FOR PLAYER {playerID}");
        audioStream.AudioSource.mute = false;
    }
}