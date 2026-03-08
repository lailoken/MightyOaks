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
        public const string PluginVersion = "1.2.0";

        private static ConfigEntry<float> ChanceScaleOak;
        private static ConfigEntry<float> ChanceScaleAshlandsOaks;
        private static ConfigEntry<float> ChanceScalePlainsStoneColumns;
        private static ConfigEntry<float> ChanceScaleSwampAncientTrees;
        private static ConfigEntry<float> ChanceScaleMistlandsTrees;
        private static ConfigEntry<float> MinScale;
        private static ConfigEntry<float> MaxScale;
        private static ConfigEntry<float> ScaleExponent;
        private static ConfigEntry<bool> ScaleToughness;
        private static ConfigEntry<float> InvulnerabilityThreshold;
        private static ConfigEntry<float> SpawnProtectionRadius;
        private static BepInEx.Logging.ManualLogSource _Logger;
        private static readonly int OakScaleFactorHash = "OakScaleFactor".GetStableHashCode();

        private static readonly System.Collections.Generic.HashSet<string> AshlandsOakPrefabs = new System.Collections.Generic.HashSet<string> { "AshlandsTree6", "AshlandsTree6_big" };
        private static readonly System.Collections.Generic.HashSet<string> PlainsStoneColumnPrefabs = new System.Collections.Generic.HashSet<string> { "HeathRockPillar","HeathRockPillar_frac" };
        private static readonly System.Collections.Generic.HashSet<string> SwampAncientTreePrefabs = new System.Collections.Generic.HashSet<string> { "SwampTree2", "SwampTree2_darkland" };
        private static readonly System.Collections.Generic.HashSet<string> MistlandsTreePrefabs = new System.Collections.Generic.HashSet<string> { "YggaShoot1", "YggaShoot2", "YggaShoot3" };

        // Server-side: kick clients that don't have MightyOaks (Jötunn only checks clients that have Jötunn)
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ZNetPeer, object> ValidatedPeers = new System.Runtime.CompilerServices.ConditionalWeakTable<ZNetPeer, object>();

        private void Awake()
        {
            _Logger = base.Logger;
            // Jötunn IsAdminOnly: server config is master. Jötunn syncs to clients on connect and when admins edit in-game (Configuration Manager etc.).
            var synced = new ConfigurationManagerAttributes { IsAdminOnly = true };
            var chanceRange = new AcceptableValueRange<float>(0f, 100f);
            const float scaleRangeMax = 20f;
            var scaleRange = new AcceptableValueRange<float>(0.1f, scaleRangeMax);
            var invulnRange = new AcceptableValueRange<float>(0f, scaleRangeMax + 1f);

            ChanceScaleOak = Config.Bind("Chances", "ChanceScaleOak", 15f, new ConfigDescription("Chance (0-100) to scale Oak trees. 0 = off.", chanceRange, synced));
            ChanceScaleAshlandsOaks = Config.Bind("Chances", "ChanceScaleAshlandsOaks", 5f, new ConfigDescription("Chance (0-100) to scale Ashlands burnt oaks. 0 = off.", chanceRange, synced));
            ChanceScalePlainsStoneColumns = Config.Bind("Chances", "ChanceScalePlainsStoneColumns", 0f, new ConfigDescription("Chance (0-100) to scale Plains stone columns. 0 = off.", chanceRange, synced));
            ChanceScaleSwampAncientTrees = Config.Bind("Chances", "ChanceScaleSwampAncientTrees", 0f, new ConfigDescription("Chance (0-100) to scale Swamp ancient trees. 0 = off.", chanceRange, synced));
            ChanceScaleMistlandsTrees = Config.Bind("Chances", "ChanceScaleMistlandsTrees", 0f, new ConfigDescription("Chance (0-100) to scale Mistlands trees (Yggdrasil shoots). 0 = off.", chanceRange, synced));
            MinScale = Config.Bind("General", "MinScale", 1f, new ConfigDescription("Minimum scale factor.", scaleRange, synced));
            MaxScale = Config.Bind("General", "MaxScale", 10f, new ConfigDescription("Maximum scale factor.", scaleRange, synced));
            ScaleExponent = Config.Bind("General", "ScaleExponent", 3.0f, new ConfigDescription("Exponent for scale distribution. 1.0 is linear (uniform). Higher values (e.g. 2.0, 3.0) make large trees rarer.", scaleRange, synced));
            ScaleToughness = Config.Bind("General", "ScaleToughness", true, new ConfigDescription("If true, scales the tree's health/toughness along with its size.", null, synced));
            InvulnerabilityThreshold = Config.Bind("General", "InvulnerabilityThreshold", 2.0f, new ConfigDescription("Scale threshold above which trees become invulnerable. Set to max to disable.", invulnRange, synced));
            SpawnProtectionRadius = Config.Bind("General", "SpawnProtectionRadius", 300f, new ConfigDescription("Radius from world center (spawn) where no giant oaks spawn. Default 300 keeps spawn area clear (beyond ~180m render). Set 0 to allow everywhere.", null, synced));

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
                    // Client: send only our version (for allow/kick). Config is never sent by client; server is master and pushes config to us.
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

        /// <summary>Description of who is writing the oak scale: "server" when server; when client is owner, the client's player name.</summary>
        private static string GetOakWriteOwnerDescription()
        {
            if (ZNet.instance == null) return "?";
            if (ZNet.instance.IsServer()) return "server";
            var name = TryGetLocalPlayerName();
            return string.IsNullOrEmpty(name) ? "client" : name;
        }

        private static string TryGetLocalPlayerName()
        {
            try
            {
                var playerType = typeof(Player);
                var t = Traverse.Create(playerType);
                var localPlayer = t.Field("m_localPlayer").GetValue() ?? t.Field("s_localPlayer").GetValue();
                if (localPlayer == null) return null;
                var name = Traverse.Create(localPlayer).Method("GetPlayerName").GetValue<string>();
                if (!string.IsNullOrEmpty(name)) return name;
                var game = Game.instance;
                if (game != null)
                {
                    var profile = Traverse.Create(game).Method("GetPlayerProfile").GetValue();
                    if (profile != null)
                    {
                        var profileName = Traverse.Create(profile).Method("GetName").GetValue<string>();
                        if (!string.IsNullOrEmpty(profileName)) return profileName;
                    }
                }
                return null;
            }
            catch { return null; }
        }

        private static string GetTreePrefabName(ZNetView view)
        {
            if (!view || !view.IsValid()) return "";
            var zdo = view.GetZDO();
            if (zdo != null && ZNetScene.instance != null)
            {
                var prefab = ZNetScene.instance.GetPrefab(zdo.GetPrefab());
                if (prefab) return prefab.name;
            }
            return view.gameObject.name.Replace("(Clone)", "");
        }

        private static bool IsScalableTree(ZNetView view, out string prefabName, out float chance)
        {
            prefabName = GetTreePrefabName(view);
            chance = 0f;
            if (string.IsNullOrEmpty(prefabName)) return false;
            if (prefabName == "Oak1" && ChanceScaleOak.Value > 0f) { chance = ChanceScaleOak.Value; return true; }
            if (AshlandsOakPrefabs.Contains(prefabName) && ChanceScaleAshlandsOaks.Value > 0f) { chance = ChanceScaleAshlandsOaks.Value; return true; }
            if (PlainsStoneColumnPrefabs.Contains(prefabName) && ChanceScalePlainsStoneColumns.Value > 0f) { chance = ChanceScalePlainsStoneColumns.Value; return true; }
            if (SwampAncientTreePrefabs.Contains(prefabName) && ChanceScaleSwampAncientTrees.Value > 0f) { chance = ChanceScaleSwampAncientTrees.Value; return true; }
            if (MistlandsTreePrefabs.Contains(prefabName) && ChanceScaleMistlandsTrees.Value > 0f) { chance = ChanceScaleMistlandsTrees.Value; return true; }
            return false;
        }

        private static float ComputeScale(Vector3 pos, int worldSeed, float scalingChance)
        {
            if (worldSeed == 0 || scalingChance <= 0f) return 1f;
            float radius = SpawnProtectionRadius.Value;
            if (radius > 0f)
            {
                float distSq = pos.x * pos.x + pos.z * pos.z;
                if (distSq < radius * radius) return 1f;
            }
            int treeSeed = worldSeed + (int)(pos.x * 1000) + (int)(pos.z * 1000);
            var oldState = Random.state;
            Random.InitState(treeSeed);
            float computedScale = 1f;
            if (Random.Range(0f, 100f) <= scalingChance)
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
                if (!__instance || !__instance.IsValid()) return;

                ZDO zdo = __instance.GetZDO();
                if (zdo == null) return;
                if (!IsScalableTree(__instance, out string prefabName, out float scalingChance)) return;

                float currentScale = zdo.GetFloat(OakScaleFactorHash, 0f);
                if (currentScale > 0.1f)
                {
                    ApplyScale(__instance, currentScale);
                    return;
                }

                int seedNow = GetWorldSeed();
                float scale = ComputeScale(__instance.transform.position, seedNow, scalingChance);
                bool haveValidSeed = seedNow != 0;

                if (zdo.IsOwner())
                {
                    if (haveValidSeed)
                    {
                        zdo.Set(OakScaleFactorHash, scale);
                        ApplyScale(__instance, scale);
                        _Logger?.LogInfo($"[MightyOaks] {prefabName} scale written pos={__instance.transform.position} scale={scale:F2} seed={seedNow} owner={GetOakWriteOwnerDescription()}");
                    }
                    else
                    {
                        ApplyScale(__instance, scale);
                        _Logger?.LogInfo($"[MightyOaks] {prefabName} no-write (seed not ready) pos={__instance.transform.position} scale={scale:F2} seed={seedNow} owner={GetOakWriteOwnerDescription()} -> reapply next frame");
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
            if (!IsScalableTree(view, out string prefabName, out float scalingChance)) yield break;
            if (zdo.GetFloat(OakScaleFactorHash, 0f) > 0.1f) yield break;
            int seed = GetWorldSeed();
            if (seed == 0) yield break;
            float scale = ComputeScale(view.transform.position, seed, scalingChance);
            ApplyScale(view, scale);
            if (zdo.IsOwner())
            {
                zdo.Set(OakScaleFactorHash, scale);
                _Logger?.LogInfo($"[MightyOaks] {prefabName} reapply scale written pos={view.transform.position} scale={scale:F2} seed={seed} owner={GetOakWriteOwnerDescription()}");
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
            if (InvulnerabilityThreshold.Value > 0f && scale >= InvulnerabilityThreshold.Value)
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
