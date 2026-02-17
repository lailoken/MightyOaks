using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace MightyOaks
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class MightyOaksPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.marius.mightyoaks";
        public const string PluginName = "MightyOaks";
        public const string PluginVersion = "1.0.0";

        private static ConfigEntry<float> ScalingChance;
        private static ConfigEntry<float> MinScale;
        private static ConfigEntry<float> MaxScale;
        private static ConfigEntry<bool> MakeInvulnerable;
        private static ConfigEntry<bool> Enabled;
        private static BepInEx.Logging.ManualLogSource Logger;

        private void Awake()
        {
            Logger = base.Logger;
            Enabled = Config.Bind("General", "Enabled", true, "Enable the plugin.");
            ScalingChance = Config.Bind("General", "ScalingChance", 10f, "Chance (0-100) to scale an Oak tree.");
            MinScale = Config.Bind("General", "MinScale", 1f, "Minimum scale factor.");
            MaxScale = Config.Bind("General", "MaxScale", 12f, "Maximum scale factor.");
            MakeInvulnerable = Config.Bind("General", "MakeInvulnerable", true, "Make scaled trees invulnerable.");

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
                    if (Random.Range(0f, 100f) <= ScalingChance.Value)
                    {
                        float scale = Random.Range(MinScale.Value, MaxScale.Value);
                        zdo.Set(scaleFactorHash, scale);
                        ApplyScale(__instance, scale);
                    }
                    else
                    {
                        // Mark as normal (1.0) so we don't check again
                        zdo.Set(scaleFactorHash, 1f);
                    }
                }
            }
        }

        private static void ApplyScale(ZNetView view, float scale)
        {
            if (scale <= 1.01f) return;

            Logger.LogInfo($"Applying scale {scale} to Oak tree.");
            view.transform.localScale = Vector3.one * scale;

            if (MakeInvulnerable.Value)
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
