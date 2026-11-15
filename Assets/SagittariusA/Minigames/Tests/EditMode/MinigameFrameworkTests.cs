using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace SagittariusA.Minigames.Tests
{
    // Test doubles: a scripted minigame and a recording environment.
    [Minigame("test-alpha")]
    public sealed class FakeAlphaMinigame : IMinigame
    {
        public static MinigameDefinition Definition => new MinigameDefinition { Title = "Alpha", Instruction = "Test", DefaultTimeLimitSeconds = 30 };
        public string Id => "test-alpha";
        public bool IsRunning { get; private set; }
        public MinigameContext Context; public bool ThrowOnInitialize; public int CancelCalls;
        public event Action<MinigameResult> Completed;
        public void Initialize(MinigameContext context) { if (ThrowOnInitialize) throw new InvalidOperationException("boom"); Context = context; }
        public void Begin() { IsRunning = true; }
        public void Tick(float dt) { }
        public void Cancel() { CancelCalls++; IsRunning = false; }
        public void Report(MinigameOutcome outcome, int score) { IsRunning = false; Completed?.Invoke(new MinigameResult { Outcome = outcome, Score = score }); }
    }
    [Minigame("test-beta")]
    public sealed class FakeBetaMinigame : IMinigame
    {
        public string Id => "test-beta"; public bool IsRunning => false;
        public event Action<MinigameResult> Completed { add { } remove { } }
        public void Initialize(MinigameContext c) { } public void Begin() { } public void Tick(float dt) { } public void Cancel() { }
    }
    public sealed class RecordingEnvironment : IMinigameEnvironment
    {
        public bool Paused, InputLocked, ThrowAfterCapture; public int Captures, Restores;
        public void Capture(MinigameDefinition d) { Captures++; Paused = d.PauseWorld; InputLocked = true; if (ThrowAfterCapture) throw new InvalidOperationException("Partial capture failed"); }
        public void Restore() { Restores++; Paused = false; InputLocked = false; }
    }

    public class MinigameFrameworkTests
    {
        private static MinigameRegistry Registry() => MinigameRegistry.FromAssemblies(typeof(FakeAlphaMinigame).Assembly);
        private static MinigameContext Ctx(string id = "test-alpha") => new MinigameContext { MinigameId = id, MissionId = "earth", EventId = "evt", RandomSeed = 7 };

        [Test] public void RegisteredIdResolvesToItsType()   // 1
        {
            var reg = Registry();
            Assert.IsTrue(reg.TryResolve("test-alpha", out var a)); Assert.AreEqual(typeof(FakeAlphaMinigame), a.Type);
            Assert.AreEqual("Alpha", a.Definition.Title); Assert.AreEqual("test-alpha", a.Definition.Id);
            Assert.IsTrue(reg.TryResolve("TEST-BETA", out var b)); Assert.AreEqual(typeof(FakeBetaMinigame), b.Type);
        }

        [Test] public void UnknownIdFailsSafelyAndRestoresNothing()   // 2
        {
            var env = new RecordingEnvironment(); var session = new MinigameSession(env);
            MinigameResult got = null; int calls = 0;
            Assert.IsFalse(session.Start(Ctx("missing"), Registry(), e => new FakeAlphaMinigame(), r => { got = r; calls++; }));
            Assert.AreEqual(1, calls); Assert.AreEqual(MinigameOutcome.Cancelled, got.Outcome); Assert.AreEqual(0, env.Captures);
            Assert.IsFalse(session.IsActive);
        }

        [Test] public void OnlyOneActiveMinigame()   // 3
        {
            var env = new RecordingEnvironment(); var session = new MinigameSession(env);
            var game = new FakeAlphaMinigame(); MinigameResult first = null, second = null;
            Assert.IsTrue(session.Start(Ctx(), Registry(), e => game, r => first = r));
            Assert.IsFalse(session.Start(Ctx(), Registry(), e => new FakeAlphaMinigame(), r => second = r));
            Assert.AreEqual(MinigameOutcome.Cancelled, second.Outcome); Assert.IsNull(first); Assert.IsTrue(session.IsActive);
            Assert.AreEqual(1, env.Captures);
        }

        [Test] public void CompletionCallbackFiresExactlyOnce()   // 4
        {
            var env = new RecordingEnvironment(); var session = new MinigameSession(env);
            var game = new FakeAlphaMinigame(); int calls = 0; MinigameResult got = null;
            session.Start(Ctx(), Registry(), e => game, r => { calls++; got = r; });
            game.Report(MinigameOutcome.Success, 90); game.Report(MinigameOutcome.Failure, 0);
            session.Cancel(); session.ConfirmResult();
            Assert.AreEqual(1, calls); Assert.AreEqual(MinigameOutcome.Success, got.Outcome);
            Assert.AreEqual("evt", got.EventId); Assert.AreEqual("earth", got.MissionId);
        }

        [Test] public void ResultPanelHoldsResultUntilConfirmed()
        {
            var env = new RecordingEnvironment(); var session = new MinigameSession(env);
            MinigameResult shown = null; session.GameReportedResult += r => shown = r;
            var game = new FakeAlphaMinigame(); int calls = 0;
            session.Start(Ctx(), Registry(), e => game, r => calls++);
            game.Report(MinigameOutcome.PartialSuccess, 50);
            Assert.IsNotNull(shown); Assert.AreEqual(0, calls); Assert.IsTrue(env.InputLocked);
            session.ConfirmResult();
            Assert.AreEqual(1, calls); Assert.IsFalse(env.InputLocked);
        }

        [Test] public void ScoresAreClampedTo0To100()   // 5
        {
            Assert.AreEqual(100, new MinigameResult { Score = 140 }.Normalize().Score);
            Assert.AreEqual(0, new MinigameResult { Score = -12 }.Normalize().Score);
            Assert.AreEqual(1f, new MinigameResult { CompletionPercent = 3 }.Normalize().CompletionPercent);
            Assert.AreEqual(0, MinigameScoring.Standard(30, 60, 60));
            Assert.AreEqual(100, MinigameScoring.Standard(0, 0, 60));
            var env = new RecordingEnvironment(); var session = new MinigameSession(env); var game = new FakeAlphaMinigame(); MinigameResult got = null;
            session.Start(Ctx(), Registry(), e => game, r => got = r); game.Report(MinigameOutcome.Success, 250);
            Assert.AreEqual(100, got.Score);
        }

        [Test] public void SameSeedAndDifficultyGiveSameRandomStream()   // 6 (framework part; each minigame has its own test too)
        {
            var a = MinigameRandom.Create(new MinigameContext { RandomSeed = 99, DifficultyLevel = 2 }, "x");
            var b = MinigameRandom.Create(new MinigameContext { RandomSeed = 99, DifficultyLevel = 2 }, "x");
            var c = MinigameRandom.Create(new MinigameContext { RandomSeed = 99, DifficultyLevel = 3 }, "x");
            var sa = new List<int>(); var sb = new List<int>(); var sc = new List<int>();
            for (int i = 0; i < 5; i++) { sa.Add(a.Next()); sb.Add(b.Next()); sc.Add(c.Next()); }
            CollectionAssert.AreEqual(sa, sb); CollectionAssert.AreNotEqual(sa, sc);
        }

        [Test] public void StateRestoredAfterSuccessFailureAndCancel()   // 7
        {
            foreach (var path in new[] { "success", "failure", "cancel", "init-error" })
            {
                var env = new RecordingEnvironment(); var session = new MinigameSession(env);
                var game = new FakeAlphaMinigame { ThrowOnInitialize = path == "init-error" }; MinigameResult got = null;
                session.Start(Ctx(), Registry(), e => game, r => got = r);
                if (path == "success") game.Report(MinigameOutcome.Success, 80);
                if (path == "failure") game.Report(MinigameOutcome.Failure, 10);
                if (path == "cancel") session.Cancel();
                Assert.IsNotNull(got, path); Assert.IsFalse(session.IsActive, path);
                Assert.AreEqual(1, env.Restores, path); Assert.IsFalse(env.Paused, path); Assert.IsFalse(env.InputLocked, path);
            }
        }

        [Test] public void CancelCallsGameCancelAndReturnsCancelled()
        {
            var env = new RecordingEnvironment(); var session = new MinigameSession(env); var game = new FakeAlphaMinigame(); MinigameResult got = null;
            session.Start(Ctx(), Registry(), e => game, r => got = r); session.Cancel("Scene changed.");
            Assert.AreEqual(1, game.CancelCalls); Assert.AreEqual(MinigameOutcome.Cancelled, got.Outcome); Assert.AreEqual("Scene changed.", got.Summary);
        }

        [Test] public void TimeLimitResolution()
        {
            var def = new MinigameDefinition { DefaultTimeLimitSeconds = 60 };
            Assert.AreEqual(60f, MinigameSettings.ResolveTimeLimit(new MinigameContext(), def));
            Assert.AreEqual(45f, MinigameSettings.ResolveTimeLimit(new MinigameContext { TimeLimitSeconds = 45 }, def));
            Assert.AreEqual(0f, MinigameSettings.ResolveTimeLimit(new MinigameContext { TimeLimitSeconds = 0 }, def));
            MinigameSettings.DisableTimeLimits = true;
            try { Assert.AreEqual(0f, MinigameSettings.ResolveTimeLimit(new MinigameContext { TimeLimitSeconds = 45 }, def)); }
            finally { MinigameSettings.DisableTimeLimits = false; }
        }

        [Test] public void PartialCaptureFailureRestoresEnvironmentOnce()
        {
            var env = new RecordingEnvironment { ThrowAfterCapture = true };
            var session = new MinigameSession(env);
            int calls = 0;
            Assert.IsFalse(session.Start(Ctx(), Registry(), e => new FakeAlphaMinigame(), r => { calls++; Assert.AreEqual(MinigameOutcome.Cancelled, r.Outcome); }));
            Assert.AreEqual(1, calls); Assert.AreEqual(1, env.Restores);
            Assert.IsFalse(env.Paused); Assert.IsFalse(env.InputLocked); Assert.IsFalse(session.IsActive);
        }
    }
}
