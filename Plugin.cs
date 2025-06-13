using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using LethalConfig;
using LethalConfig.ConfigItems;
using LethalConfig.ConfigItems.Options;
using LethalLib.Modules;
using SCP939.Scripts;
using SCP939.Utils;
using UnityEngine;

namespace SCP939;

[BepInPlugin(GUID, NAME, VERSION)]
public class SCP939Plugin : BaseUnityPlugin
{
    private const string GUID = "project_scp.scp939";
    private const string NAME = "SCP939";
    private const string VERSION = "1.0.1";

    public GameObject SCP939GameObject;

    public static SCP939Plugin instance;

    public List<SCP939EnemyAI> Scp939EnemyAisSpawned = new List<SCP939EnemyAI>();

    public ConfigEntry<bool> debug;

    public ConfigEntry<string> spawnMoonRarity;
    public ConfigEntry<int> maxSpawn;
    public ConfigEntry<int> powerLevel;

    public ConfigEntry<int> damage;

    public ConfigEntry<float> walkSpeed;
    public ConfigEntry<float> runSpeed;

    public ConfigEntry<float> minVoiceLineDelay;
    public ConfigEntry<float> maxVoiceLineDelay;

    public bool isMirageInstalled = false;


    private void Awake()
    {
        instance = this;

        Logger.LogInfo("SCP939 starting....");

        var assetDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "scp939");
        var bundle = AssetBundle.LoadFromFile(assetDir);

        Logger.LogInfo("SCP939 bundle found !");

        if (Chainloader.PluginInfos.ContainsKey("qwbarch.Mirage"))
        {
            Debug.Log("Mirage mod found !");
            isMirageInstalled = true;
        }

        NetcodePatcher();
        LoadConfigs();
        RegisterMonster(bundle);


        Logger.LogInfo("SCP939 is ready!");
    }

    private string RarityString(int rarity)
    {
        return
            $"Modded:{rarity},ExperimentationLevel:{rarity},AssuranceLevel:{rarity},VowLevel:{rarity},OffenseLevel:{rarity},MarchLevel:{rarity},RendLevel:{rarity},DineLevel:{rarity},TitanLevel:{rarity},Adamance:{rarity},Embrion:{rarity},Artifice:{rarity}";
    }

    private void LoadConfigs()
    {
        //GENERAL
        spawnMoonRarity = Config.Bind("General", "SpawnRarity",
            "Modded:50,ExperimentationLevel:40,AssuranceLevel:40,VowLevel:40,OffenseLevel:45,MarchLevel:45,RendLevel:50,DineLevel:50,TitanLevel:60,Adamance:45,Embrion:50,Artifice:60",
            "Chance for SCP 939 to spawn for any moon, example => assurance:100,offense:50 . You need to restart the game.");
        CreateStringConfig(spawnMoonRarity, true);

        maxSpawn = Config.Bind("General", "maxSpawn", 4,
            "Max SCP939 spawn in one day");
        CreateIntConfig(maxSpawn, 1, 30);

        powerLevel = Config.Bind("General", "powerLevel", 1,
            "SCP939 power level");
        CreateIntConfig(maxSpawn, 1, 10);

        //BEHAVIOR

        damage = Config.Bind("Behavior", "damage", 20,
            "SCP939 damage");
        CreateIntConfig(damage);

        walkSpeed = Config.Bind("Behavior", "walkSpeed", 3.5f,
            "SCP939 walk speed");
        CreateFloatConfig(walkSpeed, 1, 20);

        runSpeed = Config.Bind("Behavior", "runSpeed", 6.5f,
            "SCP939 run speed");
        CreateFloatConfig(runSpeed, 1, 30);

        //VOICES
        minVoiceLineDelay = Config.Bind("Voices", "minVoiceLineDelay", 20f,
            "Min voiceline delay");
        CreateFloatConfig(minVoiceLineDelay, 1, 300);

        maxVoiceLineDelay = Config.Bind("Voices", "maxVoiceLineDelay", 20f,
            "Max voiceline delay");
        CreateFloatConfig(maxVoiceLineDelay, 1, 300);

        //DEV
        debug = Config.Bind("Dev", "Debug", false, "Enable debug logs");
        CreateBoolConfig(debug);
    }

    void RegisterMonster(AssetBundle bundle)
    {
        //creature
        EnemyType creature = bundle.LoadAsset<EnemyType>("Assets/LethalCompany/Mods/SCP939/SCP939.asset");
        TerminalNode terminalNode =
            bundle.LoadAsset<TerminalNode>("Assets/LethalCompany/Mods/SCP939/SCP939TerminalNode.asset");
        TerminalKeyword terminalKeyword =
            bundle.LoadAsset<TerminalKeyword>("Assets/LethalCompany/Mods/SCP939/SCP939TerminalKeyword.asset");

        creature.MaxCount = maxSpawn.Value;
        creature.PowerLevel = powerLevel.Value;

        Logger.LogInfo($"{creature.name} FOUND");
        Logger.LogInfo($"{creature.enemyPrefab} prefab");
        NetworkPrefabs.RegisterNetworkPrefab(creature.enemyPrefab);
        Utilities.FixMixerGroups(creature.enemyPrefab);

        SCP939GameObject = creature.enemyPrefab;


        RegisterUtil.RegisterEnemyWithConfig(spawnMoonRarity.Value, creature, terminalNode, terminalKeyword,
            creature.PowerLevel, creature.MaxCount);
    }

    /// <summary>
    ///     Slightly modified version of:
    ///     https://github.com/EvaisaDev/UnityNetcodePatcher?tab=readme-ov-file#preparing-mods-for-patching
    /// </summary>
    private static void NetcodePatcher()
    {
        Type[] types;
        try
        {
            types = Assembly.GetExecutingAssembly().GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            // This goofy try catch is needed here to be able to use soft dependencies in the future, though none are present at the moment.
            types = e.Types.Where(type => type != null).ToArray();
        }

        foreach (var type in types)
        foreach (var method in type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            if (method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false).Length > 0)
                // Do weird magic...
                _ = method.Invoke(null, null);
    }

    private void CreateFloatConfig(ConfigEntry<float> configEntry, float min = 0f, float max = 100f)
    {
        var exampleSlider = new FloatSliderConfigItem(configEntry, new FloatSliderOptions
        {
            Min = min,
            Max = max,
            RequiresRestart = false
        });
        LethalConfigManager.AddConfigItem(exampleSlider);
    }

    private void CreateIntConfig(ConfigEntry<int> configEntry, int min = 0, int max = 100)
    {
        var exampleSlider = new IntSliderConfigItem(configEntry, new IntSliderOptions
        {
            Min = min,
            Max = max,
            RequiresRestart = false
        });
        LethalConfigManager.AddConfigItem(exampleSlider);
    }

    private void CreateStringConfig(ConfigEntry<string> configEntry, bool requireRestart = false)
    {
        var exampleSlider = new TextInputFieldConfigItem(configEntry, new TextInputFieldOptions
        {
            RequiresRestart = requireRestart
        });
        LethalConfigManager.AddConfigItem(exampleSlider);
    }

    public bool StringContain(string name, string verifiedName)
    {
        var name1 = name.ToLower();
        while (name1.Contains(" ")) name1 = name1.Replace(" ", "");

        var name2 = verifiedName.ToLower();
        while (name2.Contains(" ")) name2 = name2.Replace(" ", "");

        return name1.Contains(name2);
    }

    private void CreateBoolConfig(ConfigEntry<bool> configEntry)
    {
        var exampleSlider = new BoolCheckBoxConfigItem(configEntry, new BoolCheckBoxOptions
        {
            RequiresRestart = false
        });
        LethalConfigManager.AddConfigItem(exampleSlider);
    }
}