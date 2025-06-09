using System;
using GameNetcodeStuff;
using LethalLib.Modules;
using UnityEngine;

namespace SCP939.Scripts;

public class SCP939Fog : MonoBehaviour
{
    private float addDrunkTimer = 0f;
    private float addDrunkDelay = 0.25f;

    private void Update()
    {
        addDrunkTimer -= Time.deltaTime;
    }

    private void OnTriggerStay(Collider other)
    {
        if (addDrunkTimer >= 0) return;
        if (other.CompareTag("Player"))
        {
            var player = other.GetComponent<PlayerControllerB>();
            if (player == null || player.isPlayerDead) return;
            addDrunkTimer = addDrunkDelay;
            player.drunkness += 0.1f;
        }
    }
}