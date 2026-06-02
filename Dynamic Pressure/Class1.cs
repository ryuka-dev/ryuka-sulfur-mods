using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using PerfectRandom.Sulfur.Core;
using PerfectRandom.Sulfur.Core.LevelGeneration;
using PerfectRandom.Sulfur.Core.Units;
using PerfectRandom.Sulfur.Core.Units.AI;
using PerfectRandom.Sulfur.Core.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ryuka.Sulfur.DynamicPressure
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class DynamicPressurePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ryuka.sulfur.dynamicpressure";
        public const string PluginName = "Dynamic Pressure";
        public const string PluginVersion = "0.1.0";

        private static DynamicPressurePlugin _instance;
        private Harmony _harmony;

        private ConfigEntry<bool> _enableMod;
        private ConfigEntry<bool> _enableAutoSpawn;
        private ConfigEntry<int> _pressureStyle;

        private ConfigEntry<bool> _enableOverlay;
        private ConfigEntry<Key> _overlayToggleKey;
        private ConfigEntry<Key> _manualSpawnKey;

        private ConfigEntry<float> _scanInterval;
        private ConfigEntry<float> _enemyScanRadius;
        private ConfigEntry<float> _closeEnemyDistance;
        private ConfigEntry<float> _lowHealthStopThreshold;
        private ConfigEntry<float> _recentDamageStopWindow;

        private ConfigEntry<int> _minOriginalEngagedHostilesForSpawn;
        private ConfigEntry<float> _cooldownAfterModKillOnly;
        private ConfigEntry<float> _maxSecondsWithoutOriginalKill;
        private ConfigEntry<int> _maxModSpawnsPerOriginalEngaged;
        private ConfigEntry<bool> _allowEnvironmentFallbackCandidates;

        private ConfigEntry<float> _spawnDistanceMin;
        private ConfigEntry<float> _spawnDistanceMax;
        private ConfigEntry<float> _style1Cooldown;
        private ConfigEntry<float> _style2Cooldown;
        private ConfigEntry<float> _style3Cooldown;

        private ConfigEntry<int> _style1TargetPressure;
        private ConfigEntry<int> _style2TargetPressure;
        private ConfigEntry<int> _style3TargetPressure;

        private ConfigEntry<int> _style1MaxSpawnPerWave;
        private ConfigEntry<int> _style2MaxSpawnPerWave;
        private ConfigEntry<int> _style3MaxSpawnPerWave;

        private ConfigEntry<int> _style1MaxModAlive;
        private ConfigEntry<int> _style2MaxModAlive;
        private ConfigEntry<int> _style3MaxModAlive;

        private ConfigEntry<int> _style1MaxModPerRoom;
        private ConfigEntry<int> _style2MaxModPerRoom;
        private ConfigEntry<int> _style3MaxModPerRoom;

        private ConfigEntry<int> _style1MaxModPerLevel;
        private ConfigEntry<int> _style2MaxModPerLevel;
        private ConfigEntry<int> _style3MaxModPerLevel;

        private readonly List<UnitSO> _candidatePool = new List<UnitSO>();
        private readonly List<string> _recentEvents = new List<string>();
        private readonly Dictionary<int, int> _modSpawnedPerRoom = new Dictionary<int, int>();
        private readonly HashSet<int> _deadNpcIds = new HashSet<int>();

        private object _lastGraphContext;
        private float _nextScanTime;
        private float _nextSpawnTime;
        private bool _spawnInProgress;
        private bool _inLevelTransition;
        private int _waveId;
        private int _spawnedThisLevel;
        private int _spawnedSinceLastOriginalKill;
        private float _lastOriginalKillTime = -9999f;
        private float _lastModKillTime = -9999f;

        private Snapshot _snapshot = new Snapshot();
        private SpawnBlockReason _lastBlockReason = SpawnBlockReason.None;
        private string _lastDecision = "None";
        private string _lastSpawnSummary = "None";
        private string _lastAggroReport = "None";

        private void Awake()
        {
            _instance = this;
            BindConfig();

            _harmony = new Harmony(PluginGuid);
            TryPatchTransitions();
            TryPatchNpcDie();

            Log("Loaded.");
        }

        private void OnDestroy()
        {
            try
            {
                _harmony?.UnpatchSelf();
            }
            catch
            {
                // ignored
            }

            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void BindConfig()
        {
            _enableMod = Config.Bind("General", "EnableMod", true, "Enable Dynamic Pressure.");
            _enableAutoSpawn = Config.Bind("General", "EnableAutoSpawn", true, "Enable automatic dynamic pressure spawning. Keep false for first debug testing.");
            _pressureStyle = Config.Bind("General", "PressureStyle", 1, "1 = Light, 2 = Heavy, 3 = Nightmare.");

            _enableOverlay = Config.Bind("Debug Overlay", "EnableOverlay", false, "Show debug overlay.");
            _overlayToggleKey = Config.Bind("Debug Overlay", "OverlayToggleKey", Key.F9, "Toggle overlay key.");
            _manualSpawnKey = Config.Bind("Debug Overlay", "ManualSpawnKey", Key.F8, "Manual debug spawn key.");

            _scanInterval = Config.Bind("Pressure", "ScanInterval", 1.0f, "Seconds between pressure scans.");
            _enemyScanRadius = Config.Bind("Pressure", "EnemyScanRadius", 25.0f, "Nearby enemy scan radius.");
            _closeEnemyDistance = Config.Bind("Pressure", "CloseEnemyDistance", 8.0f, "Enemies closer than this add extra pressure.");
            _lowHealthStopThreshold = Config.Bind("Safety", "LowHealthStopThreshold", 0.35f, "Do not spawn if player health is below this normalized value.");
            _recentDamageStopWindow = Config.Bind("Safety", "RecentDamageStopWindow", 4.0f, "Do not spawn if player was damaged within this many seconds.");

            _minOriginalEngagedHostilesForSpawn = Config.Bind("Anti Loop", "MinOriginalEngagedHostilesForSpawn", 1, "Require this many engaged original hostiles before spawning mod enemies.");
            _cooldownAfterModKillOnly = Config.Bind("Anti Loop", "CooldownAfterModKillOnly", 8.0f, "If only mod enemies are dying, pause spawning for this many seconds.");
            _maxSecondsWithoutOriginalKill = Config.Bind("Anti Loop", "MaxSecondsWithoutOriginalKill", 30.0f, "Stop spawning if no original enemy has died for this long after mod spawns.");
            _maxModSpawnsPerOriginalEngaged = Config.Bind("Anti Loop", "MaxModSpawnsPerOriginalEngaged", 2, "Soft budget: each engaged original enemy can support this many mod spawns.");
            _allowEnvironmentFallbackCandidates = Config.Bind("Candidates", "AllowEnvironmentFallbackCandidates", false, "Use current environment enemy metadata if current level alive NPC candidate pool is empty.");

            _spawnDistanceMin = Config.Bind("Spawn", "SpawnDistanceMin", 8.0f, "Minimum spawn distance from player.");
            _spawnDistanceMax = Config.Bind("Spawn", "SpawnDistanceMax", 60.0f, "Maximum spawn distance from player.");

            _style1TargetPressure = Config.Bind("Style 1 - Light", "TargetPressure", 6, "Target pressure.");
            _style1Cooldown = Config.Bind("Style 1 - Light", "SpawnCooldown", 16.0f, "Spawn cooldown.");
            _style1MaxSpawnPerWave = Config.Bind("Style 1 - Light", "MaxSpawnPerWave", 1, "Max spawns per wave.");
            _style1MaxModAlive = Config.Bind("Style 1 - Light", "MaxModSpawnedAlive", 2, "Max alive mod-spawned NPCs.");
            _style1MaxModPerRoom = Config.Bind("Style 1 - Light", "MaxModSpawnedPerRoom", 3, "Max mod spawns per room.");
            _style1MaxModPerLevel = Config.Bind("Style 1 - Light", "MaxModSpawnedPerLevel", 8, "Max mod spawns per level.");

            _style2TargetPressure = Config.Bind("Style 2 - Heavy", "TargetPressure", 9, "Target pressure.");
            _style2Cooldown = Config.Bind("Style 2 - Heavy", "SpawnCooldown", 10.0f, "Spawn cooldown.");
            _style2MaxSpawnPerWave = Config.Bind("Style 2 - Heavy", "MaxSpawnPerWave", 1, "Max spawns per wave.");
            _style2MaxModAlive = Config.Bind("Style 2 - Heavy", "MaxModSpawnedAlive", 4, "Max alive mod-spawned NPCs.");
            _style2MaxModPerRoom = Config.Bind("Style 2 - Heavy", "MaxModSpawnedPerRoom", 5, "Max mod spawns per room.");
            _style2MaxModPerLevel = Config.Bind("Style 2 - Heavy", "MaxModSpawnedPerLevel", 16, "Max mod spawns per level.");

            _style3TargetPressure = Config.Bind("Style 3 - Nightmare", "TargetPressure", 13, "Target pressure.");
            _style3Cooldown = Config.Bind("Style 3 - Nightmare", "SpawnCooldown", 7.0f, "Spawn cooldown.");
            _style3MaxSpawnPerWave = Config.Bind("Style 3 - Nightmare", "MaxSpawnPerWave", 2, "Max spawns per wave.");
            _style3MaxModAlive = Config.Bind("Style 3 - Nightmare", "MaxModSpawnedAlive", 6, "Max alive mod-spawned NPCs.");
            _style3MaxModPerRoom = Config.Bind("Style 3 - Nightmare", "MaxModSpawnedPerRoom", 8, "Max mod spawns per room.");
            _style3MaxModPerLevel = Config.Bind("Style 3 - Nightmare", "MaxModSpawnedPerLevel", 28, "Max mod spawns per level.");
        }

        private void Update()
        {
            HandleKeys();

            if (!_enableMod.Value)
            {
                _lastDecision = "BLOCKED";
                _lastBlockReason = SpawnBlockReason.Disabled;
                return;
            }

            GameManager gm = StaticInstance<GameManager>.Instance;
            if (gm != null && gm.graphContext != null && !ReferenceEquals(_lastGraphContext, gm.graphContext))
            {
                _lastGraphContext = gm.graphContext;
                ResetRuntimeState("New graphContext");
            }

            if (Time.time >= _nextScanTime)
            {
                _nextScanTime = Time.time + Mathf.Max(0.2f, _scanInterval.Value);
                ScanPressure();
            }

            if (_enableAutoSpawn.Value && !_spawnInProgress && Time.time >= _nextSpawnTime)
            {
                TryAutoSpawn();
            }
        }

        private void HandleKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard[_overlayToggleKey.Value].wasPressedThisFrame)
            {
                _enableOverlay.Value = !_enableOverlay.Value;
            }

            if (keyboard[_manualSpawnKey.Value].wasPressedThisFrame)
            {
                if (!_spawnInProgress)
                {
                    _ = SpawnWaveAsync(1, "Manual debug spawn");
                }
            }
        }

        private void ScanPressure()
        {
            Snapshot s = new Snapshot();
            _candidatePool.Clear();

            GameManager gm = StaticInstance<GameManager>.Instance;
            if (gm == null)
            {
                _snapshot = s;
                _lastBlockReason = SpawnBlockReason.NoGameManager;
                return;
            }

            s.hasGameManager = true;
            s.gameState = gm.gameState.ToString();
            s.inSafeZone = gm.InSafeZone;
            s.currentEnvironment = gm.currentEnvironment != null ? gm.currentEnvironment.id.ToString() : "null";

            if (gm.PlayerUnit == null || gm.PlayerObject == null)
            {
                _snapshot = s;
                _lastBlockReason = SpawnBlockReason.NoPlayer;
                return;
            }

            Unit playerUnit = gm.PlayerUnit;
            Room playerRoom = GetUnitRoom(playerUnit);

            s.hasPlayer = true;
            s.playerAlive = playerUnit.IsAlive;
            s.playerHp = SafeGetPlayerHp(playerUnit);
            s.playerTimeSinceDamage = playerUnit.TimeSinceLastDamageTaken;
            s.playerRoomName = playerRoom != null ? playerRoom.name : "null";
            s.playerRoomIsEndRoom = playerRoom != null && playerRoom.IsEndRoom;

            List<Npc> alive = gm.aliveNpcs;
            s.aliveNpcs = alive != null ? alive.Count : 0;

            if (alive != null)
            {
                for (int i = 0; i < alive.Count; i++)
                {
                    Npc npc = alive[i];
                    if (!IsValidAliveNpc(npc))
                    {
                        continue;
                    }

                    DynamicPressureSpawnMarker marker = npc.GetComponent<DynamicPressureSpawnMarker>();
                    bool isMod = marker != null;
                    bool hostile = IsHostileToPlayer(npc);
                    if (!hostile)
                    {
                        continue;
                    }

                    float distance = Vector3.Distance(npc.transform.position, gm.PlayerPosition);
                    bool nearby = distance <= _enemyScanRadius.Value;
                    bool targeting = SafeTargetIsPlayer(npc);
                    bool knowsPlayer = SafeHasKnownPlayerPosition(npc, gm.PlayerUnit);
                    bool roomRelevant = IsSameOrConnectedRoom(GetUnitRoom(npc), playerRoom);
                    bool engaged = nearby || targeting || knowsPlayer || roomRelevant;

                    int pressure = CalculateNpcPressure(npc, distance, targeting, knowsPlayer);

                    if (isMod)
                    {
                        s.modHostileAlive++;
                        if (nearby)
                        {
                            s.nearbyModHostiles++;
                            s.modPressure += pressure;
                        }

                        if (targeting)
                        {
                            s.modTargetingPlayer++;
                        }
                    }
                    else
                    {
                        s.originalHostileAlive++;

                        if (engaged)
                        {
                            s.originalEngagedHostiles++;
                        }

                        if (nearby)
                        {
                            s.nearbyOriginalHostiles++;
                            s.originalPressure += pressure;
                        }

                        if (targeting)
                        {
                            s.originalTargetingPlayer++;
                        }

                        TryAddCandidateFromNpc(npc);
                    }
                }
            }

            if (_candidatePool.Count == 0 && _allowEnvironmentFallbackCandidates.Value)
            {
                AddEnvironmentFallbackCandidates(gm);
                s.candidateSource = _candidatePool.Count > 0 ? "EnvironmentMetadata" : "None";
            }
            else
            {
                s.candidateSource = _candidatePool.Count > 0 ? "CurrentLevelAliveNpcs" : "None";
            }

            s.candidateCount = _candidatePool.Count;
            s.targetPressure = GetTargetPressure();
            s.currentPressure = s.originalPressure + s.modPressure;
            s.deficit = s.targetPressure - s.currentPressure;
            s.spawnedThisLevel = _spawnedThisLevel;
            s.spawnedSinceLastOriginalKill = _spawnedSinceLastOriginalKill;
            s.timeSinceLastOriginalKill = Time.time - _lastOriginalKillTime;
            s.timeSinceLastModKill = Time.time - _lastModKillTime;
            s.nextSpawnIn = Mathf.Max(0f, _nextSpawnTime - Time.time);

            s.wouldDelayOnAllEnemiesDead = s.originalHostileAlive == 0 && s.modHostileAlive > 0;

            _snapshot = s;
        }

        private void TryAutoSpawn()
        {
            SpawnBlockReason blockReason;
            int spawnCount;

            if (!CanSpawn(out blockReason, out spawnCount))
            {
                _lastDecision = "BLOCKED";
                _lastBlockReason = blockReason;
                return;
            }

            _ = SpawnWaveAsync(spawnCount, "Auto pressure deficit");
        }

        private bool CanSpawn(out SpawnBlockReason blockReason, out int spawnCount)
        {
            spawnCount = 0;

            GameManager gm = StaticInstance<GameManager>.Instance;
            if (gm == null)
            {
                blockReason = SpawnBlockReason.NoGameManager;
                return false;
            }

            if (_inLevelTransition)
            {
                blockReason = SpawnBlockReason.LevelTransition;
                return false;
            }

            if (gm.PlayerUnit == null || gm.PlayerObject == null)
            {
                blockReason = SpawnBlockReason.NoPlayer;
                return false;
            }

            if (gm.gameState.ToString() != "Running")
            {
                blockReason = SpawnBlockReason.GameStateNotRunning;
                return false;
            }

            if (gm.InSafeZone)
            {
                blockReason = SpawnBlockReason.SafeZone;
                return false;
            }

            Unit playerUnit = gm.PlayerUnit;
            if (!playerUnit.IsAlive)
            {
                blockReason = SpawnBlockReason.PlayerDead;
                return false;
            }

            if (SafeGetPlayerHp(playerUnit) <= _lowHealthStopThreshold.Value)
            {
                blockReason = SpawnBlockReason.LowHealth;
                return false;
            }

            if (playerUnit.TimeSinceLastDamageTaken <= _recentDamageStopWindow.Value)
            {
                blockReason = SpawnBlockReason.RecentDamage;
                return false;
            }

            Room playerRoom = GetUnitRoom(playerUnit);
            if (playerRoom != null && playerRoom.IsEndRoom)
            {
                blockReason = SpawnBlockReason.EndRoomBlocked;
                return false;
            }

            if (_snapshot.originalEngagedHostiles < _minOriginalEngagedHostilesForSpawn.Value)
            {
                blockReason = SpawnBlockReason.NoEngagedOriginalHostiles;
                return false;
            }

            if (_snapshot.currentPressure >= _snapshot.targetPressure)
            {
                blockReason = SpawnBlockReason.PressureAlreadyHigh;
                return false;
            }

            if (_snapshot.modHostileAlive >= GetMaxModAlive())
            {
                blockReason = SpawnBlockReason.MaxModSpawnedAlive;
                return false;
            }

            if (_spawnedThisLevel >= GetMaxModPerLevel())
            {
                blockReason = SpawnBlockReason.LevelSpawnBudgetExceeded;
                return false;
            }

            int supportedSpawnBudget = Mathf.Max(1, _snapshot.originalEngagedHostiles) * Mathf.Max(1, _maxModSpawnsPerOriginalEngaged.Value);
            if (_spawnedSinceLastOriginalKill >= supportedSpawnBudget)
            {
                blockReason = SpawnBlockReason.SpawnBudgetPerOriginalExceeded;
                return false;
            }

            if (_spawnedSinceLastOriginalKill > 0 && Time.time - _lastOriginalKillTime > _maxSecondsWithoutOriginalKill.Value)
            {
                blockReason = SpawnBlockReason.NoOriginalKillProgress;
                return false;
            }

            if (_lastModKillTime > _lastOriginalKillTime && Time.time - _lastModKillTime < _cooldownAfterModKillOnly.Value)
            {
                blockReason = SpawnBlockReason.RecentModKillOnly;
                return false;
            }

            if (_candidatePool.Count == 0)
            {
                blockReason = SpawnBlockReason.NoCandidateUnits;
                return false;
            }

            spawnCount = Mathf.Min(GetMaxSpawnPerWave(), Mathf.Max(1, _snapshot.deficit));
            spawnCount = Mathf.Min(spawnCount, GetMaxModAlive() - _snapshot.modHostileAlive);
            spawnCount = Mathf.Min(spawnCount, GetMaxModPerLevel() - _spawnedThisLevel);

            if (spawnCount <= 0)
            {
                blockReason = SpawnBlockReason.MaxModSpawnedAlive;
                return false;
            }

            blockReason = SpawnBlockReason.None;
            return true;
        }

        private async Task SpawnWaveAsync(int count, string reason)
        {
            if (_spawnInProgress)
            {
                return;
            }

            _spawnInProgress = true;
            _waveId++;

            int spawned = 0;

            try
            {
                ScanPressure();

                for (int i = 0; i < count; i++)
                {
                    UnitSO unitSo = PickCandidate();
                    if (unitSo == null)
                    {
                        _lastDecision = "BLOCKED";
                        _lastBlockReason = SpawnBlockReason.NoCandidateUnits;
                        AddEvent("Spawn blocked: no candidate.");
                        break;
                    }

                    SpawnPointChoice spawnPoint;
                    if (!TryFindSpawnPoint(unitSo, out spawnPoint))
                    {
                        _lastDecision = "BLOCKED";
                        _lastBlockReason = SpawnBlockReason.NoValidNpcSpawnPoint;
                        AddEvent("Spawn blocked: no valid NPCSpawn point.");
                        break;
                    }

                    int roomId = GetRoomId(spawnPoint.room);
                    int roomCount;
                    _modSpawnedPerRoom.TryGetValue(roomId, out roomCount);
                    if (roomCount >= GetMaxModPerRoom())
                    {
                        _lastDecision = "BLOCKED";
                        _lastBlockReason = SpawnBlockReason.RoomSpawnBudgetExceeded;
                        AddEvent("Spawn blocked: room budget exceeded.");
                        break;
                    }

                    bool ok = await SpawnOneAsync(unitSo, spawnPoint, reason);
                    if (!ok)
                    {
                        _lastDecision = "FAILED";
                        _lastBlockReason = SpawnBlockReason.SpawnAsyncFailed;
                        break;
                    }

                    spawned++;
                }

                if (spawned > 0)
                {
                    _lastDecision = "SPAWNED";
                    _lastBlockReason = SpawnBlockReason.None;
                    _nextSpawnTime = Time.time + GetSpawnCooldown();
                }
            }
            catch (Exception e)
            {
                _lastDecision = "FAILED";
                _lastBlockReason = SpawnBlockReason.SpawnAsyncFailed;
                AddEvent("Spawn exception: " + e.GetType().Name);
                Logger.LogError(e);
            }
            finally
            {
                _spawnInProgress = false;
                ScanPressure();
            }
        }

        private async Task<bool> SpawnOneAsync(UnitSO unitSo, SpawnPointChoice spawnPoint, string reason)
        {
            if (unitSo == null)
            {
                return false;
            }

            Unit spawnedUnit = await unitSo.SpawnUnitAsync(this, spawnPoint.position, Quaternion.identity);
            if (spawnedUnit == null)
            {
                return false;
            }

            Npc npc = spawnedUnit as Npc;
            if (npc == null)
            {
                npc = spawnedUnit.GetComponent<Npc>();
            }

            if (npc == null)
            {
                return false;
            }

            if (spawnPoint.room != null)
            {
                npc.currentRoom = spawnPoint.room;
                npc.lastValidCurrentRoom = spawnPoint.room;
                npc.lastRoomCalcPosition = npc.transform.position;
            }

            DynamicPressureSpawnMarker marker = npc.gameObject.AddComponent<DynamicPressureSpawnMarker>();
            marker.sourceUnitSo = unitSo;
            marker.spawnTime = Time.time;
            marker.spawnPosition = spawnPoint.position;
            marker.spawnRoom = spawnPoint.room;
            marker.spawnReason = reason;
            marker.waveId = _waveId;

            int roomId = GetRoomId(spawnPoint.room);
            int roomCount;
            _modSpawnedPerRoom.TryGetValue(roomId, out roomCount);
            _modSpawnedPerRoom[roomId] = roomCount + 1;

            _spawnedThisLevel++;
            _spawnedSinceLastOriginalKill++;

            _lastSpawnSummary = string.Format(
                "{0} at {1}, room={2}, source={3}",
                unitSo.name,
                spawnPoint.position,
                spawnPoint.room != null ? spawnPoint.room.name : "null",
                spawnPoint.source
            );

            AddEvent("Spawned: " + unitSo.name);
            StartCoroutine(ReportPlayerPositionLater(npc, marker));

            return true;
        }

        private IEnumerator ReportPlayerPositionLater(Npc npc, DynamicPressureSpawnMarker marker)
        {
            yield return new WaitForSeconds(0.5f);

            GameManager gm = StaticInstance<GameManager>.Instance;
            if (gm == null || gm.PlayerObject == null || gm.PlayerUnit == null)
            {
                _lastAggroReport = "Failed: no player";
                yield break;
            }

            if (npc == null || !npc.IsAlive || npc.AiAgent == null)
            {
                _lastAggroReport = "Failed: invalid npc";
                yield break;
            }

            try
            {
                npc.AiAgent.ReportLastSeen(gm.PlayerObject);

                marker.aggroReported = true;
                marker.lastAggroReportTime = Time.time;
                marker.targetingPlayerAfterReport = SafeTargetIsPlayer(npc);
                marker.hasKnownPlayerPositionAfterReport = SafeHasKnownPlayerPosition(npc, gm.PlayerUnit);

                _lastAggroReport = marker.targetingPlayerAfterReport || marker.hasKnownPlayerPositionAfterReport
                    ? "Success"
                    : "Reported but not targeting yet";

                AddEvent("ReportLastSeen: " + _lastAggroReport);
            }
            catch (Exception e)
            {
                _lastAggroReport = "Exception: " + e.GetType().Name;
                Logger.LogWarning(e);
            }
        }

        private UnitSO PickCandidate()
        {
            if (_candidatePool.Count == 0)
            {
                return null;
            }

            return _candidatePool[UnityEngine.Random.Range(0, _candidatePool.Count)];
        }

        private void TryAddCandidateFromNpc(Npc npc)
        {
            if (npc == null || npc.unitSO == null)
            {
                return;
            }

            UnitSO so = npc.unitSO;
            if (!IsValidCandidateUnitSo(so))
            {
                return;
            }

            if (!_candidatePool.Contains(so))
            {
                _candidatePool.Add(so);
            }
        }

        private void AddEnvironmentFallbackCandidates(GameManager gm)
        {
            try
            {
                if (gm == null || gm.environmentsInOrder == null || gm.currentEnvironment == null)
                {
                    return;
                }

                WorldEnvironmentList.Metadata metadata = gm.environmentsInOrder.GetMetadata(gm.currentEnvironment.id);
                if (metadata.availableEnemiesReadOnly == null)
                {
                    return;
                }

                for (int i = 0; i < metadata.availableEnemiesReadOnly.Length; i++)
                {
                    UnitSO so = metadata.availableEnemiesReadOnly[i].GetAsset();
                    if (so != null && IsValidCandidateUnitSo(so) && !_candidatePool.Contains(so))
                    {
                        _candidatePool.Add(so);
                    }
                }
            }
            catch (Exception e)
            {
                AddEvent("Candidate fallback failed: " + e.GetType().Name);
            }
        }

        private bool IsValidCandidateUnitSo(UnitSO so)
        {
            if (so == null)
            {
                return false;
            }

            if (so.isCivilian)
            {
                return false;
            }

            if (so.isProtectedNpc)
            {
                return false;
            }

            if (so.ExperienceOnKill <= 0)
            {
                return false;
            }

            if ((so.unitType & UnitType.Boss) != 0)
            {
                return false;
            }

            return true;
        }

        private bool TryFindSpawnPoint(UnitSO unitSo, out SpawnPointChoice result)
        {
            result = default;

            GameManager gm = StaticInstance<GameManager>.Instance;
            if (gm == null || gm.PlayerUnit == null || gm.PlayerObject == null || unitSo == null)
                return false;

            Vector3 playerPos = gm.PlayerPosition;

            List<SpawnPointChoice> strictChoices = new List<SpawnPointChoice>();
            List<SpawnPointChoice> looseChoices = new List<SpawnPointChoice>();

            List<Room> rooms = gm.orderedRooms;
            if (rooms == null || rooms.Count == 0)
                return false;

            for (int r = 0; r < rooms.Count; r++)
            {
                Room room = rooms[r];
                if (!IsValidSpawnRoom(room))
                    continue;

                NPCSpawn.Data[] spawns = room.GetNPCSpawns();
                if (spawns == null || spawns.Length == 0)
                    continue;

                for (int i = 0; i < spawns.Length; i++)
                {
                    NPCSpawn.Data data = spawns[i];
                    Room spawnRoom = data.room != null ? data.room : room;

                    if (!IsValidSpawnRoom(spawnRoom))
                        continue;

                    if ((data.usableByTypes & unitSo.unitType) == 0)
                        continue;

                    float distance = Vector3.Distance(data.position, playerPos);

                    SpawnPointChoice choice = new SpawnPointChoice
                    {
                        position = data.position,
                        room = spawnRoom,
                        distanceToPlayer = distance,
                        source = "GameManager.orderedRooms NPCSpawn"
                    };

                    if (distance >= _spawnDistanceMin.Value && distance <= _spawnDistanceMax.Value)
                    {
                        strictChoices.Add(choice);
                    }
                    else if (distance > _spawnDistanceMax.Value && distance <= _spawnDistanceMax.Value * 2f)
                    {
                        looseChoices.Add(choice);
                    }
                }
            }

            if (strictChoices.Count > 0)
            {
                result = PickNearestRandomized(strictChoices, playerPos);
                return true;
            }

            if (looseChoices.Count > 0)
            {
                result = PickNearestRandomized(looseChoices, playerPos);
                result.source += " / loose distance";
                return true;
            }

            return false;
        }

        private bool IsValidSpawnRoom(Room room)
        {
            if (room == null)
                return false;

            if (room.IsStartRoom)
                return false;

            if (room.IsEndRoom)
                return false;

            if (room.disallowEnemySpawn)
                return false;

            return true;
        }

        private SpawnPointChoice PickNearestRandomized(List<SpawnPointChoice> choices, Vector3 playerPos)
        {
            choices.Sort((a, b) => a.distanceToPlayer.CompareTo(b.distanceToPlayer));

            int maxPick = Mathf.Min(choices.Count, 5);
            return choices[UnityEngine.Random.Range(0, maxPick)];
        }

        private int CalculateNpcPressure(Npc npc, float distance, bool targeting, bool knowsPlayer)
        {
            int pressure = 1;

            if (npc != null && npc.unitSO != null)
            {
                pressure = Mathf.Clamp(npc.unitSO.SpawnCost, 1, 4);
            }

            if (distance <= _closeEnemyDistance.Value)
            {
                pressure += 1;
            }

            if (targeting)
            {
                pressure += 2;
            }
            else if (knowsPlayer)
            {
                pressure += 1;
            }

            return pressure;
        }

        private bool IsValidAliveNpc(Npc npc)
        {
            return npc != null && npc.gameObject != null && npc.IsAlive;
        }

        private bool IsHostileToPlayer(Npc npc)
        {
            try
            {
                return npc != null && npc.IsHostileTo(FactionIds.Player);
            }
            catch
            {
                return false;
            }
        }

        private bool SafeTargetIsPlayer(Npc npc)
        {
            try
            {
                return npc != null && npc.targetIsPlayer;
            }
            catch
            {
                return false;
            }
        }

        private bool SafeHasKnownPlayerPosition(Npc npc, Unit playerUnit)
        {
            try
            {
                return npc != null && npc.AiAgent != null && playerUnit != null && npc.AiAgent.HasKnownPosition(playerUnit);
            }
            catch
            {
                return false;
            }
        }

        private float SafeGetPlayerHp(Unit playerUnit)
        {
            try
            {
                return playerUnit != null ? playerUnit.GetNormalizedHealth() : 0f;
            }
            catch
            {
                return 0f;
            }
        }

        private Room GetUnitRoom(Unit unit)
        {
            if (unit == null)
            {
                return null;
            }

            if (unit.currentRoom != null)
            {
                return unit.currentRoom;
            }

            return unit.lastValidCurrentRoom;
        }

        private bool IsSameOrConnectedRoom(Room npcRoom, Room playerRoom)
        {
            if (npcRoom == null || playerRoom == null)
            {
                return false;
            }

            if (npcRoom == playerRoom)
            {
                return true;
            }

            if (playerRoom.connectedRooms == null)
            {
                return false;
            }

            for (int i = 0; i < playerRoom.connectedRooms.Length; i++)
            {
                if (playerRoom.connectedRooms[i] == npcRoom)
                {
                    return true;
                }
            }

            return false;
        }

        private int GetRoomId(Room room)
        {
            return room != null ? room.GetInstanceID() : 0;
        }

        private int GetStyle()
        {
            return Mathf.Clamp(_pressureStyle.Value, 1, 3);
        }

        private int GetTargetPressure()
        {
            switch (GetStyle())
            {
                case 1:
                    return _style1TargetPressure.Value;
                case 2:
                    return _style2TargetPressure.Value;
                default:
                    return _style3TargetPressure.Value;
            }
        }

        private float GetSpawnCooldown()
        {
            switch (GetStyle())
            {
                case 1:
                    return _style1Cooldown.Value;
                case 2:
                    return _style2Cooldown.Value;
                default:
                    return _style3Cooldown.Value;
            }
        }

        private int GetMaxSpawnPerWave()
        {
            switch (GetStyle())
            {
                case 1:
                    return _style1MaxSpawnPerWave.Value;
                case 2:
                    return _style2MaxSpawnPerWave.Value;
                default:
                    return _style3MaxSpawnPerWave.Value;
            }
        }

        private int GetMaxModAlive()
        {
            switch (GetStyle())
            {
                case 1:
                    return _style1MaxModAlive.Value;
                case 2:
                    return _style2MaxModAlive.Value;
                default:
                    return _style3MaxModAlive.Value;
            }
        }

        private int GetMaxModPerRoom()
        {
            switch (GetStyle())
            {
                case 1:
                    return _style1MaxModPerRoom.Value;
                case 2:
                    return _style2MaxModPerRoom.Value;
                default:
                    return _style3MaxModPerRoom.Value;
            }
        }

        private int GetMaxModPerLevel()
        {
            switch (GetStyle())
            {
                case 1:
                    return _style1MaxModPerLevel.Value;
                case 2:
                    return _style2MaxModPerLevel.Value;
                default:
                    return _style3MaxModPerLevel.Value;
            }
        }

        private void ResetRuntimeState(string reason)
        {
            _candidatePool.Clear();
            _recentEvents.Clear();
            _modSpawnedPerRoom.Clear();
            _deadNpcIds.Clear();

            _waveId = 0;
            _spawnedThisLevel = 0;
            _spawnedSinceLastOriginalKill = 0;
            _lastOriginalKillTime = Time.time;
            _lastModKillTime = -9999f;
            _lastDecision = "Reset";
            _lastBlockReason = SpawnBlockReason.None;
            _lastSpawnSummary = "None";
            _lastAggroReport = "None";
            _inLevelTransition = false;
            _nextSpawnTime = Time.time + 3f;

            AddEvent("Reset: " + reason);
        }

        private static void OnNpcDiePostfix(Npc __instance)
        {
            if (_instance == null || __instance == null)
            {
                return;
            }

            _instance.HandleNpcDeath(__instance);
        }

        private void HandleNpcDeath(Npc npc)
        {
            int id = npc.GetInstanceID();
            if (_deadNpcIds.Contains(id))
            {
                return;
            }

            _deadNpcIds.Add(id);

            bool isMod = npc.GetComponent<DynamicPressureSpawnMarker>() != null;
            bool hostile = IsHostileToPlayer(npc);

            if (!hostile)
            {
                return;
            }

            if (isMod)
            {
                _lastModKillTime = Time.time;
                AddEvent("Mod NPC died: " + npc.name);
            }
            else
            {
                _lastOriginalKillTime = Time.time;
                _spawnedSinceLastOriginalKill = 0;
                AddEvent("Original NPC died: " + npc.name);
            }
        }

        private static void OnLevelTransitionPrefix()
        {
            if (_instance == null)
            {
                return;
            }

            _instance._inLevelTransition = true;
            _instance.ResetRuntimeState("Level transition");
        }

        private void TryPatchTransitions()
        {
            MethodInfo prefix = AccessTools.Method(typeof(DynamicPressurePlugin), nameof(OnLevelTransitionPrefix));

            TryPatchMethod(typeof(NextLevelTrigger), "MakeTransition", prefix);
            TryPatchMethod(typeof(GameManager), "CompleteLevel", prefix);
            TryPatchAllNamedMethods(typeof(GameManager), "GoToLevel", prefix);
            TryPatchAllNamedMethods(typeof(GameManager), "GoToChurchHub", prefix);
            TryPatchAllNamedMethods(typeof(GameManager), "GoToCarHub", prefix);
            TryPatchAllNamedMethods(typeof(GameManager), "SwitchLevel", prefix);
        }

        private void TryPatchNpcDie()
        {
            try
            {
                MethodInfo target = AccessTools.Method(typeof(Npc), "Die");
                MethodInfo postfix = AccessTools.Method(typeof(DynamicPressurePlugin), nameof(OnNpcDiePostfix));

                if (target != null && postfix != null)
                {
                    _harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                    Log("Patched Npc.Die.");
                }
                else
                {
                    Logger.LogWarning("Failed to patch Npc.Die: method not found.");
                }
            }
            catch (Exception e)
            {
                Logger.LogWarning("Failed to patch Npc.Die: " + e.Message);
            }
        }

        private void TryPatchMethod(Type type, string methodName, MethodInfo prefix)
        {
            try
            {
                MethodInfo target = AccessTools.Method(type, methodName);
                if (target == null)
                {
                    Logger.LogWarning("Patch skipped: " + type.Name + "." + methodName);
                    return;
                }

                _harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                Log("Patched " + type.Name + "." + methodName);
            }
            catch (Exception e)
            {
                Logger.LogWarning("Patch failed: " + type.Name + "." + methodName + " / " + e.Message);
            }
        }

        private void TryPatchAllNamedMethods(Type type, string methodName, MethodInfo prefix)
        {
            try
            {
                MethodInfo[] methods = AccessTools.GetDeclaredMethods(type)
                    .Where(m => m.Name == methodName)
                    .ToArray();

                if (methods.Length == 0)
                {
                    Logger.LogWarning("Patch skipped: " + type.Name + "." + methodName);
                    return;
                }

                for (int i = 0; i < methods.Length; i++)
                {
                    _harmony.Patch(methods[i], prefix: new HarmonyMethod(prefix));
                }

                Log("Patched " + type.Name + "." + methodName + " x" + methods.Length);
            }
            catch (Exception e)
            {
                Logger.LogWarning("Patch failed: " + type.Name + "." + methodName + " / " + e.Message);
            }
        }

        private void AddEvent(string text)
        {
            _recentEvents.Add(DateTime.Now.ToString("HH:mm:ss") + " " + text);
            while (_recentEvents.Count > 8)
            {
                _recentEvents.RemoveAt(0);
            }
        }

        private void Log(string text)
        {
            Logger.LogInfo(text);
        }

        private void OnGUI()
        {
            if (!_enableOverlay.Value)
            {
                return;
            }

            Rect rect = new Rect(20f, 120f, 620f, 620f);
            GUI.Box(rect, "Dynamic Pressure Debug");

            GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 25f, rect.width - 20f, rect.height - 35f));

            GUILayout.Label("Enabled: " + _enableMod.Value + " | AutoSpawn: " + _enableAutoSpawn.Value + " | Style: " + GetStyle());
            GUILayout.Label("GameState: " + _snapshot.gameState + " | SafeZone: " + _snapshot.inSafeZone + " | Env: " + _snapshot.currentEnvironment);
            GUILayout.Label("Player: " + (_snapshot.hasPlayer ? "OK" : "Missing") + " | Alive: " + _snapshot.playerAlive + " | HP: " + Mathf.RoundToInt(_snapshot.playerHp * 100f) + "% | DamageAgo: " + _snapshot.playerTimeSinceDamage.ToString("0.0") + "s");
            GUILayout.Label("Room: " + _snapshot.playerRoomName + " | EndRoom: " + _snapshot.playerRoomIsEndRoom);

            GUILayout.Space(8f);
            GUILayout.Label("Pressure");
            GUILayout.Label("Original Hostile Alive: " + _snapshot.originalHostileAlive + " | Engaged: " + _snapshot.originalEngagedHostiles + " | Targeting: " + _snapshot.originalTargetingPlayer);
            GUILayout.Label("Mod Hostile Alive: " + _snapshot.modHostileAlive + " | Targeting: " + _snapshot.modTargetingPlayer);
            GUILayout.Label("Original Pressure: " + _snapshot.originalPressure + " | Mod Pressure: +" + _snapshot.modPressure + " | Current: " + _snapshot.currentPressure + " / Target: " + _snapshot.targetPressure + " | Deficit: " + _snapshot.deficit);

            GUILayout.Space(8f);
            GUILayout.Label("Spawn Decision");
            GUILayout.Label("Last Decision: " + _lastDecision + " | Reason: " + _lastBlockReason);
            GUILayout.Label("Next Spawn In: " + _snapshot.nextSpawnIn.ToString("0.0") + "s | Candidate Source: " + _snapshot.candidateSource + " | Candidates: " + _snapshot.candidateCount);

            GUILayout.Space(8f);
            GUILayout.Label("Mod Impact");
            GUILayout.Label("Spawned This Level: " + _spawnedThisLevel + " / " + GetMaxModPerLevel());
            GUILayout.Label("Spawned Since Last Original Kill: " + _spawnedSinceLastOriginalKill);
            GUILayout.Label("Time Since Original Kill: " + _snapshot.timeSinceLastOriginalKill.ToString("0.0") + "s | Time Since Mod Kill: " + _snapshot.timeSinceLastModKill.ToString("0.0") + "s");
            GUILayout.Label("Would Delay OnAllEnemiesDead: " + _snapshot.wouldDelayOnAllEnemiesDead);
            GUILayout.Label("Last Spawn: " + _lastSpawnSummary);
            GUILayout.Label("Last Aggro Report: " + _lastAggroReport);

            GUILayout.Space(8f);
            GUILayout.Label("Keys: F8 Manual Spawn | F9 Toggle Overlay");

            GUILayout.Space(8f);
            GUILayout.Label("Recent Events");
            for (int i = 0; i < _recentEvents.Count; i++)
            {
                GUILayout.Label(_recentEvents[i]);
            }

            GUILayout.EndArea();
        }

        private struct SpawnPointChoice
        {
            public Vector3 position;
            public Room room;
            public float distanceToPlayer;
            public string source;
        }

        private sealed class Snapshot
        {
            public bool hasGameManager;
            public bool hasPlayer;
            public bool playerAlive;
            public bool inSafeZone;
            public string gameState = "Unknown";
            public string currentEnvironment = "Unknown";

            public float playerHp;
            public float playerTimeSinceDamage;
            public string playerRoomName = "null";
            public bool playerRoomIsEndRoom;

            public int aliveNpcs;
            public int originalHostileAlive;
            public int originalEngagedHostiles;
            public int nearbyOriginalHostiles;
            public int originalTargetingPlayer;

            public int modHostileAlive;
            public int nearbyModHostiles;
            public int modTargetingPlayer;

            public int originalPressure;
            public int modPressure;
            public int currentPressure;
            public int targetPressure;
            public int deficit;

            public int candidateCount;
            public string candidateSource = "None";

            public int spawnedThisLevel;
            public int spawnedSinceLastOriginalKill;
            public float timeSinceLastOriginalKill;
            public float timeSinceLastModKill;
            public float nextSpawnIn;

            public bool wouldDelayOnAllEnemiesDead;
        }
    }

    public sealed class DynamicPressureSpawnMarker : MonoBehaviour
    {
        public UnitSO sourceUnitSo;
        public float spawnTime;
        public Vector3 spawnPosition;
        public Room spawnRoom;
        public string spawnReason;
        public int waveId;

        public bool aggroReported;
        public float lastAggroReportTime;
        public bool targetingPlayerAfterReport;
        public bool hasKnownPlayerPositionAfterReport;
    }

    public enum SpawnBlockReason
    {
        None,
        Disabled,
        NoGameManager,
        NoPlayer,
        PlayerDead,
        SafeZone,
        GameStateNotRunning,
        LowHealth,
        RecentDamage,
        EndRoomBlocked,
        SpawnRoomIsEndRoom,
        NoOriginalHostiles,
        NoEngagedOriginalHostiles,
        NotEnoughOriginalHostiles,
        NoCandidateUnits,
        NoValidNpcSpawnPoint,
        Cooldown,
        MaxModSpawnedAlive,
        RoomSpawnBudgetExceeded,
        LevelSpawnBudgetExceeded,
        PressureAlreadyHigh,
        SpawnAsyncFailed,
        LevelTransition,
        RecentModKillOnly,
        NoOriginalKillProgress,
        SpawnBudgetPerOriginalExceeded
    }
}