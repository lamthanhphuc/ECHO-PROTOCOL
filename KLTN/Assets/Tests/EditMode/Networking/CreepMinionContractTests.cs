using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EchoProtocol.Networking.Tests
{
    public sealed class CreepMinionContractTests
    {
        private const string MinionSource = "Assets/Scripts/AI/Minions/CreepMinionRuntime.cs";
        private const string SpawnerSource = "Assets/_Project/Scripts/Networking/Player/PlayerSpawner.cs";

        [Test]
        public void MinionBodyIsHalfPlayerHeight_AndVisualIsScaledToMatch()
        {
            var minion = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/PF_CreepMinionNetwork.prefab");
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerNetwork.prefab");
            Assert.That(player.GetComponent<CharacterController>().height, Is.EqualTo(2f));
            Assert.That(minion.GetComponent<CapsuleCollider>().height, Is.EqualTo(1f));
            Assert.That(minion.GetComponent<UnityEngine.AI.NavMeshAgent>().height, Is.EqualTo(1f));
            string source = File.ReadAllText(MinionSource);
            StringAssert.Contains("_agent.height = 1f", source);
            StringAssert.Contains("_visualRoot.localScale *= 1f / visualBounds.size.y", source);
        }

        [Test]
        public void CreepAlert_HasRejectedAlertRetry()
        {
            string source = File.ReadAllText(MinionSource);
            StringAssert.Contains("_stalkerAlertDeliveredForTarget", source);
            StringAssert.Contains("alertRetrySeconds", source);
            StringAssert.Contains("TrySendStalkerAlert", source);
        }

        [Test]
        public void CreepSabotageMovement_RequiresCompletePaths()
        {
            string source = File.ReadAllText(MinionSource);
            string toolRelocation = ExtractMethod(source,
                "private bool TryFindSabotageDropPosition",
                "private bool TryStealCore");
            string coreFlee = ExtractMethod(source,
                "private bool TryFindFleeDestination",
                "public void ReleaseStolenCoreAuthoritative");

            StringAssert.Contains("NavMesh.CalculatePath", toolRelocation);
            StringAssert.Contains("NavMeshPathStatus.PathComplete", toolRelocation);
            StringAssert.Contains("NavMesh.CalculatePath", coreFlee);
            StringAssert.Contains("NavMeshPathStatus.PathComplete", coreFlee);
        }

        [Test]
        public void ToolDive_FallsBackAndOnlyFleesWithStolenTool()
        {
            string source = File.ReadAllText(MinionSource);
            string beginToolDive = ExtractMethod(source,
                "private void BeginToolDive",
                "private void TickToolDive");
            string tickToolDive = ExtractMethod(source,
                "private void TickToolDive",
                "public void GetToolCarryPose");

            StringAssert.Contains("&& !TryFindFleeDestination(", beginToolDive);

            int guard = tickToolDive.LastIndexOf("if (StolenToolId.IsValid)",
                System.StringComparison.Ordinal);
            int blockStart = tickToolDive.IndexOf('{', guard);
            int flee = tickToolDive.LastIndexOf("BeginFleeTo(",
                System.StringComparison.Ordinal);
            int blockEnd = tickToolDive.IndexOf('}', blockStart);

            Assert.That(guard, Is.GreaterThanOrEqualTo(0));
            Assert.That(flee, Is.GreaterThan(blockStart));
            Assert.That(flee, Is.LessThan(blockEnd));
        }

        [Test]
        public void Minions_UseStalkerAnchorFromTheirZone()
        {
            string source = File.ReadAllText(SpawnerSource);
            string anchor = ExtractMethod(
                source,
                "private bool TryGetCreepMinionSpawnAnchor(",
                "private bool TryFindCreepMinionSpawnPosition(");

            StringAssert.Contains("RegionSemanticZone.Zone03", anchor);
            StringAssert.Contains("_zone3MonsterInstance", anchor);
            StringAssert.Contains("RegionSemanticZone.Zone02", anchor);
            StringAssert.Contains("_zone2MonsterInstance", anchor);
            StringAssert.Contains("_monsterInstance", anchor);
            StringAssert.Contains("IsValidNetworkObject(stalker)", anchor);

            StringAssert.Contains(
                "TryGetCreepMinionSpawnAnchor(zone, out var stalker)",
                source);
            StringAssert.Contains("profile.Zone3MinionCap", source);
        }

        [Test]
        public void CreepPrefab_SerializesAlertRetrySeconds()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/PF_CreepMinionNetwork.prefab");
            Assert.That(prefab, Is.Not.Null);

            var minion = FindComponentByTypeName(
                prefab,
                "EchoProtocol.AI.Minions.CreepMinionRuntime");
            Assert.That(minion, Is.Not.Null);

            var retry = new SerializedObject(minion).FindProperty("alertRetrySeconds");
            Assert.That(retry, Is.Not.Null);
            Assert.That(retry.floatValue, Is.EqualTo(0.15f).Within(0.001f));
        }

        [Test]
        public void UnifiedMinion_ContainsAllMechanics()
        {
            string source = File.ReadAllText(MinionSource);

            StringAssert.Contains("CreepMinionAttackKind.ShootSlow", source);
            StringAssert.Contains("CreepMinionAttackKind.StealTool", source);
            StringAssert.Contains("CreepMinionAttackKind.StealCore", source);
            StringAssert.Contains("TryApplySlowAuthoritative", source);
            StringAssert.Contains("TryTransferTeamToolToMonster", source);
            StringAssert.Contains("TryStealCore", source);
            StringAssert.Contains("BeginFleeTo", source);
            StringAssert.Contains("TrySendStalkerAlert", source);
            StringAssert.Contains("RuntimeNoiseType.NOISE_MAKER", source);
            StringAssert.Contains("IsStabilizerBuffed", source);
            StringAssert.Contains("BeginFlashlightDeath", source);
        }

        [Test]
        public void EveryNormalAttack_HasRecovery_AndDisruptorStillAttacksWhenSlowIsActive()
        {
            string source = File.ReadAllText(MinionSource);
            string fixedUpdate = ExtractMethod(source,
                "public override void FixedUpdateNetwork",
                "private void TickStationaryRecovery");
            string attack = ExtractMethod(source,
                "private void AttackPlayer",
                "private void CommitAttack");
            string commit = ExtractMethod(source,
                "private void CommitAttack",
                "private float StealClipSeconds");
            string dive = ExtractMethod(source,
                "private void TickToolDive",
                "public void GetToolCarryPose");

            StringAssert.DoesNotContain("!AttackRecovery.ExpiredOrNotRunning(Runner)", fixedUpdate);
            StringAssert.Contains("!AttackRecovery.ExpiredOrNotRunning(Runner)", attack);
            StringAssert.Contains("AttackRecovery = TickTimer.CreateFromSeconds", commit);
            StringAssert.Contains("attackRecoverySeconds);", commit);
            StringAssert.DoesNotContain("IsDisruptor ? attackCooldownSeconds : attackRecoverySeconds", commit);
            StringAssert.Contains("disruptorMovement.TryApplySlowAuthoritative", attack);
            StringAssert.DoesNotContain("|| !disruptorMovement.TryApplySlowAuthoritative", attack);
            StringAssert.DoesNotContain("|| !movement.TryApplySlowAuthoritative", attack);
            StringAssert.Contains("CommitAttack(CreepMinionAttackKind.MeleeSlow)", attack);
            Assert.That(attack.IndexOf("CommitAttack(CreepMinionAttackKind.MeleeSlow)", System.StringComparison.Ordinal),
                Is.LessThan(attack.IndexOf("life.TryApplyAuthoritativeDamage(AttackDamage, \"MINION_DISRUPTOR\"", System.StringComparison.Ordinal)));
            StringAssert.Contains(": CreepMinionAttackKind.ShootSlow", dive);
            StringAssert.Contains("CommitAttack(attackKind)", dive);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/PF_CreepMinionNetwork.prefab");
            var runtime = FindComponentByTypeName(prefab, "EchoProtocol.AI.Minions.CreepMinionRuntime");
            Assert.That(new SerializedObject(runtime).FindProperty("attackRecoverySeconds").floatValue,
                Is.EqualTo(3f));
        }

        [Test]
        public void AlertSlowAndToolSteal_AreConcurrent_AndToolDiveApproachesFromPlayerFront()
        {
            string source = File.ReadAllText(MinionSource);
            string targeting = ExtractMethod(source,
                "private void UpdateTargetAndMovement",
                "private void TickStalkerAlertObservation");
            string attack = ExtractMethod(source,
                "private void AttackPlayer",
                "private void CommitAttack");
            string dive = ExtractMethod(source,
                "private void TickToolDive",
                "public void GetToolCarryPose");

            StringAssert.Contains("TickStalkerAlertObservation(targetObject)", targeting);
            StringAssert.Contains("AttackPlayer(targetObject, lifeState)", targeting);
            StringAssert.Contains("BeginToolDive(playerObject, canSteal)", attack);
            StringAssert.Contains("TickStalkerAlertObservation(target)", dive);
            StringAssert.Contains("target.transform.forward", dive);
            StringAssert.Contains("target.transform.position + playerForward * 1.35f", dive);
            StringAssert.Contains(": CreepMinionAttackKind.ShootSlow", dive);
            StringAssert.Contains("CommitAttack(attackKind)", dive);
            StringAssert.Contains("if (ReplicatedToolDiveApproaching)", dive);
            StringAssert.Contains("if (frontDistance > 1.4f)", dive);
            StringAssert.Contains("ToolDivePhase = 0", dive);
        }

        [Test]
        public void MinionSlowAttacks_DealLethalDamage_AndToolDiveUsesLowHeight()
        {
            string source = File.ReadAllText(MinionSource);
            string movement = File.ReadAllText(
                "Assets/_Project/Scripts/Networking/Player/NetworkPlayerMovement.cs");
            string attack = ExtractMethod(source,
                "private void AttackPlayer",
                "private void CommitAttack");
            string dive = ExtractMethod(source,
                "private void BeginToolDive",
                "public void GetToolCarryPose");

            StringAssert.Contains("life.TryApplyAuthoritativeDamage(AttackDamage", attack);
            StringAssert.Contains("life.TryApplyAuthoritativeDamage(AttackDamage, \"MINION\"", dive);
            StringAssert.Contains("TryApplyKnockbackAuthoritative", attack);
            StringAssert.Contains("TryApplyKnockbackAuthoritative", dive);
            StringAssert.Contains("public bool TryApplyKnockbackAuthoritative", movement);
            StringAssert.Contains("ExternalKnockbackTimer", movement);
            StringAssert.Contains("private bool TickExternalKnockback", movement);
            StringAssert.Contains("if (TickExternalKnockback())", movement);
            StringAssert.Contains("FloodedWaterBoundary.ConstrainMotion", movement);
            StringAssert.Contains("_unityCharacterController.Move(motion)", movement);
            StringAssert.Contains("_controller.Teleport(transform.position", movement);
            StringAssert.Contains("RpcPlayAttackPresentation((int)kind)", source);
            StringAssert.Contains("private const float ToolDiveHeight = 0.35f", source);
            StringAssert.Contains("FlightHeightValue = ToolDiveHeight", dive);
            StringAssert.Contains("Mathf.Lerp(ReplicatedDiveStartHeight, ToolDiveHeight", dive);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/PF_CreepMinionNetwork.prefab");
            var runtime = FindComponentByTypeName(prefab, "EchoProtocol.AI.Minions.CreepMinionRuntime");
            StringAssert.Contains("private const float AttackDamage = 10f;", source);
            Assert.That(new SerializedObject(runtime).FindProperty("attackDamage"), Is.Null);
            Assert.That(new SerializedObject(runtime).FindProperty("attackKnockbackSpeed").floatValue,
                Is.EqualTo(10f));
            Assert.That(new SerializedObject(runtime).FindProperty("attackKnockbackSeconds").floatValue,
                Is.EqualTo(0.2f));
        }

        [Test]
        public void LockedTarget_IsFollowedAndAttackedOutsideInitialFieldOfView()
        {
            string source = File.ReadAllText(MinionSource);
            string targeting = ExtractMethod(source,
                "private void UpdateTargetAndMovement",
                "private void TickStalkerAlertObservation");
            string selection = ExtractMethod(source,
                "private PlayerRef FindBestVisibleTarget",
                "private PlayerRef FindClosestEligibleTarget");
            string sight = ExtractMethod(source,
                "private bool CanSeePlayer",
                "private void EmitStalkerAlert");
            string attack = ExtractMethod(source,
                "private void AttackPlayer",
                "private void CommitAttack");

            StringAssert.Contains("HasLineOfSight(current)", selection);
            StringAssert.Contains("return TargetPlayer", selection);
            StringAssert.Contains("return HasLineOfSight(playerObject)", sight);
            StringAssert.Contains("private bool HasLineOfSight", sight);
            StringAssert.Contains("distance > DisruptorAttackRange || !HasLineOfSight(playerObject)", attack);
            StringAssert.Contains("? DisruptorAttackRange", source);
            StringAssert.Contains("Mathf.Min(meleeRange, 2f)", source);
            StringAssert.Contains("IsDisruptor ? 1.8f : 0.4f", source);
            StringAssert.Contains("Vector3.ProjectOnPlane(targetObject.transform.position - transform.position, Vector3.up).magnitude", targeting);
            StringAssert.Contains("Vector3.ProjectOnPlane(playerObject.transform.position - transform.position, Vector3.up).magnitude", attack);
            StringAssert.Contains("if (IsDisruptor)", targeting);
            StringAssert.Contains("StateValue = (int)CreepMinionState.Harass", targeting);
            StringAssert.Contains("TrySeekPlayerOnNavMesh(targetObject.transform.position)", targeting);
            StringAssert.Contains("if (!HasLineOfSight(targetObject))", source);
        }

        [Test]
        public void OriginalMinion_UsesVisibleAttack2AnimationForSlowAttacks()
        {
            string source = File.ReadAllText(MinionSource);
            StringAssert.Contains(": \"Attack2\");", source);
            StringAssert.DoesNotContain(": \"Shoot\");", source);
            StringAssert.Contains("animator.HasState(0, stateHash)", source);
            StringAssert.Contains("animator.Play(stateHash, 0, 0f)", source);
        }

        [Test]
        public void BothZones_UseSameUnifiedMinionPrefab()
        {
            string source = File.ReadAllText(SpawnerSource);

            StringAssert.Contains("ResolveCreepMinionPrefab()", source);
            StringAssert.Contains("ConfigureBeforeSpawn(", source);
            StringAssert.Contains("GetComponent<CreepMinionRuntime>()", source);
        }

        [Test]
        public void MinionResearchBaselineMatchesNormalProduction()
        {
            var difficultyType = System.Type.GetType(
                "EchoProtocol.Gameplay.MatchDifficulty, Assembly-CSharp");
            var profilesType = System.Type.GetType(
                "EchoProtocol.Gameplay.MatchDifficultyProfiles, Assembly-CSharp");
            Assert.That(difficultyType, Is.Not.Null);
            Assert.That(profilesType, Is.Not.Null);
            var normal = profilesType.GetMethod("Get").Invoke(
                null, new[] { System.Enum.Parse(difficultyType, "Normal") });
            Assert.That((int)normal.GetType().GetProperty("Zone1MinionCap").GetValue(normal), Is.EqualTo(3));
            Assert.That((int)normal.GetType().GetProperty("Zone2MinionCap").GetValue(normal), Is.EqualTo(5));
            Assert.That((int)normal.GetType().GetProperty("Zone3MinionCap").GetValue(normal), Is.EqualTo(0));

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/PF_CreepMinionNetwork.prefab");
            Assert.That(prefab, Is.Not.Null);
            var minion = FindComponentByTypeName(prefab,
                "EchoProtocol.AI.Minions.CreepMinionRuntime");
            Assert.That(minion, Is.Not.Null);
            var serialized = new SerializedObject(minion);
            Assert.That(serialized.FindProperty("visionRange").floatValue, Is.EqualTo(20f));
            Assert.That(serialized.FindProperty("trackSpeed").floatValue, Is.EqualTo(8f));
            Assert.That(serialized.FindProperty("harassSpeed").floatValue, Is.EqualTo(10f));
            Assert.That(serialized.FindProperty("attackCooldownSeconds").floatValue, Is.EqualTo(0.65f));
            Assert.That(serialized.FindProperty("alertCooldownSeconds").floatValue, Is.EqualTo(4f));
            Assert.That(serialized.FindProperty("stealToolRange").floatValue, Is.EqualTo(3f));
            Assert.That(serialized.FindProperty("toolFleeSeconds").floatValue, Is.EqualTo(15f));
            Assert.That(serialized.FindProperty("stolenToolDropMinDistance").floatValue, Is.EqualTo(12f));
            Assert.That(serialized.FindProperty("stolenToolDropMaxDistance").floatValue, Is.EqualTo(45f));
        }

        [Test]
        public void MinionProductionSpawnKeepsSafetyConstraints()
        {
            string source = File.ReadAllText(SpawnerSource);
            StringAssert.Contains("_minionSpawnMinDistanceFromPlayer = 18f", source);
            StringAssert.Contains("_minionSpawnCheckInterval = 3f", source);
            StringAssert.Contains("NavMeshPathStatus.PathComplete", source);
            StringAssert.Contains("TryGetCreepMinionSpawnAnchor(zone, out var stalker)", source);
            StringAssert.Contains("IsCreepMinionSpawnFarFromPlayers", source);
        }

        [Test]
        public void ToolDivePreservesAEDEvidenceAndFleeGuard()
        {
            string source = File.ReadAllText(MinionSource);
            string dive = ExtractMethod(source, "private void TickToolDive()", "public void GetToolCarryPose");
            string networkTick = ExtractMethod(source, "public override void FixedUpdateNetwork()", "private void UpdateTargetAndMovement()");
            StringAssert.Contains("AEDMinionFactKindV1.AttackAttempted", dive);
            StringAssert.Contains("effectKind: \"CORE\"", dive);
            StringAssert.Contains("effectKind: \"TOOL\"", dive);
            StringAssert.Contains("accepted: forcedDrop", dive);
            StringAssert.Contains("accepted: relocated", dive);
            StringAssert.Contains("PublishAEDSnapshot();", networkTick);
            StringAssert.Contains("|| StolenToolId.IsValid", source);
            StringAssert.Contains("if (StolenToolId.IsValid)\n" +
                "            {\n" +
                "                ReplicatedToolRouteRetries = 0;\n" +
                "                BeginFleeTo(", dive.Replace("\r\n", "\n"));
        }

        [Test]
        public void MinionAbilities_AreIndependent()
        {
            string source = File.ReadAllText(MinionSource);

            string tracking = ExtractMethod(
                source,
                "private void UpdateTargetAndMovement()",
                "private void TickStalkerAlertObservation(");

            string attack = ExtractMethod(
                source,
                "private void AttackPlayer(",
                "private void CommitAttack(");

            StringAssert.Contains("TickStalkerAlertObservation(targetObject)", tracking);
            StringAssert.Contains("AttackPlayer(targetObject, lifeState)", tracking);
            Assert.That(tracking.IndexOf("TickStalkerAlertObservation(targetObject)"),
                Is.LessThan(tracking.IndexOf("AttackPlayer(targetObject, lifeState)")));
            StringAssert.DoesNotContain("_stalkerAlertDeliveredForTarget", attack);
            StringAssert.Contains("BeginToolDive(playerObject, canSteal)", attack);
        }

        [Test]
        public void MinionSteal_RequiresFrontView()
        {
            string source = File.ReadAllText(MinionSource);
            string dive = ExtractMethod(source, "private void TickToolDive()", "public void GetToolCarryPose");

            StringAssert.Contains(
                "private bool IsInsidePlayerViewCone(",
                source);

            StringAssert.Contains(
                "IsInsidePlayerViewCone(",
                dive);

            StringAssert.Contains(
                "PlayerCanSeeMinion(",
                dive);
        }

        [Test]
        public void Disruptor_UsesNetworkedRoleAndCyclesAllMonster10Visuals()
        {
            string minion = File.ReadAllText(MinionSource);
            string spawner = File.ReadAllText(SpawnerSource);
            string flashlight = File.ReadAllText("Assets/_Project/Scripts/Networking/Player/NetworkPlayerFlashlight.Resonance.cs");
            StringAssert.Contains("[Networked] public int RoleValue", minion);
            StringAssert.Contains("[Networked] public int VisualVariantValue", minion);
            StringAssert.Contains("CreepMinionAttackKind.MeleeSlow", minion);
            StringAssert.Contains("InterruptZone2OperationsAuthoritative", minion);
            StringAssert.Contains("[Networked] public NetworkBool IsUvStunned", minion);
            StringAssert.Contains("_agent.isStopped = true", minion);
            StringAssert.Contains("if (IsUvStunned)", minion);
            StringAssert.Contains("CrossFade(\"GetHit\")", minion);
            StringAssert.Contains("private const float ResonanceDeathSeconds = 1f", minion);
            StringAssert.Contains("private const float ResonanceHitRadius = 0.75f", flashlight);
            StringAssert.Contains("Physics.SphereCastNonAlloc(origin, ResonanceHitRadius", flashlight);
            StringAssert.Contains("CrossFade(\"Die\")", minion);
            StringAssert.Contains("DeathClipSeconds()", minion);
            StringAssert.Contains("_agent.radius = 0.65f", minion);
            StringAssert.Contains("NavMesh.GetSettingsByID(_agent.agentTypeID).agentRadius", minion);
            StringAssert.Contains("if (bakedRadius > 0f) _agent.radius = bakedRadius", minion);
            StringAssert.Contains("!currentlyInWater && !_catfishPatrolCrossing && TryActivateAgent()", minion);
            StringAssert.Contains("_navigation.ClearDestinationCache()", minion);
            StringAssert.Contains("currentlyInWater ? ResolveSwimHeight(surfaceY)", minion);
            StringAssert.Contains("capsule.center = Vector3.up * 0.5f", minion);
            StringAssert.Contains("capsule.radius = Mathf.Min(_agent.radius, 0.5f)", minion);
            StringAssert.Contains("Physics.IgnoreCollision(_bodyCollider, playerCollider, true)", minion);
            StringAssert.DoesNotContain("private void LateUpdate()", minion);
            StringAssert.Contains("capsule.center = _bodyColliderBaseCenter + Vector3.up * localHeight", minion);
            StringAssert.Contains("_agent.isStopped = true", minion);
            StringAssert.DoesNotContain("_agent.isStopped = false", minion);
            StringAssert.Contains("(_nextCreepMinionVisualVariant + 1) % 30", spawner);
            StringAssert.Contains(
                "SceneManager.GetActiveScene().name != LobbyManager.GameSceneName",
                spawner);
            StringAssert.Contains("disruptorCount < originalCount", spawner);
            StringAssert.Contains("int count = originalCount + disruptorCount", spawner);
            StringAssert.Contains("TryFindCatfishSpawnPosition(runner, out var catfishPosition)", spawner);
            StringAssert.Contains("if (map2 && disruptor)", spawner);
            StringAssert.Contains("SpawnCreepMinion(runner, catfishPosition, zone, true)", spawner);
            StringAssert.Contains("playerPositions[attempt % playerPositions.Count]", spawner);
            StringAssert.Contains("TryGetCreepMinionSpawnAnchor(zone, out var fallbackStalker)", spawner);
            StringAssert.Contains("SpawnCreepMinion(runner, landPosition, zone, true)", spawner);
            StringAssert.Contains("FloodedWaterBoundary.TryGetWaterBounds(out var water)", spawner);
            StringAssert.Contains("Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)", spawner);
            StringAssert.Contains("IsInsideBlockerBoundary(blockers, candidate, surfaceY", spawner);
            StringAssert.Contains("IsCatfishSpawnInPlayerArea(runner, candidate, surfaceY, focus)", spawner);
            StringAssert.Contains("hit.collider.name.StartsWith(\"_BLOCKERCUBE\")", spawner);
            StringAssert.Contains("path.status == NavMeshPathStatus.PathComplete", spawner);
            StringAssert.Contains("bool approachingLand = _catfishWasInWater && (!enteringShallowWater", minion);
            StringAssert.Contains("!FloodedWaterBoundary.TryGetSwimSurface(destination, out _, .1f)", minion);
            StringAssert.Contains("NavMesh.SamplePosition(nextPosition, out var landHit, 6f", minion);
            StringAssert.Contains("Vector3.Dot(", minion);
            StringAssert.Contains("Vector3.MoveTowards(transform.position, _catfishLandEntry", minion);
            StringAssert.Contains("_catfishOnLand = true", minion);
            StringAssert.Contains("bool enteringShallowWater = FloodedWaterBoundary.TryGetSwimSurface(nextPosition, out _, 0.1f)", minion);
            StringAssert.Contains("if (visible.IsRealPlayer)", minion);
            StringAssert.Contains("destination = ReplicatedDistractionPoint", minion);
            StringAssert.Contains("private const float PlayerSeekTimeoutSeconds = 20f", minion);
            StringAssert.Contains("TargetPlayer = FindClosestEligibleTarget()", minion);
            StringAssert.Contains("if (_seekingPlayer)", minion);
            StringAssert.Contains("_lastVisiblePlayerAt = Time.time", minion);
            StringAssert.Contains("private void CancelForcedPlayerSeek()", minion);
            StringAssert.Contains("if (_seekingPlayer) CancelForcedPlayerSeek()", minion);
            StringAssert.Contains("_swimDestination = transform.position", minion);
            StringAssert.Contains("_nextRoamRetargetAt = 0f", minion);
            StringAssert.Contains("_navigation.TickProgress(Runner.DeltaTime)", minion);
            StringAssert.Contains("execution == NavigationExecutionStatus.Stuck", minion);
            StringAssert.Contains("RecoverFromBlockedNavigation()", minion);
            StringAssert.Contains("new Vector3(destination.x, origin.y, destination.z)", minion);
            StringAssert.Contains("_navigation.EvaluateDestination(destination).IsComplete", minion);
            StringAssert.DoesNotContain("_agent.SetDestination", minion);
            StringAssert.Contains("TrySeekPlayerOnNavMesh(targetObject.transform.position)", minion);
            StringAssert.Contains("TryNavigateOrFly(hit.position)", minion);
            StringAssert.Contains("private bool IsMap2Flyer", minion);
            StringAssert.Contains("CrossesMap2Water(destination)", minion);
            StringAssert.Contains("private void TickMap2Flight()", minion);
            StringAssert.Contains("if (IsMap2Flyer && TryNavigateOrFly(hit.position)) return true", minion);
            StringAssert.Contains("if (CrossesMap2Water(hit.position))", minion);
            StringAssert.Contains("_catfishOnLand = false", minion);
            StringAssert.Contains("_catfishWasInWater = false", minion);
            StringAssert.Contains("map2 ? false : disruptor", spawner);
            StringAssert.Contains("(minion.transform.position - hit.position).sqrMagnitude < 4f", spawner);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/PF_CreepMinionNetwork.prefab");
            var variants = prefab != null ? prefab.transform.Find("VisualVariants") : null;
            Assert.That(variants, Is.Not.Null);
            var minionRuntime = FindComponentByTypeName(prefab, "EchoProtocol.AI.Minions.CreepMinionRuntime");
            Assert.That(new SerializedObject(minionRuntime).FindProperty("noiseMakerDistractionSeconds").floatValue,
                Is.EqualTo(12f));
            Assert.That(variants.childCount, Is.EqualTo(18));
            Assert.That(variants.GetChild(1).name, Is.EqualTo("Monster10_01"));
            Assert.That(variants.GetChild(15).name, Is.EqualTo("Monster10_15"));
            Assert.That(variants.GetChild(16).name, Is.EqualTo("CatfishA"));
            Assert.That(variants.GetChild(17).name, Is.EqualTo("CatfishB"));

            var originalController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Animations/Minion/AC_CreepMinion.controller");
            var disruptorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Animations/Minion/AC_DisruptorMinion.controller");
            Assert.That(System.Array.Exists(originalController.animationClips, clip => clip.name == "Monster08_GetHit"));
            Assert.That(System.Array.Exists(originalController.animationClips, clip => clip.name == "Monster08_Die"));
            Assert.That(System.Array.Exists(disruptorController.animationClips, clip => clip.name == "Monster10_GetHit"));
            Assert.That(System.Array.Exists(disruptorController.animationClips, clip => clip.name == "Monster10_Die"));

            var originalGetHit = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/Stylized3DMonster/Monster08/Anim/Monster08_GetHit.anim");
            var disruptorGetHit = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/Stylized3DMonster/Monster10/Anim/InPlace_Anim/Monster10_GetHit.anim");
            Assert.That(AnimationUtility.GetAnimationClipSettings(originalGetHit).loopTime, Is.True);
            Assert.That(AnimationUtility.GetAnimationClipSettings(disruptorGetHit).loopTime, Is.True);
        }

        [Test]
        public void Map2Catfish_UsesDistinctUrpMaterialsAndRequiredAnimations()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/PF_CreepMinionNetwork.prefab");
            var variants = prefab.transform.Find("VisualVariants");
            var first = variants.GetChild(16).GetComponentInChildren<Renderer>(true).sharedMaterial;
            var second = variants.GetChild(17).GetComponentInChildren<Renderer>(true).sharedMaterial;
            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(first.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            Assert.That(second.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            Assert.That(first.GetTexture("_BaseMap"), Is.Not.EqualTo(second.GetTexture("_BaseMap")));

            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(
                "Assets/Animations/Minion/AC_CatfishMinion.controller");
            var states = controller.layers[0].stateMachine.states;
            foreach (string name in new[] { "Idle", "Walk", "Run", "Attack01", "GetHit", "Die" })
                Assert.That(System.Array.Exists(states, state => state.state.name == name), Is.True, name);
            var walk = System.Array.Find(states, state => state.state.name == "Walk").state.motion;
            Assert.That(walk, Is.Not.Null);
            Assert.That(walk.name, Is.EqualTo("walk"));
            Assert.That(AnimationUtility.GetAnimationClipSettings(
                System.Array.Find(controller.animationClips, clip => clip.name == "die")).loopTime, Is.False);
            Assert.That(AnimationUtility.GetAnimationClipSettings(
                System.Array.Find(controller.animationClips, clip => clip.name == "hit")).loopTime, Is.True);
        }

        [Test]
        public void CreepHostMigration_PersistsFleeAndToolDiveAuthoritativeState()
        {
            string source = File.ReadAllText(MinionSource);
            StringAssert.Contains("[Networked] private Vector3 ReplicatedFleeDestination", source);
            StringAssert.Contains("[Networked] private float ReplicatedDiveDuration", source);
            StringAssert.Contains("[Networked] private NetworkBool ReplicatedToolContactResolved", source);
            StringAssert.Contains("[Networked] private Vector3 ReplicatedToolVictimPosition", source);
            StringAssert.Contains("[Networked] private int ReplicatedToolRouteRetries", source);
            Assert.That(source.Contains("out ReplicatedFleeDestination"), Is.False);
        }

        [Test]
        public void CreepHostMigration_PersistsDistractionAndDetectionProgress()
        {
            string source = File.ReadAllText(MinionSource);
            StringAssert.Contains("[Networked] private TickTimer ReplicatedDistractionTimer", source);
            StringAssert.Contains("[Networked] private Vector3 ReplicatedDistractionPoint", source);
            StringAssert.Contains("[Networked] private float ReplicatedLostSightSeconds", source);
            StringAssert.Contains("[Networked] private float ReplicatedFlashlightExposureSeconds", source);
            StringAssert.Contains("ReplicatedDistractionTimer.ExpiredOrNotRunning(Runner)", source);
            StringAssert.Contains("HM_AI_MINION_RESTORE", source);
        }

        [Test]
        public void StalkerHostMigration_LogsSnapshotAndFirstSimulationDivergence()
        {
            string stalker = File.ReadAllText("Assets/Scripts/AI/Stalker/Networking/StalkerFusionRuntime.cs");
            StringAssert.Contains("HM_AI_STALKER_RESTORE", stalker);
            StringAssert.Contains("HM_AI_STALKER_FIRST_TICK", stalker);
            StringAssert.Contains("HM_AI_STALKER_STATE_CHANGED", stalker);
        }

        private static Component FindComponentByTypeName(GameObject root, string fullTypeName)
        {
            var components = root.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component != null && component.GetType().FullName == fullTypeName) return component;
            }

            return null;
        }

        private static string ExtractMethod(string source, string signature, string nextSignature)
        {
            int start = source.IndexOf(signature, System.StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Missing method: {signature}");
            int end = source.IndexOf(nextSignature, start, System.StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start), $"Missing method boundary: {nextSignature}");
            return source.Substring(start, end - start);
        }
    }
}
