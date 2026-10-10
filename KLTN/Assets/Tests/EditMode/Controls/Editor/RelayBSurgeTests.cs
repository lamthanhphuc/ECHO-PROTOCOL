using System;
using System.Threading.Tasks;
using EchoProtocol.RelayB;
using NUnit.Framework;
using UnityEngine;

namespace EchoProtocol.Tests.EditMode.Controls
{
    public sealed class RelayBSurgeTests
    {
        [TestCase(0,0)] [TestCase(0,1)] [TestCase(1,0)] [TestCase(1,1)] [TestCase(2,0)] [TestCase(2,1)]
        public void GeneratedBoards_HaveTimedSolutionsAndMatchDifficulty(int difficulty,int variant)
        {
            for(int seed=1;seed<=20;seed++) {
                var board=RelayBSurgeGenerator.Generate(seed,difficulty,variant,0);
                Assert.That(board.Width,Is.EqualTo(difficulty==0 ? 7 : difficulty==1 ? 9 : 10));
                Assert.That(board.Budget,Is.EqualTo(difficulty==0 ? 3 : difficulty==1 ? 4 : 5));
                Assert.That(board.Witness.Length,Is.InRange(difficulty==0 ? 2 : difficulty==1 ? 4 : 5,board.Budget));
                if (difficulty > 0)
                    Assert.That(RelayBSurgeGenerator.HasDistributedCut(board, board.Witness), Is.True,
                        "Normal/hard solutions must require choices across multiple rows and columns");
                Assert.That(RelayBSurgeGenerator.VerifyTimedWitness(board,board.Witness,out _),Is.True,$"Seed {seed}");
                var same=RelayBSurgeGenerator.Generate(seed,difficulty,variant,0);
                Assert.That(same.Walls.Low,Is.EqualTo(board.Walls.Low));Assert.That(same.Walls.High,Is.EqualTo(board.Walls.High));
            }
        }
        [Test]
        public async Task WorkerGeneration_PreservesNetworkKeysAndRetrySolutions()
        {
            for(int difficulty=0;difficulty<3;difficulty++) {
                var jobs=new Task<RelayBSurgeBoard>[8];
                for(int attempt=0;attempt<jobs.Length;attempt++) {
                    int retry=attempt,d=difficulty;
                    jobs[attempt]=Task.Run(()=>RelayBSurgeGenerator.Generate(87123,d,1,retry));
                }
                var boards=await Task.WhenAll(jobs);
                for(int attempt=0;attempt<boards.Length;attempt++) {
                    var expected=RelayBSurgeGenerator.Generate(87123,difficulty,1,attempt);
                    Assert.That(boards[attempt].Seed,Is.EqualTo(87123));
                    Assert.That(boards[attempt].Walls.Low,Is.EqualTo(expected.Walls.Low));
                    Assert.That(boards[attempt].Walls.High,Is.EqualTo(expected.Walls.High));
                    Assert.That(boards[attempt].Sources.Low,Is.EqualTo(expected.Sources.Low));
                    Assert.That(RelayBSurgeGenerator.VerifyTimedWitness(boards[attempt],boards[attempt].Witness,out _),Is.True);
                }
                Assert.That(boards[0].Walls.Low==boards[1].Walls.Low && boards[0].Walls.High==boards[1].Walls.High,Is.False);
            }
        }
        [Test]
        public void Placement_RejectsInvalidCellsDuplicateAndCooldown()
        {
            var sim=new RelayBSurgeSimulation();sim.Initialize(7,1,0);sim.Start();
            Assert.That(sim.Place(-1),Is.False);Assert.That(sim.Place(sim.Board.Count),Is.False);
            for(int cell=0;cell<sim.Board.Count;cell++)if(sim.Infected.Has(cell)||sim.Board.Cores.Has(cell)||sim.Board.Walls.Has(cell))
                Assert.That(sim.Place(cell),Is.False);
            Assert.That(sim.Place(sim.Board.Witness[0]),Is.True);
            Assert.That(sim.Place(sim.Board.Witness[0]),Is.False);
            Assert.That(sim.Place(sim.Board.Witness[1]),Is.False);
            sim.Tick(1.21f);Assert.That(sim.Place(sim.Board.Witness[1]),Is.True);
        }
        [Test]
        public void Pulse_SpreadsExactlyOneFrontierIncludingCellsAbove64()
        {
            var sim=new RelayBSurgeSimulation();sim.Initialize(12,2,1);sim.Start();
            var before=sim.Infected;var next=sim.Frontier;sim.Tick(sim.Board.PulseSeconds);
            Assert.That(sim.Infected.Low,Is.EqualTo(before.Low|next.Low));
            Assert.That(sim.Infected.High,Is.EqualTo(before.High|next.High));
            Assert.That(sim.PulseSequence,Is.EqualTo(1));
        }
        [Test]
        public void NoInputEventuallyFails_AndRetryChangesOnlyThisBoard()
        {
            var sim=new RelayBSurgeSimulation();sim.Initialize(4,1,0);sim.Start();
            sim.Tick(70);Assert.That(sim.IsFailed,Is.True);
            var walls=sim.Board.Walls;sim.Initialize(4,1,0,1);
            Assert.That(sim.Phase,Is.EqualTo(RelayBSurgePhase.Ready));Assert.That(sim.Elapsed,Is.Zero);
            Assert.That(sim.Board.Walls.Low!=walls.Low||sim.Board.Walls.High!=walls.High,Is.True);
        }
        [Test]
        public void ReadyDoesNotRun_ContainedDoesNotRequireExtraHold()
        {
            var sim=new RelayBSurgeSimulation();sim.Initialize(9,1,0);sim.Tick(100);Assert.That(sim.Elapsed,Is.Zero);
            sim.Start();foreach(int cell in sim.Board.Witness) { Assert.That(sim.Place(cell),Is.True);sim.Tick(1.3f); }
            sim.Tick(sim.Board.TimeLimit);Assert.That(sim.Phase,Is.EqualTo(RelayBSurgePhase.Contained));
            Assert.That(sim.Frontier.Count,Is.Zero);float end=sim.Elapsed;sim.Tick(5);Assert.That(sim.Elapsed,Is.EqualTo(end));
        }
        [Test]
        public void LegacyChannelSelectionCannotBypassContainment()
        {
            var config=ScriptableObject.CreateInstance<RelayBConfig>();
            try {
                var sim=new RelayBSignalSimulation();sim.Initialize(config,0,true,42);sim.Surge.Initialize(42,1,0);
                sim.ScanSpectrum();sim.SelectChannel(sim.GetCurrentPreset().CorrectChannelIndex);
                Assert.That(sim.IsSignalFound,Is.False);sim.CompleteSurge();Assert.That(sim.IsSignalFound,Is.False);
                sim.Surge.Start();foreach(int cell in sim.Surge.Board.Witness){Assert.That(sim.Surge.Place(cell),Is.True);sim.Surge.Tick(1.3f);}
                sim.Surge.Tick(60);sim.CompleteSurge();Assert.That(sim.IsSignalFound,Is.True);Assert.That(sim.ActiveTab,Is.EqualTo(1));
                Assert.That(sim.IsSignalClean,Is.False,"Decode still required");sim.StartSynchronization();Assert.That(sim.IsSynchronizing,Is.False);
            } finally { UnityEngine.Object.DestroyImmediate(config); }
        }
    }
}
