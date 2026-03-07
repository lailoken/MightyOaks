using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn;
using Jotunn.Utils;
using UnityEngine;
using System.Collections;

namespace MightyOaks
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class MightyOaksPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.lailoken.mightyoaks";
        public const string PluginName = "MightyOaks";
        public const string PluginVersion = "1.1.5";

        private static ConfigEntry<float> ScalingChance;
        private static ConfigEntry<float> MinScale;
        private static ConfigEntry<float> MaxScale;
        private static ConfigEntry<float> ScaleExponent;
        private static ConfigEntry<bool> ScaleToughness;
        private static ConfigEntry<bool> MakeInvulnerable;
        private static ConfigEntry<float> InvulnerabilityThreshold;
        private static ConfigEntry<bool> Enabled;
        private static BepInEx.Logging.ManualLogSource _Logger;
        private static readonly int OakScaleFactorHash = "OakScaleFactor".GetStableHashCode();

        // Server-side: kick clients that don't have MightyOaks (Jötunn only checks clients that have Jötunn)
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ZNetPeer, object> ValidatedPeers = new System.Runtime.CompilerServices.ConditionalWeakTable<ZNetPeer, object>();

        private void Awake()
        {
            _Logger = base.Logger;
            var synced = new ConfigurationManagerAttributes { IsAdminOnly = true };
            var chanceRange = new AcceptableValueRange<float>(0f, 100f);
            var scaleRange = new AcceptableValueRange<float>(0.1f, 100f);

            Enabled = Config.Bind("General", "Enabled", true, new ConfigDescription("Enable the plugin.", null, synced));
            ScalingChance = Config.Bind("General", "ScalingChance", 25f, new ConfigDescription("Chance (0-100) to scale an Oak tree.", chanceRange, synced));
            MinScale = Config.Bind("General", "MinScale", 1f, new ConfigDescription("Minimum scale factor.", scaleRange, synced));
            MaxScale = Config.Bind("General", "MaxScale", 12f, new ConfigDescription("Maximum scale factor.", scaleRange, synced));
            ScaleExponent = Config.Bind("General", "ScaleExponent", 2.0f, new ConfigDescription("Exponent for scale distribution. 1.0 is linear (uniform). Higher values (e.g. 2.0, 3.0) make large trees rarer.", scaleRange, synced));
            ScaleToughness = Config.Bind("General", "ScaleToughness", true, new ConfigDescription("If true, scales the tree's health/toughness along with its size.", null, synced));
            MakeInvulnerable = Config.Bind("General", "MakeInvulnerable", true, new ConfigDescription("Enable invulnerability for trees above a certain size.", null, synced));
            InvulnerabilityThreshold = Config.Bind("General", "InvulnerabilityThreshold", 2.0f, new ConfigDescription("Scale threshold above which trees become invulnerable (if MakeInvulnerable is true).", scaleRange, synced));

            Harmony.CreateAndPatchAll(System.Reflection.Assembly.GetExecutingAssembly(), PluginGUID);
        }

        [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
        static class ZNet_OnNewConnection_Patch
        {
            static void Postfix(ZNet __instance, ZNetPeer peer)
            {
                peer.m_rpc.Register<string>("MightyOaks_VersionCheck", RPC_MightyOaks_VersionCheck);

                if (!__instance.IsServer())
                {
                    // Client: tell server we have MightyOaks and our version
                    peer.m_rpc.Invoke("MightyOaks_VersionCheck", PluginVersion);
                }
                else
                {
                    // Server: client must prove they have the mod within timeout or we kick
                    __instance.StartCoroutine(VerifyPeerHasMod(peer));
                }
            }
        }

        private static IEnumerator VerifyPeerHasMod(ZNetPeer peer)
        {
            yield return new WaitForSeconds(10f);
            if (peer.m_socket.IsConnected() && !ValidatedPeers.TryGetValue(peer, out _))
            {
                _Logger.LogWarning($"Peer {peer.m_uid} did not send MightyOaks version (missing mod?). Kicking.");
                ZNet.instance.Disconnect(peer);
            }
        }

        private static void RPC_MightyOaks_VersionCheck(ZRpc rpc, string clientVersion)
        {
            if (!ZNet.instance.IsServer()) return;

            ZNetPeer peer = null;
            foreach (var p in ZNet.instance.GetPeers())
                if (p.m_rpc == rpc) { peer = p; break; }

            if (peer == null)
            {
                _Logger.LogWarning("Received MightyOaks version check from unknown peer.");
                return;
            }

            if (!IsVersionCompatible(clientVersion, PluginVersion))
            {
                _Logger.LogWarning($"Peer {peer.m_uid} has incompatible MightyOaks version {clientVersion} (server: {PluginVersion}). Kicking.");
                ZNet.instance.Disconnect(peer);
                return;
            }

            if (!ValidatedPeers.TryGetValue(peer, out _))
                ValidatedPeers.Add(peer, null);
        }

        private static bool IsVersionCompatible(string v1, string v2)
        {
            try
            {
                var ver1 = new System.Version(v1);
                var ver2 = new System.Version(v2);
                return ver1.Major == ver2.Major && ver1.Minor == ver2.Minor;
            }
            catch
            {
                return false;
            }
        }

        private static int GetWorldSeed()
        {
            if (WorldGenerator.instance != null)
            {
                var world = Traverse.Create(WorldGenerator.instance).Field("m_world").GetValue<World>();
                if (world != null) return world.m_seed;
            }
            if (ZNet.instance != null)
            {
                var world = Traverse.Create(ZNet.instance).Field("m_world").GetValue<World>();
                if (world != null) return world.m_seed;
            }
            return 0;
        }

        private static bool IsOak1(ZNetView view)
        {
            if (!view || !view.IsValid()) return false;
            string prefabName = "";
            var zdo = view.GetZDO();
            if (zdo != null && ZNetScene.instance != null)
            {
                var prefab = ZNetScene.instance.GetPrefab(zdo.GetPrefab());
                if (prefab) prefabName = prefab.name;
            }
            if (string.IsNullOrEmpty(prefabName))
                prefabName = view.gameObject.name.Replace("(Clone)", "");
            return prefabName == "Oak1";
        }

        private static float ComputeOakScale(Vector3 pos, int worldSeed)
        {
            if (worldSeed == 0) return 1f;
            int treeSeed = worldSeed + (int)(pos.x * 1000) + (int)(pos.z * 1000);
            var oldState = Random.state;
            Random.InitState(treeSeed);
            float computedScale = 1f;
            if (Random.Range(0f, 100f) <= ScalingChance.Value)
            {
                float randomT = Random.value;
                float biasedT = Mathf.Pow(randomT, ScaleExponent.Value);
                computedScale = Mathf.Lerp(MinScale.Value, MaxScale.Value, biasedT);
            }
            Random.state = oldState;
            return computedScale;
        }

        [HarmonyPatch(typeof(ZNetView), "Awake")]
        static class ZNetView_Awake_Patch
        {
            static void Postfix(ZNetView __instance)
            {
                if (!Enabled.Value || !__instance || !__instance.IsValid()) return;

                ZDO zdo = __instance.GetZDO();
                if (zdo == null) return;
                if (!IsOak1(__instance)) return;

                float currentScale = zdo.GetFloat(OakScaleFactorHash, 0f);
                if (currentScale > 0.1f)
                {
                    ApplyScale(__instance, currentScale);
                    return;
                }

                int seedNow = GetWorldSeed();
                float scale = ComputeOakScale(__instance.transform.position, seedNow);
                bool haveValidSeed = seedNow != 0;

                if (zdo.IsOwner())
                {
                    if (haveValidSeed)
                    {
                        zdo.Set(OakScaleFactorHash, scale);
                        ApplyScale(__instance, scale);
                        _Logger?.LogInfo($"[MightyOaks] Oak1 owner wrote pos={__instance.transform.position} scale={scale:F2} seed={seedNow}");
                    }
                    else
                    {
                        ApplyScale(__instance, scale);
                        _Logger?.LogInfo($"[MightyOaks] Oak1 owner no-write (seed not ready) pos={__instance.transform.position} scale={scale:F2} seed={seedNow} -> reapply next frame");
                        if (__instance != null && __instance.gameObject != null)
                            __instance.StartCoroutine(ReapplyScaleWhenSeedReady(__instance));
                    }
                }
                else
                {
                    ApplyScale(__instance, scale);
                    if (!haveValidSeed && __instance != null && __instance.gameObject != null)
                        __instance.StartCoroutine(ReapplyScaleWhenSeedReady(__instance));
                }
            }
        }

        private static IEnumerator ReapplyScaleWhenSeedReady(ZNetView view)
        {
            yield return null;
            if (view == null || !view.IsValid()) yield break;
            ZDO zdo = view.GetZDO();
            if (zdo == null) yield break;
            if (zdo.GetFloat(OakScaleFactorHash, 0f) > 0.1f) yield break;
            int seed = GetWorldSeed();
            if (seed == 0) yield break;
            float scale = ComputeOakScale(view.transform.position, seed);
            ApplyScale(view, scale);
            if (zdo.IsOwner())
            {
                zdo.Set(OakScaleFactorHash, scale);
                _Logger?.LogInfo($"[MightyOaks] Oak1 reapply owner wrote pos={view.transform.position} scale={scale:F2} seed={seed}");
            }
        }

        private static void ApplyScale(ZNetView view, float scale)
        {
            if (scale <= 1.01f) return;
            if (Mathf.Abs(view.transform.localScale.x - scale) < 0.01f) return;

            view.transform.localScale = Vector3.one * scale;

            var dest = view.GetComponent<Destructible>();
            var tree = view.GetComponent<TreeBase>();
            if (ScaleToughness.Value)
            {
                if (dest) dest.m_health *= (scale * scale);
                if (tree) tree.m_health *= (scale * scale);
            }
            if (MakeInvulnerable.Value && scale >= InvulnerabilityThreshold.Value)
            {
                if (dest)
                {
                    dest.m_minToolTier = 1000;
                    dest.m_damages.m_chop = HitData.DamageModifier.Immune;
                    dest.m_damages.m_pickaxe = HitData.DamageModifier.Immune;
                }
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
