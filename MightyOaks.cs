using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace MightyOaks
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class MightyOaksPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.lailoken.mightyoaks";
        public const string PluginName = "MightyOaks";
        public const string PluginVersion = "1.0.0";

        private static ConfigEntry<float> ScalingChance;
        private static ConfigEntry<float> MinScale;
        private static ConfigEntry<float> MaxScale;
        private static ConfigEntry<float> ScaleExponent;
        private static ConfigEntry<bool> ScaleToughness;
        private static ConfigEntry<bool> MakeInvulnerable;
        private static ConfigEntry<float> InvulnerabilityThreshold;
        private static ConfigEntry<bool> Enabled;
        private static BepInEx.Logging.ManualLogSource _Logger;

        private void Awake()
        {
            _Logger = base.Logger;
            Enabled = Config.Bind("General", "Enabled", true, "Enable the plugin.");
            ScalingChance = Config.Bind("General", "ScalingChance", 25f, "Chance (0-100) to scale an Oak tree.");
            MinScale = Config.Bind("General", "MinScale", 1f, "Minimum scale factor.");
            MaxScale = Config.Bind("General", "MaxScale", 12f, "Maximum scale factor.");
            ScaleExponent = Config.Bind("General", "ScaleExponent", 2.0f, "Exponent for scale distribution. 1.0 is linear (uniform). Higher values (e.g. 2.0, 3.0) make large trees rarer.");
            ScaleToughness = Config.Bind("General", "ScaleToughness", true, "If true, scales the tree's health/toughness along with its size.");
            MakeInvulnerable = Config.Bind("General", "MakeInvulnerable", true, "Enable invulnerability for trees above a certain size.");
            InvulnerabilityThreshold = Config.Bind("General", "InvulnerabilityThreshold", 2.0f, "Scale threshold above which trees become invulnerable (if MakeInvulnerable is true).");

            Harmony.CreateAndPatchAll(System.Reflection.Assembly.GetExecutingAssembly(), PluginGUID);
        }

        [HarmonyPatch(typeof(ZNetView), "Awake")]
        static class ZNetView_Awake_Patch
        {
            static void Postfix(ZNetView __instance)
            {
                if (!Enabled.Value || !__instance || !__instance.IsValid()) return;

                // Check if we have a valid ZDO
                ZDO zdo = __instance.GetZDO();
                if (zdo == null) return;

                // Identify if this is an Oak tree
                // We use the prefab name from the ZDO if possible, or check the game object name
                string prefabName = "";
                if (ZNetScene.instance)
                {
                    GameObject prefab = ZNetScene.instance.GetPrefab(zdo.GetPrefab());
                    if (prefab) prefabName = prefab.name;
                }
                
                // Fallback to game object name if ZNetScene lookup fails (e.g. during early init)
                if (string.IsNullOrEmpty(prefabName))
                {
                    prefabName = __instance.gameObject.name.Replace("(Clone)", "");
                }

                if (prefabName != "Oak1") return;

                int scaleFactorHash = "OakScaleFactor".GetStableHashCode();
                float currentScale = zdo.GetFloat(scaleFactorHash, 0f);

                if (currentScale > 0.1f)
                {
                    // Already has a scale (or marked as 1.0)
                    ApplyScale(__instance, currentScale);
                    return;
                }

                // If not checked yet and we are the owner, roll for scale
                if (zdo.IsOwner())
                {
                    // Use deterministic RNG based on World Seed and Tree Position
                    int worldSeed = 0;
                    
                    if (WorldGenerator.instance != null)
                    {
                         var world = Traverse.Create(WorldGenerator.instance).Field("m_world").GetValue<World>();
                         if (world != null) worldSeed = world.m_seed;
                    }
                    
                    if (worldSeed == 0 && ZNet.instance != null)
                    {
                        var world = Traverse.Create(ZNet.instance).Field("m_world").GetValue<World>();
                        if (world != null) worldSeed = world.m_seed;
                    }

                    // Ensure non-zero seed to avoid issues (though 0 is valid seed, but usually not default)
                    if (worldSeed == 0) worldSeed = 123456; 

                    Vector3 pos = __instance.transform.position;
                    // Create a unique seed for this tree using simple hash
                    int treeSeed = worldSeed + (int)(pos.x * 1000) + (int)(pos.z * 1000);
                    
                    // Save old RNG state
                    UnityEngine.Random.State oldState = UnityEngine.Random.state;
                    UnityEngine.Random.InitState(treeSeed);

                    float scale = 1f;
                    if (UnityEngine.Random.Range(0f, 100f) <= ScalingChance.Value)
                    {
                        // Use power distribution to make larger trees rarer
                        // randomT (0.0 to 1.0)
                        float randomT = UnityEngine.Random.value;
                        
                        // Apply exponent: 
                        // If Exponent > 1.0, results are biased towards 0.0 (smaller scale)
                        // If Exponent < 1.0, results are biased towards 1.0 (larger scale)
                        float biasedT = Mathf.Pow(randomT, ScaleExponent.Value);
                        
                        // Calculate final scale using Lerp
                        scale = Mathf.Lerp(MinScale.Value, MaxScale.Value, biasedT);
                        ApplyScale(__instance, scale);
                    }
                    
                    // Restore RNG state
                    UnityEngine.Random.state = oldState;

                    // Save the scale to ZDO so it persists and syncs
                    zdo.Set(scaleFactorHash, scale);
                }
            }
        }

        private static void ApplyScale(ZNetView view, float scale)
        {
            if (scale <= 1.01f) return;

            _Logger.LogInfo($"Applying scale {scale} to Oak tree.");
            view.transform.localScale = Vector3.one * scale;

            if (ScaleToughness.Value)
            {
                var dest = view.GetComponent<Destructible>();
                if (dest)
                {
                    // Scale health non-linearly (e.g. area/volume based, roughly square of scale)
                    dest.m_health *= (scale * scale);
                }
                
                var tree = view.GetComponent<TreeBase>();
                if (tree)
                {
                    tree.m_health *= (scale * scale);
                }
            }

            if (MakeInvulnerable.Value && scale >= InvulnerabilityThreshold.Value)
            {
                // Make invulnerable
                var dest = view.GetComponent<Destructible>();
                if (dest)
                {
                    // Set extremely high tool tier requirement
                    dest.m_minToolTier = 1000;
                    dest.m_damages.m_chop = HitData.DamageModifier.Immune;
                    dest.m_damages.m_pickaxe = HitData.DamageModifier.Immune;
                }
                
                var tree = view.GetComponent<TreeBase>();
                if (tree)
                {
                    tree.m_minToolTier = 1000;
                    tree.m_damageModifiers.m_chop = HitData.DamageModifier.Immune;
                    tree.m_damageModifiers.m_pickaxe = HitData.DamageModifier.Immune;
                }
            }
        }
    }
}
