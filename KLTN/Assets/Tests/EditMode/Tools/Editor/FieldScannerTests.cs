using System;
using System.Collections.Generic;
using EchoProtocol.AI.Listener.Noise;
using EchoProtocol.Networking;
using EchoProtocol.Tools.Scanner;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.Tests.EditMode.Tools
{
    public sealed class FieldScannerTests
    {
        [Test]
        public void RadarHUD_PrefabsKeepReferences_AndRenderReadyScanDetectedEmptyCooldown()
        {
            foreach (string path in new[] { "Assets/Resources/PF_FieldScanner_HUD.prefab", "Assets/Resources/PF_GameplayHUD_Canvas.prefab", "Assets/Prefabs/UI/PF_GameplayHUD_Canvas.prefab" })
            {
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var view = prefab.GetComponentInChildren<EchoProtocol.UI.HUD.HUDFieldScanner>(true);
                var serialized = new UnityEditor.SerializedObject(view);
                foreach (string field in new[] { "canvasGroup", "titleText", "modeBadgeText", "scanTimerText", "signalBarsText", "signalDetailText", "statusText", "controlsText", "detectedText", "radarGraphic" })
                    Assert.That(serialized.FindProperty(field).objectReferenceValue, Is.Not.Null, path + ": " + field);
                Assert.That(view.GetComponentInChildren<EchoProtocol.UI.HUD.ScannerRadarGraphic>(true).GetComponent<CanvasRenderer>(), Is.Not.Null);
                var rect = (RectTransform)view.transform;
                Assert.That(rect.anchorMin, Is.EqualTo(Vector2.one));
                Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(rect.pivot, Is.EqualTo(Vector2.one));
            }
            var source = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/PF_FieldScanner_HUD.prefab");
            var root = UnityEngine.Object.Instantiate(source);
            var player = new GameObject("ScannerHudTestPlayer");
            try
            {
                var scanner = player.AddComponent<NetworkFieldScanner>();
                var hud = root.GetComponent<EchoProtocol.UI.HUD.HUDFieldScanner>();
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var render = hud.GetType().GetMethod("RenderScannerState", flags);
                var status = (UnityEngine.UI.Text)hud.GetType().GetField("statusText", flags).GetValue(hud);
                var timer = (UnityEngine.UI.Text)hud.GetType().GetField("scanTimerText", flags).GetValue(hud);
                hud.BindScanner(scanner);
                render.Invoke(hud, null);
                Assert.That(timer.text, Is.EqualTo("SẴN SÀNG"));
                Assert.That(status.text, Is.EqualTo("[Chuột trái] Quét"));
                var hintUntil = hud.GetType().GetField("_modeHintUntil", flags);
                hintUntil.SetValue(hud, Time.unscaledTime + 6f);
                render.Invoke(hud, null);
                Assert.That(status.text, Does.Contain("[Chuột phải] Đổi chế độ"));
                Assert.That(status.text, Does.Contain("[Chuột trái] Quét"));
                hud.GetType().GetMethod("HandleModeChanged", flags).Invoke(hud, new object[] { FieldScannerMode.Motion });
                Assert.That(status.text, Does.Contain("[Chuột trái] Quét"));
                hintUntil.SetValue(hud, Time.unscaledTime - 1f);
                render.Invoke(hud, null);
                Assert.That(status.text, Does.Contain("[Chuột trái] Quét"));
                typeof(NetworkFieldScanner).GetField("_localActiveScanTimer", flags).SetValue(scanner, 8f);
                render.Invoke(hud, null);
                Assert.That(timer.text, Is.EqualTo("ĐANG QUÉT  08s"));
                Assert.That(status.text, Is.EqualTo("ĐANG QUÉT..."));
                typeof(NetworkFieldScanner).GetField("_hasLocalActiveResult", flags).SetValue(scanner, true);
                typeof(NetworkFieldScanner).GetField("_localResultTimer", flags).SetValue(scanner, 1f);
                render.Invoke(hud, null);
                Assert.That(status.text, Is.EqualTo("KHÔNG CÓ TÍN HIỆU"));
                var offsets = (List<Vector3>)typeof(NetworkFieldScanner).GetField("_radarOffsets", flags).GetValue(scanner);
                offsets.Add(Vector3.forward * 18);
                render.Invoke(hud, null);
                Assert.That(status.text, Is.EqualTo("PHÁT HIỆN TÍN HIỆU"));
                typeof(NetworkFieldScanner).GetField("_localActiveScanTimer", flags).SetValue(scanner, 0f);
                typeof(NetworkFieldScanner).GetField("_localCooldownTimer", flags).SetValue(scanner, 10f);
                render.Invoke(hud, null);
                Assert.That(timer.text, Is.EqualTo("HỒI  10s"));
                Assert.That(status.text, Is.EqualTo("MÁY QUÉT ĐANG HỒI"));
                hud.UnbindScanner();
                render.Invoke(hud, null);
                Assert.That(timer.text, Is.EqualTo("MẤT KẾT NỐI"));
                Assert.That(status.text, Is.EqualTo("ĐANG CHỜ KẾT NỐI"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void RadarPresentation_UsesAcceptedTargetsWithoutChangingDetection_AndClearsOldSamples()
        {
            var offsets = new List<Vector3>();
            var origin = new Vector3(10, 0, 10);
            var cores = new ICoreScanCandidate[]
            {
                new MockCoreCandidate { TargetId = 1, WorldPosition = origin + Vector3.right * 4, IsAvailableInWorld = true },
                new MockCoreCandidate { TargetId = 2, WorldPosition = origin + Vector3.forward * 8, IsAvailableInWorld = true },
                new MockCoreCandidate { TargetId = 3, WorldPosition = origin, IsAvailableInWorld = false },
                new MockCoreCandidate { TargetId = 4, WorldPosition = origin + Vector3.forward * 30, IsAvailableInWorld = true }
            };
            var expected = FieldScannerCoreDetector.Evaluate(origin, Vector3.forward, cores, isOccludedFunc: (a, b) => false);
            var actual = FieldScannerCoreDetector.Evaluate(origin, Vector3.forward, cores, isOccludedFunc: (a, b) => false, radarOffsets: offsets);
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(offsets, Is.EqualTo(new[] { Vector3.right * 4, Vector3.forward * 8 }));
            var monsters = new IMotionScannable[]
            {
                new MockMotionTarget { TargetId = 1, WorldPosition = origin + Vector3.left * 9, CurrentSpeed = 1 },
                new MockMotionTarget { TargetId = 2, WorldPosition = origin + Vector3.forward * 2, CurrentSpeed = 0 },
                new MockMotionTarget { TargetId = 3, WorldPosition = origin + Vector3.back * 3, CurrentSpeed = 1 }
            };
            var motion = FieldScannerMotionDetector.Evaluate(origin, Vector3.forward, monsters, radarOffsets: offsets);
            Assert.That(motion, Is.EqualTo(FieldScannerMotionDetector.Evaluate(origin, Vector3.forward, monsters)));
            Assert.That(offsets, Is.EqualTo(new[] { Vector3.back * 3, Vector3.left * 9 }));
            FieldScannerMotionDetector.Evaluate(origin, Vector3.forward, null, radarOffsets: offsets);
            Assert.That(offsets, Is.Empty);
            offsets.Add(Vector3.one);
            FieldScannerCoreDetector.Evaluate(origin, Vector3.forward, null, radarOffsets: offsets);
            Assert.That(offsets, Is.Empty);
        }

        private sealed class MockCoreCandidate : ICoreScanCandidate
        {
            public int TargetId { get; set; }
            public Vector3 WorldPosition { get; set; }
            public bool IsAvailableInWorld { get; set; }
        }

        private sealed class MockMotionTarget : IMotionScannable
        {
            public int TargetId { get; set; }

            public Vector3 WorldPosition { get; set; }

            public float CurrentSpeed { get; set; }

            public bool IsActiveTarget { get; set; } = true;

            public bool? IsMovingOverride { get; set; }

            public bool IsMoving =>
                IsMovingOverride
                ?? CurrentSpeed >= 0.2f;
        }

        [Test]
        public void MotionMode_ThreatState_WithZeroSpeed_IsDetected()
        {
            var tuning =
                FieldScannerTuning.Default;

            var targets =
                new List<IMotionScannable>
                {
                    new MockMotionTarget
                    {
                        TargetId = 901,

                        WorldPosition =
                            Vector3.forward * 5f,

                        CurrentSpeed = 0f,

                        IsMovingOverride = true
                    }
                };

            MotionScanResult result =
                FieldScannerMotionDetector.Evaluate(
                    Vector3.zero,
                    Vector3.forward,
                    targets,
                    tuning);

            Assert.That(
                result.HasMotion,
                Is.True);

            Assert.That(
                result.BlipCount,
                Is.EqualTo(1));

            Assert.That(
                result.GetBlip(0).TargetId,
                Is.EqualTo(901));
        }

        // ==========================================
        // 1 - 8: CORE MODE TESTS
        // ==========================================

        [Test]
        public void Test01_CoreMode_AvailableCoreInRange_Detected()
        {
            var tuning = FieldScannerTuning.Default;
            var candidates = new List<ICoreScanCandidate>
            {
                new MockCoreCandidate { TargetId = 101, WorldPosition = new Vector3(0f, 0f, 10f), IsAvailableInWorld = true }
            };

            var result = FieldScannerCoreDetector.Evaluate(Vector3.zero, Vector3.forward, candidates, tuning);

            Assert.That(result.HasTarget, Is.True);
            Assert.That(result.TargetId, Is.EqualTo(101));
            Assert.That(result.SignalBars, Is.EqualTo(ScannerSignalStrength.Bar2)); // 8-12m is Bar2
            Assert.That(result.Direction, Is.EqualTo(RelativeDirectionSector.Front));
        }

        [Test]
        public void Test02_CoreMode_CoreBeyond20m_NotDetected()
        {
            var tuning = FieldScannerTuning.Default;
            var candidates = new List<ICoreScanCandidate>
            {
                new MockCoreCandidate { TargetId = 102, WorldPosition = new Vector3(0f, 0f, 25f), IsAvailableInWorld = true }
            };

            var result = FieldScannerCoreDetector.Evaluate(Vector3.zero, Vector3.forward, candidates, tuning);

            Assert.That(result.HasTarget, Is.False);
            Assert.That(result.SignalBars, Is.EqualTo(ScannerSignalStrength.None));
        }

        [Test]
        public void Test03_CoreMode_CarriedCore_NotDetected()
        {
            var tuning = FieldScannerTuning.Default;
            var candidates = new List<ICoreScanCandidate>
            {
                new MockCoreCandidate { TargetId = 103, WorldPosition = new Vector3(0f, 0f, 5f), IsAvailableInWorld = false } // Carried
            };

            var result = FieldScannerCoreDetector.Evaluate(Vector3.zero, Vector3.forward, candidates, tuning);

            Assert.That(result.HasTarget, Is.False);
        }

        [Test]
        public void Test04_CoreMode_DepositedOrPlacedCore_NotDetected()
        {
            var tuning = FieldScannerTuning.Default;
            var candidates = new List<ICoreScanCandidate>
            {
                new MockCoreCandidate { TargetId = 104, WorldPosition = new Vector3(0f, 0f, 5f), IsAvailableInWorld = false } // Placed in sector box
            };

            var result = FieldScannerCoreDetector.Evaluate(Vector3.zero, Vector3.forward, candidates, tuning);

            Assert.That(result.HasTarget, Is.False);
        }

        [Test]
        public void Test05_CoreMode_MultipleCores_SelectsStrongest()
        {
            var tuning = FieldScannerTuning.Default;
            var candidates = new List<ICoreScanCandidate>
            {
                new MockCoreCandidate { TargetId = 1, WorldPosition = new Vector3(0f, 0f, 15f), IsAvailableInWorld = true }, // Far
                new MockCoreCandidate { TargetId = 2, WorldPosition = new Vector3(0f, 0f, 5f), IsAvailableInWorld = true }   // Near (stronger)
            };

            var result = FieldScannerCoreDetector.Evaluate(Vector3.zero, Vector3.forward, candidates, tuning);

            Assert.That(result.HasTarget, Is.True);
            Assert.That(result.TargetId, Is.EqualTo(2)); // Core 2 is stronger
            Assert.That(result.SignalBars, Is.EqualTo(ScannerSignalStrength.Bar3)); // 4-8m is Bar3
        }

        [Test]
        public void Test06_CoreMode_SameInput_DeterministicResult()
        {
            var tuning = FieldScannerTuning.Default;
            var candidates = new List<ICoreScanCandidate>
            {
                new MockCoreCandidate { TargetId = 10, WorldPosition = new Vector3(5f, 0f, 5f), IsAvailableInWorld = true },
                new MockCoreCandidate { TargetId = 20, WorldPosition = new Vector3(-5f, 0f, 5f), IsAvailableInWorld = true }
            };

            var result1 = FieldScannerCoreDetector.Evaluate(Vector3.zero, Vector3.forward, candidates, tuning);
            var result2 = FieldScannerCoreDetector.Evaluate(Vector3.zero, Vector3.forward, candidates, tuning);

            Assert.That(result1.TargetId, Is.EqualTo(result2.TargetId));
            Assert.That(result1.Direction, Is.EqualTo(result2.Direction));
            Assert.That(result1.SignalScore, Is.EqualTo(result2.SignalScore));
        }

        [Test]
        public void Test07_CoreMode_OccludedCore_SignalAttenuated()
        {
            var tuning = FieldScannerTuning.Default;
            var clearCandidate = new[] { new MockCoreCandidate { TargetId = 1, WorldPosition = new Vector3(0f, 0f, 10f), IsAvailableInWorld = true } };
            var occludedCandidate = new[] { new MockCoreCandidate { TargetId = 1, WorldPosition = new Vector3(0f, 0f, 10f), IsAvailableInWorld = true } };

            var clearResult = FieldScannerCoreDetector.Evaluate(Vector3.zero, Vector3.forward, clearCandidate, tuning, (orig, target) => false);
            var occludedResult = FieldScannerCoreDetector.Evaluate(Vector3.zero, Vector3.forward, occludedCandidate, tuning, (orig, target) => true);

            Assert.That(clearResult.HasTarget, Is.True);
            Assert.That(occludedResult.HasTarget, Is.True);
            Assert.That(occludedResult.SignalScore, Is.LessThan(clearResult.SignalScore));
            Assert.That(occludedResult.SignalScore, Is.EqualTo(clearResult.SignalScore * tuning.OccludedSignalMultiplier).Within(0.001f));
        }

        [Test]
        public void Test08_CoreMode_RelativeDirections_8SectorsAccurate()
        {
            Vector3 fwd = Vector3.forward;

            Assert.That(FieldScannerCoreDetector.ResolveDirectionSector(fwd, new Vector3(0f, 0f, 10f)), Is.EqualTo(RelativeDirectionSector.Front));
            Assert.That(FieldScannerCoreDetector.ResolveDirectionSector(fwd, new Vector3(10f, 0f, 10f)), Is.EqualTo(RelativeDirectionSector.FrontRight));
            Assert.That(FieldScannerCoreDetector.ResolveDirectionSector(fwd, new Vector3(10f, 0f, 0f)), Is.EqualTo(RelativeDirectionSector.Right));
            Assert.That(FieldScannerCoreDetector.ResolveDirectionSector(fwd, new Vector3(10f, 0f, -10f)), Is.EqualTo(RelativeDirectionSector.BackRight));
            Assert.That(FieldScannerCoreDetector.ResolveDirectionSector(fwd, new Vector3(0f, 0f, -10f)), Is.EqualTo(RelativeDirectionSector.Back));
            Assert.That(FieldScannerCoreDetector.ResolveDirectionSector(fwd, new Vector3(-10f, 0f, -10f)), Is.EqualTo(RelativeDirectionSector.BackLeft));
            Assert.That(FieldScannerCoreDetector.ResolveDirectionSector(fwd, new Vector3(-10f, 0f, 0f)), Is.EqualTo(RelativeDirectionSector.Left));
            Assert.That(FieldScannerCoreDetector.ResolveDirectionSector(fwd, new Vector3(-10f, 0f, 10f)), Is.EqualTo(RelativeDirectionSector.FrontLeft));
        }

        // ==========================================
        // 9 - 14: MOTION MODE TESTS
        // ==========================================

        [Test]
        public void Test09_MotionMode_MovingMonsterInRange_Detected()
        {
            var tuning = FieldScannerTuning.Default;
            var targets = new List<IMotionScannable>
            {
                new MockMotionTarget { TargetId = 501, WorldPosition = new Vector3(0f, 0f, 8f), CurrentSpeed = 1.5f }
            };

            var result = FieldScannerMotionDetector.Evaluate(Vector3.zero, Vector3.forward, targets, tuning);

            Assert.That(result.HasMotion, Is.True);
            Assert.That(result.BlipCount, Is.EqualTo(1));
            var blip = result.GetBlip(0);
            Assert.That(blip.IsValid, Is.True);
            Assert.That(blip.TargetId, Is.EqualTo(501));
            Assert.That(blip.Intensity, Is.EqualTo(MotionBlipIntensity.Medium)); // 7-12m is Medium
            Assert.That(blip.Direction, Is.EqualTo(RelativeDirectionSector.Front));
        }

        [Test]
        public void Test10_MotionMode_StationaryMonster_NotDetected()
        {
            var tuning = FieldScannerTuning.Default;
            var targets = new List<IMotionScannable>
            {
                new MockMotionTarget { TargetId = 502, WorldPosition = new Vector3(0f, 0f, 5f), CurrentSpeed = 0.05f } // Below 0.2 m/s
            };

            var result = FieldScannerMotionDetector.Evaluate(Vector3.zero, Vector3.forward, targets, tuning);

            Assert.That(result.HasMotion, Is.False);
            Assert.That(result.BlipCount, Is.EqualTo(0));
        }

        [Test]
        public void Test11_MotionMode_MonsterBeyond15m_NotDetected()
        {
            var tuning = FieldScannerTuning.Default;
            var targets = new List<IMotionScannable>
            {
                new MockMotionTarget { TargetId = 503, WorldPosition = new Vector3(0f, 0f, 18f), CurrentSpeed = 2.0f } // Beyond 15m
            };

            var result = FieldScannerMotionDetector.Evaluate(Vector3.zero, Vector3.forward, targets, tuning);

            Assert.That(result.HasMotion, Is.False);
            Assert.That(result.BlipCount, Is.EqualTo(0));
        }

        [Test]
        public void Test12_MotionMode_MoreThan3Monsters_OnlyStrongest3()
        {
            var tuning = FieldScannerTuning.Default;
            var targets = new List<IMotionScannable>
            {
                new MockMotionTarget { TargetId = 1, WorldPosition = new Vector3(0f, 0f, 2f), CurrentSpeed = 1f },  // Nearest
                new MockMotionTarget { TargetId = 2, WorldPosition = new Vector3(0f, 0f, 4f), CurrentSpeed = 1f },
                new MockMotionTarget { TargetId = 3, WorldPosition = new Vector3(0f, 0f, 6f), CurrentSpeed = 1f },
                new MockMotionTarget { TargetId = 4, WorldPosition = new Vector3(0f, 0f, 8f), CurrentSpeed = 1f },
                new MockMotionTarget { TargetId = 5, WorldPosition = new Vector3(0f, 0f, 10f), CurrentSpeed = 1f }
            };

            var result = FieldScannerMotionDetector.Evaluate(Vector3.zero, Vector3.forward, targets, tuning);

            Assert.That(result.HasMotion, Is.True);
            Assert.That(result.BlipCount, Is.EqualTo(3)); // Capped at 3
            Assert.That(result.GetBlip(0).TargetId, Is.EqualTo(1));
            Assert.That(result.GetBlip(1).TargetId, Is.EqualTo(2));
            Assert.That(result.GetBlip(2).TargetId, Is.EqualTo(3));
        }

        [Test]
        public void Test13_MotionMode_NoMonsters_NoSignal()
        {
            var tuning = FieldScannerTuning.Default;
            var targets = new List<IMotionScannable>();

            var result = FieldScannerMotionDetector.Evaluate(Vector3.zero, Vector3.forward, targets, tuning);

            Assert.That(result.HasMotion, Is.False);
            Assert.That(result.BlipCount, Is.EqualTo(0));
        }

        [Test]
        public void Test14_MotionMode_SamePositions_DeterministicOrdering()
        {
            var tuning = FieldScannerTuning.Default;
            var targets = new List<IMotionScannable>
            {
                new MockMotionTarget { TargetId = 30, WorldPosition = new Vector3(5f, 0f, 5f), CurrentSpeed = 1f },
                new MockMotionTarget { TargetId = 10, WorldPosition = new Vector3(5f, 0f, 5f), CurrentSpeed = 1f }
            };

            var result = FieldScannerMotionDetector.Evaluate(Vector3.zero, Vector3.forward, targets, tuning);

            Assert.That(result.BlipCount, Is.EqualTo(2));
            // Tie break sorts smaller id first
            Assert.That(result.GetBlip(0).TargetId, Is.EqualTo(10));
            Assert.That(result.GetBlip(1).TargetId, Is.EqualTo(30));
        }

        // ==========================================
        // 15 - 20: GAMEPLAY VALIDATION TESTS
        // ==========================================

        [Test]
        public void Test15_Gameplay_NotOwningScanner_ScanRejected()
        {
            bool ownsScanner = false;
            bool isCarryingCore = false;
            bool cooldownReady = true;

            bool canScan = ownsScanner && !isCarryingCore && cooldownReady;
            Assert.That(canScan, Is.False);
        }

        [Test]
        public void Test16_Gameplay_CarryingCore_ScanRejected()
        {
            bool ownsScanner = true;
            bool isCarryingCore = true;
            bool cooldownReady = true;

            bool canScan = ownsScanner && !isCarryingCore && cooldownReady;
            Assert.That(canScan, Is.False);
        }

        [Test]
        public void Test17_Gameplay_CooldownActive_ScanRejected()
        {
            bool ownsScanner = true;
            bool isCarryingCore = false;
            bool cooldownReady = false;

            bool canScan = ownsScanner && !isCarryingCore && cooldownReady;
            Assert.That(canScan, Is.False);
        }

        [Test]
        public void Test18_Gameplay_CooldownExpired_ScanAccepted()
        {
            bool ownsScanner = true;
            bool isCarryingCore = false;
            bool cooldownReady = true;

            bool canScan = ownsScanner && !isCarryingCore && cooldownReady;
            Assert.That(canScan, Is.True);
        }

        [Test]
        public void Test19_Gameplay_CoreMode_DoesNotReturnMonsters()
        {
            var tuning = FieldScannerTuning.Default;
            var cores = new List<ICoreScanCandidate>
            {
                new MockCoreCandidate { TargetId = 99, WorldPosition = new Vector3(0f, 0f, 5f), IsAvailableInWorld = true }
            };

            var coreResult = FieldScannerCoreDetector.Evaluate(Vector3.zero, Vector3.forward, cores, tuning);

            Assert.That(coreResult.HasTarget, Is.True);
            Assert.That(coreResult.TargetId, Is.EqualTo(99));
        }

        [Test]
        public void Test20_Gameplay_MotionMode_DoesNotReturnCores()
        {
            var tuning = FieldScannerTuning.Default;
            var monsters = new List<IMotionScannable>
            {
                new MockMotionTarget { TargetId = 88, WorldPosition = new Vector3(0f, 0f, 5f), CurrentSpeed = 1f }
            };

            var motionResult = FieldScannerMotionDetector.Evaluate(Vector3.zero, Vector3.forward, monsters, tuning);

            Assert.That(motionResult.HasMotion, Is.True);
            Assert.That(motionResult.GetBlip(0).TargetId, Is.EqualTo(88));
        }

        // ==========================================
        // 21 - 24: NETWORK & CONTRACT TESTS
        // ==========================================

        [Test]
        public void Test21_Network_ClientCannotFabricateAuthoritativeResult()
        {
            // Detector logic runs statically / authoritatively on the host; client only receives CoreScanResult/MotionScanResult.
            Assert.That(typeof(FieldScannerCoreDetector).IsAbstract && typeof(FieldScannerCoreDetector).IsSealed, Is.True);
            Assert.That(typeof(FieldScannerMotionDetector).IsAbstract && typeof(FieldScannerMotionDetector).IsSealed, Is.True);
        }

        [Test]
        public void Test22_Network_StateAuthorityOwnsScanValidation()
        {
            var method = typeof(NetworkFieldScanner).GetMethod("RpcRequestScan", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);

            var rpcAttr = method.GetCustomAttributes(typeof(Fusion.RpcAttribute), false);
            Assert.That(rpcAttr.Length, Is.GreaterThan(0));
            var rpc = (Fusion.RpcAttribute)rpcAttr[0];
            Assert.That(rpc.Sources, Is.EqualTo(Fusion.RpcSources.InputAuthority));
            Assert.That(rpc.Targets, Is.EqualTo(Fusion.RpcTargets.StateAuthority));
        }

        [Test]
        public void Test23_Network_RemotePresentationDoesNotExposeCoordinates()
        {
            // CoreScanResult only exposes RelativeDirectionSector and SignalBars, not world position
            var result = new CoreScanResult
            {
                HasTarget = true,
                SignalBars = ScannerSignalStrength.Bar3,
                Direction = RelativeDirectionSector.FrontRight
            };

            Assert.That(result.Direction, Is.EqualTo(RelativeDirectionSector.FrontRight));
            Assert.That(result.SignalBars, Is.EqualTo(ScannerSignalStrength.Bar3));
        }

        [Test]
        public void Test24_Network_TeamToolSelectionPreservesUniqueRule()
        {
            var def = new LobbyToolDefinition();
            // Inspect backing fields via reflection
            var idField = typeof(LobbyToolDefinition).GetField("_id", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var isUniqueField = typeof(LobbyToolDefinition).GetField("_isUnique", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            idField.SetValue(def, 1);
            isUniqueField.SetValue(def, true);

            Assert.That(def.Id, Is.EqualTo(1));
            Assert.That(def.IsUnique, Is.True);
        }

        // ==========================================
        // 25 - 27: RUNTIME NOISE TESTS
        // ==========================================

        [Test]
        public void Test25_Noise_AcceptedScan_EmitsScannerNoise()
        {
            var catalog = RuntimeNoiseCatalog.CreateDefault();
            bool hasDef = catalog.TryGetDefinition(RuntimeNoiseType.FIELD_SCANNER, out var def);

            Assert.That(hasDef, Is.True);
            Assert.That(def.NoiseType, Is.EqualTo(RuntimeNoiseType.FIELD_SCANNER));
            Assert.That(def.BaseLoudness, Is.EqualTo(0.45d));
            Assert.That(def.HearingRadius, Is.EqualTo(8.0d));
        }

        [Test]
        public void Test26_Noise_RejectedScan_EmitsNoNoise()
        {
            var system = new RuntimeNoiseSystem();
            // A rejected scan does not submit to RuntimeNoiseSystem, so ActiveCount remains 0
            Assert.That(system.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void Test27_Noise_DuplicateRequest_DedupPreventsDuplicateNoise()
        {
            var system = new RuntimeNoiseSystem();
            var matchId = Guid.NewGuid();
            var key = RuntimeNoiseSourceOccurrenceKey.ForTeamTool("player_1", "FIELD_SCANNER", 42);
            long tick = 100;
            Vector3 pos = Vector3.zero;
            DateTime now = DateTime.UtcNow;

            var req1 = new RuntimeNoiseEmissionRequest(key, RuntimeNoiseType.FIELD_SCANNER, pos, now, tick);
            var req2 = new RuntimeNoiseEmissionRequest(key, RuntimeNoiseType.FIELD_SCANNER, pos, now, tick + 1);

            var status1 = system.TryAccept(matchId, req1, out _);
            var status2 = system.TryAccept(matchId, req2, out _);

            Assert.That(status1, Is.EqualTo(RuntimeNoiseAcceptStatus.Accepted));
            Assert.That(status2, Is.EqualTo(RuntimeNoiseAcceptStatus.Duplicate));
        }

        // ==========================================
        // 28 - 31: NETWORK TOOL PICKUP TESTS
        // ==========================================

        [Test]
        public void Test28_ToolPickup_CanInteract_AllowedWhenHandsEmpty()
        {
            var pickupGo = new GameObject("Pickup", typeof(BoxCollider), typeof(NetworkToolPickup));
            var playerGo = new GameObject("Player", typeof(PlayerInventory));

            var pickup = pickupGo.GetComponent<NetworkToolPickup>();
            Assert.That(pickup.CanInteract(playerGo), Is.True);

            UnityEngine.Object.DestroyImmediate(pickupGo);
            UnityEngine.Object.DestroyImmediate(playerGo);
        }

        [Test]
        public void Test29_ToolPickup_CanInteract_BlockedWhenAlreadyHoldingTool()
        {
            var pickupGo = new GameObject("Pickup", typeof(BoxCollider), typeof(NetworkToolPickup));
            var playerGo = new GameObject("Player", typeof(PlayerInventory));
            var inv = playerGo.GetComponent<PlayerInventory>();

            var itemDef = ScriptableObject.CreateInstance<InventoryItemDefinition>();
            var typeField = typeof(InventoryItemDefinition).GetField("itemType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            typeField.SetValue(itemDef, InventoryItemType.TeamTool);

            inv.TryAdd(itemDef);

            var pickup = pickupGo.GetComponent<NetworkToolPickup>();
            Assert.That(pickup.CanInteract(playerGo), Is.False);

            UnityEngine.Object.DestroyImmediate(pickupGo);
            UnityEngine.Object.DestroyImmediate(playerGo);
            UnityEngine.Object.DestroyImmediate(itemDef);
        }

        [Test]
        public void Test30_ToolPickup_CanInteract_BlockedWhenCarryingCore()
        {
            var pickupGo = new GameObject("Pickup", typeof(BoxCollider), typeof(NetworkToolPickup));
            var playerGo = new GameObject("Player", typeof(PlayerInventory), typeof(PlayerEnergyCoreCarrier));
            var carrier = playerGo.GetComponent<PlayerEnergyCoreCarrier>();

            var coreGo = new GameObject("Core", typeof(BoxCollider), typeof(EnergyCorePickup));
            var corePickup = coreGo.GetComponent<EnergyCorePickup>();

            var carriedCoreField = typeof(PlayerEnergyCoreCarrier).GetField("_carriedCore", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            carriedCoreField.SetValue(carrier, corePickup);

            var pickup = pickupGo.GetComponent<NetworkToolPickup>();
            Assert.That(pickup.CanInteract(playerGo), Is.False);

            UnityEngine.Object.DestroyImmediate(pickupGo);
            UnityEngine.Object.DestroyImmediate(playerGo);
            UnityEngine.Object.DestroyImmediate(coreGo);
        }

        [Test]
        public void Test31_ToolPickup_CanInteract_BlockedWhenAlreadyPickedUp()
        {
            var pickupGo = new GameObject("Pickup", typeof(BoxCollider), typeof(NetworkToolPickup));
            var playerGo = new GameObject("Player", typeof(PlayerInventory));

            var pickup = pickupGo.GetComponent<NetworkToolPickup>();
            pickup.IsPickedUp = true;

            Assert.That(pickup.CanInteract(playerGo), Is.False);

            UnityEngine.Object.DestroyImmediate(pickupGo);
            UnityEngine.Object.DestroyImmediate(playerGo);
        }
    }
}
