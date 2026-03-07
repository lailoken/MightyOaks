using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using System.Collections;

namespace MightyOaks
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
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

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ZNetPeer, object> ValidatedPeers = new System.Runtime.CompilerServices.ConditionalWeakTable<ZNetPeer, object>();
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ZNetPeer, object> VerificationStarted = new System.Runtime.CompilerServices.ConditionalWeakTable<ZNetPeer, object>();

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

        [HarmonyPatch(typeof(ZNet), "Awake")]
        static class ZNet_Awake_Patch
        {
            static void Postfix(ZNet __instance)
            {
                ZRoutedRpc.instance.Register<ZPackage>("MightyOaks_ConfigSync", RPC_MightyOaks_ConfigSync);
            }
        }

        [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
        static class ZNet_OnNewConnection_Patch
        {
            static void Postfix(ZNet __instance, ZNetPeer peer)
            {
                peer.m_rpc.Register<string>("MightyOaks_VersionCheck", RPC_MightyOaks_VersionCheck);

                if (!__instance.IsServer())
                    peer.m_rpc.Invoke("MightyOaks_VersionCheck", PluginVersion);
                else
                {
                    if (VerificationStarted.TryGetValue(peer, out _)) return;
                    VerificationStarted.Add(peer, null);
                    if (peer.m_uid != 0) SendConfigToPeer(peer);
                    __instance.StartCoroutine(VerifyPeer(peer));
                }
            }
        }

        private static IEnumerator VerifyPeer(ZNetPeer peer)
        {
            yield return new WaitForSeconds(10f);
            if (peer.m_socket.IsConnected() && !ValidatedPeers.TryGetValue(peer, out _))
            {
                _Logger.LogWarning($"Peer {peer.m_uid} failed to validate MightyOaks version (timeout). Kicking.");
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

            _Logger.LogInfo($"Peer {peer.m_uid} has MightyOaks version: {clientVersion}");

            if (!IsVersionCompatible(clientVersion, PluginVersion))
            {
                _Logger.LogWarning($"Peer {peer.m_uid} incompatible version {clientVersion} (server: {PluginVersion}). Kicking.");
                ZNet.instance.Disconnect(peer);
                return;
            }

            if (!ValidatedPeers.TryGetValue(peer, out _))
            {
                ValidatedPeers.Add(peer, null);
                if (peer.m_uid != 0) SendConfigToPeer(peer);
            }
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

        private static void SendConfigToPeer(ZNetPeer peer)
        {
            _Logger.LogInfo($"Sending config to peer {peer.m_uid}");
            ZPackage pkg = new ZPackage();
            pkg.Write(ScalingChance.Value);
            pkg.Write(MinScale.Value);
            pkg.Write(MaxScale.Value);
            pkg.Write(ScaleExponent.Value);
            pkg.Write(ScaleToughness.Value);
            pkg.Write(MakeInvulnerable.Value);
            pkg.Write(InvulnerabilityThreshold.Value);
            
            ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, "MightyOaks_ConfigSync", pkg);
        }

        private static void RPC_MightyOaks_ConfigSync(long sender, ZPackage pkg)
        {
            if (ZNet.instance.IsServer()) return;

            _Logger.LogInfo("Received config from server.");
            ScalingChance.Value = pkg.ReadSingle();
            MinScale.Value = pkg.ReadSingle();
            MaxScale.Value = pkg.ReadSingle();
            ScaleExponent.Value = pkg.ReadSingle();
            ScaleToughness.Value = pkg.ReadBool();
            MakeInvulnerable.Value = pkg.ReadBool();
            InvulnerabilityThreshold.Value = pkg.ReadSingle();
            
            _Logger.LogInfo("Config synced with server.");
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
