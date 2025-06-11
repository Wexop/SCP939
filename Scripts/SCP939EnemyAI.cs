using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

namespace SCP939.Scripts;

public class SCP939EnemyAI : EnemyAI
{
    private static readonly int Run = Animator.StringToHash("run");
    private static readonly int Attack = Animator.StringToHash("attack");
    private static readonly int Hit = Animator.StringToHash("hit");
    private static readonly int Die = Animator.StringToHash("die");

    public List<AudioClip> walkSounds;

    public GameObject fogObject;
    public List<ParticleSystem> smokeParticles;
    public AudioClip biteClip;
    public List<AudioClip> voiceLinesClips;

    public bool isChief;

    private float runSpeed = 6.5f;
    private float walkSpeed = 3.5f;

    private int damage = 20;

    private float aiInterval = 0.2f;
    private int lastBehaviorState;
    private readonly float walkSoundDelayRun = 0.5f;
    private readonly float walkSoundDelayWalk = 0.9f;

    private float walkSoundTimer;

    private int soundHeard;
    private int maxSoundHeard = 3;
    private float heardLastSoundTimer = 0f;
    private float heardLastSoundDelay = 3f;

    private float disableHeardSoundTimer = 0f;
    private float disableHeardSoundDelay = 1.5f;

    private float hitPlayerTimer = 0f;
    private float hitPlayerDelay = 1f;

    private float makeSmokeTimer = 0f;
    private float makeSmokeDelay = 30f;

    private float detectPlayerTimer = 0f;
    private float detectPlayerDelay = 2f;

    private float playRandomVoiceTimer = 10f;

    private float disabledTimer = 2f;

    private Vector3 lastSearchPosition;
    private Vector3 lastNoisePosition;

    private void SpawnAFriend()
    {
        if (SCP939Plugin.instance.Scp939EnemyAisSpawned.Count % 2 == 0) return;
        var friend = Instantiate(SCP939Plugin.instance.SCP939GameObject, transform.position, Quaternion.identity);
        friend.GetComponent<NetworkObject>().Spawn();
        RoundManager.Instance.SpawnedEnemies.Add(friend.GetComponent<EnemyAI>());
    }

    [ClientRpc]
    private void SyncInformationClientRpc(float walk, float run, int dmg)
    {
        walkSpeed = walk;
        runSpeed = run;
        damage = dmg;
    }

    public override void Start()
    {
        base.Start();

        disabledTimer = Random.Range(0f, 2f);

        SCP939Plugin.instance.Scp939EnemyAisSpawned.Add(this);

        agent.speed = walkSpeed;
        agent.acceleration = 255f;
        agent.angularSpeed = 900f;

        if (IsServer)
        {
            SpawnAFriend();
            if (GetChief() == null)
            {
                isChief = true;
                SetChiefServerRpc(true);
            }

            SyncInformationClientRpc(SCP939Plugin.instance.walkSpeed.Value, SCP939Plugin.instance.runSpeed.Value,
                SCP939Plugin.instance.damage.Value);
        }
    }

    public override void Update()
    {
        base.Update();

        if (isEnemyDead) return;

        heardLastSoundTimer -= Time.deltaTime;
        hitPlayerTimer -= Time.deltaTime;
        disableHeardSoundTimer -= Time.deltaTime;
        makeSmokeTimer -= Time.deltaTime;
        detectPlayerTimer -= Time.deltaTime;
        playRandomVoiceTimer -= Time.deltaTime;
        disabledTimer -= Time.deltaTime;

        if (detectPlayerTimer < 0)
        {
            targetPlayer = null;
        }

        if (heardLastSoundTimer < 0f && soundHeard > 0)
        {
            if (SCP939Plugin.instance.debug.Value)
                Debug.Log($"Reduce sound heard : {soundHeard}");
            soundHeard--;
            heardLastSoundTimer = heardLastSoundDelay;
        }

        if (lastBehaviorState != currentBehaviourStateIndex)
        {
            if (SCP939Plugin.instance.debug.Value)
                Debug.Log($"New behavior state : {currentBehaviourStateIndex} last : {lastBehaviorState}");
            lastBehaviorState = currentBehaviourStateIndex;
            AllClientOnSwitchBehaviorState();
        }

        walkSoundTimer -= Time.deltaTime;

        //WALKSOUNDS
        if (walkSoundTimer <= 0f)
        {
            var randomSound = walkSounds[Random.Range(0, walkSounds.Count)];
            creatureSFX.PlayOneShot(randomSound);
            walkSoundTimer = currentBehaviourStateIndex == 2 ? walkSoundDelayRun : walkSoundDelayWalk;
        }

        if (!IsServer) return;

        if (playRandomVoiceTimer < 0)
        {
            PlayRandomVoiceLineServerRpc();
            playRandomVoiceTimer = Random.Range(SCP939Plugin.instance.minVoiceLineDelay.Value,
                SCP939Plugin.instance.maxVoiceLineDelay.Value);
        }

        if (aiInterval <= 0)
        {
            aiInterval = AIIntervalTime;
            DoAIInterval();
        }

        //RUNNING STATE
        if (currentBehaviourStateIndex == 2 && !targetPlayer)
        {
            if (agent.remainingDistance <= agent.stoppingDistance)
            {
                lastSearchPosition = GetClosePositionToPosition(lastNoisePosition, 4);
                SetDestinationToPosition(lastSearchPosition, true);
            }
        }
    }

    public override void DetectNoise(Vector3 noisePosition, float noiseLoudness, int timesPlayedInOneSpot = 0,
        int noiseID = 0)
    {
        if (isEnemyDead) return;
        if (disableHeardSoundTimer >= 0 && currentBehaviourStateIndex != 2) return;
        base.DetectNoise(noisePosition, noiseLoudness, timesPlayedInOneSpot, noiseID);
        if (!IsServer) return;

        if (noiseLoudness < 0.25) return;

        if (soundHeard < maxSoundHeard)
        {
            soundHeard++;
        }

        heardLastSoundTimer = heardLastSoundDelay;
        lastNoisePosition = noisePosition;
        if (SCP939Plugin.instance.debug.Value)
            Debug.Log($"DETECT sound : {soundHeard}");
        CallOther939();
        disableHeardSoundTimer = disableHeardSoundDelay;
    }

    public override void DoAIInterval()
    {
        if (isEnemyDead || disabledTimer > 0) return;
        base.DoAIInterval();

        switch (currentBehaviourStateIndex)
        {
            //roaming
            case 0:
            {
                if (soundHeard <= 0)
                {
                    if (isChief)
                    {
                        if (currentSearch.inProgress) break;
                        var aiSearchRoutine = new AISearchRoutine();
                        aiSearchRoutine.searchWidth = Random.Range(25f, 60f);
                        aiSearchRoutine.searchPrecision = 8f;
                        StartSearch(ChooseFarthestNodeFromPosition(transform.position, true).position, aiSearchRoutine);
                    }
                    else
                    {
                        if (agent.remainingDistance <= agent.stoppingDistance)
                        {
                            var chief = GetChief();
                            SetDestinationToPosition(GetClosePositionToPosition(chief.transform.position, 10), true);
                        }
                    }
                }
                else
                {
                    StopSearch(currentSearch);
                    SwitchToBehaviourState(1);
                }

                break;
            }
            //heard noise
            case 1:
            {
                SetDestinationToPosition(lastNoisePosition);

                if (Vector3.Distance(lastNoisePosition, transform.position) <= 0.1)
                {
                    SwitchToBehaviourState(0);
                    if (makeSmokeTimer <= 0f)
                    {
                        InstantiateFogServerRpc();
                    }
                }
                else if (soundHeard > 1)
                {
                    SetDestinationToPosition(lastNoisePosition);
                    SwitchToBehaviourState(2);
                }


                break;
            }
            //run to noise
            case 2:
            {
                if (targetPlayer)
                {
                    SetMovingTowardsTargetPlayer(targetPlayer);
                }
                else if (soundHeard <= 0)
                {
                    SwitchToBehaviourState(0);
                }

                break;
            }
        }
    }

    private void AllClientOnSwitchBehaviorState()
    {
        switch (currentBehaviourStateIndex)
        {
            case 0:
            {
                creatureAnimator.SetBool(Run, false);
                agent.speed = walkSpeed;
                break;
            }
            case 1:
            {
                creatureAnimator.SetBool(Run, false);
                agent.speed = walkSpeed;
                break;
            }
            case 2:
            {
                creatureAnimator.SetBool(Run, true);
                agent.speed = runSpeed;
                break;
            }
        }
    }

    private static SCP939EnemyAI GetChief()
    {
        SCP939EnemyAI chief = null;

        SCP939Plugin.instance.Scp939EnemyAisSpawned.ForEach(m =>
        {
            if (m.isChief) chief = m;
        });

        return chief;
    }

    [ServerRpc]
    public void SetChiefServerRpc(bool value)
    {
        SetChiefClientRpc(value);
    }

    [ClientRpc]
    private void SetChiefClientRpc(bool value)
    {
        if (SCP939Plugin.instance.debug.Value) Debug.Log($"New chief : {value}");
        isChief = value;
    }

    [ServerRpc]
    private void InstantiateFogServerRpc()
    {
        makeSmokeTimer = makeSmokeDelay;
        InstantiateFogClientRpc();
    }

    [ClientRpc]
    private void InstantiateFogClientRpc()
    {
        var fog = Instantiate(fogObject, transform.position, Quaternion.identity);
        smokeParticles.ForEach(p => { p.Play(); });
        StartCoroutine(SmokeAnimation(fog));
    }

    [ServerRpc]
    private void PlayRandomVoiceLineServerRpc()
    {
        PlayRandomVoiceLineClientRpc(Random.Range(0, voiceLinesClips.Count));
    }

    [ClientRpc]
    private void PlayRandomVoiceLineClientRpc(int index)
    {
        creatureVoice.PlayOneShot(voiceLinesClips[index]);
    }

    private IEnumerator SmokeAnimation(GameObject fog)
    {
        yield return new WaitForSeconds(2f);
        smokeParticles.ForEach(p => { p.Stop(); });

        yield return new WaitForSeconds(8.5f);
        Destroy(fog);
    }

    private void CallOther939()
    {
        if (soundHeard <= 0) return;
        var all939 = GetClose939();
        all939.ForEach(scp => { scp.GetCalledByAnother939(lastNoisePosition, soundHeard); });
    }

    private void GetCalledByAnother939(Vector3 position, int soundCount)
    {
        if (currentBehaviourStateIndex == 2 && soundCount < 2) return;

        if (SCP939Plugin.instance.debug.Value)
            Debug.Log($"939 CALLED FROM ANOTHER : {soundCount}, {position}");
        var randomPosAround = GetClosePositionToPosition(position, 2);

        lastNoisePosition = randomPosAround;
        soundHeard = soundCount;
        disableHeardSoundTimer = disableHeardSoundDelay;
        heardLastSoundTimer = heardLastSoundDelay;
    }

    private Vector3 GetClosePositionToPosition(Vector3 position, float maxDistance = 1)
    {
        return position + new Vector3(Random.Range(-maxDistance, maxDistance), 0,
            Random.Range(-maxDistance, maxDistance));
    }

    private List<SCP939EnemyAI> GetClose939()
    {
        var list = new List<SCP939EnemyAI>() { };

        SCP939Plugin.instance.Scp939EnemyAisSpawned.ToList().ForEach(s =>
        {
            if (Vector3.Distance(s.transform.position, transform.position) < 50 && s != this)
            {
                list.Add(s);
            }
        });

        return list;
    }

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false,
        int hitID = -1)
    {
        if (isEnemyDead) return;

        creatureAnimator.SetTrigger(Hit);
        base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);
        enemyHP -= force;
        TargetClosestPlayer();
        detectPlayerTimer = detectPlayerDelay;
        soundHeard = maxSoundHeard;
        DetectNoise(playerWhoHit.transform.position, 1);
        if (enemyHP <= 0)
        {
            KillEnemy();
        }
    }

    public override void KillEnemy(bool destroy = false)
    {
        base.KillEnemy(destroy);
        creatureAnimator.SetBool(Run, false);
        creatureAnimator.SetBool(Die, true);
        SCP939Plugin.instance.Scp939EnemyAisSpawned.Remove(this);
        if (!IsServer) return;
        if (!isChief) return;
        SetChiefServerRpc(false);
        bool foundChief = false;
        SCP939Plugin.instance.Scp939EnemyAisSpawned.ForEach(m =>
        {
            if (foundChief) return;
            if (!m.isEnemyDead)
            {
                m.SetChiefServerRpc(true);
                foundChief = true;
            }
        });
    }

    public override void OnCollideWithPlayer(Collider other)
    {
        if (isEnemyDead) return;

        if (hitPlayerTimer >= 0) return;
        creatureAnimator.SetTrigger(Attack);
        creatureVoice.PlayOneShot(biteClip);
        base.OnCollideWithPlayer(other);
        TargetClosestPlayer();
        detectPlayerTimer = detectPlayerDelay;
        soundHeard = maxSoundHeard;
        DetectNoise(other.transform.position, 1);
        hitPlayerTimer = hitPlayerDelay;
        var player = MeetsStandardPlayerCollisionConditions(other);
        if (player)
        {
            player.DamagePlayer(damage);
        }
    }

    public override void OnCollideWithEnemy(Collider other, EnemyAI collidedEnemy = null)
    {
        if (isEnemyDead) return;
        if (hitPlayerTimer >= 0) return;
        base.OnCollideWithEnemy(other, collidedEnemy);
        if (collidedEnemy != null)
        {
            if (collidedEnemy.enemyType.enemyName == "SCP 939" || collidedEnemy.isEnemyDead) return;
            creatureAnimator.SetTrigger(Attack);
            collidedEnemy?.HitEnemy(force: 2, playHitSFX: true);
            hitPlayerTimer = hitPlayerDelay;
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        SCP939Plugin.instance.Scp939EnemyAisSpawned.Remove(this);
    }
}