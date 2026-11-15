using System.Collections;
using NUnit.Framework;
using SagittariusA.Minigames.EarthImageCalibration;
using UnityEngine;
using UnityEngine.TestTools;

namespace SagittariusA.Minigames.Tests
{
    // A behaviour that stands in for 3D player input (like MoonFirstPersonController).
    public sealed class FakePlayerInputBehaviour : MonoBehaviour { }

    // Ends itself on the first tick so the success path can be tested without input.
    public sealed class InstantSuccessMinigame : MinigameBase
    {
        public override string Id => "test-instant";
        private float elapsed;
        protected override float Elapsed => elapsed;
        protected override void OnInitialize() { }
        protected override void BuildContent(RectTransform content) { }
        protected override bool OnTick(float dt) { elapsed += dt; return true; }
        protected override void Refresh() { }
        protected override MinigameResult BuildResult() => new MinigameResult { Outcome = MinigameOutcome.Success, Score = 120, CompletionPercent = 1 };
    }

    public class MinigameLifecyclePlayModeTests
    {
        private GameObject player;
        private FakePlayerInputBehaviour input;

        [SetUp] public void SetUp()
        {
            Time.timeScale = 1f;
            player = new GameObject("FakePlayer"); input = player.AddComponent<FakePlayerInputBehaviour>();
            MinigameInputLock.RegisterBlockerType<FakePlayerInputBehaviour>();
            MinigameManager.Instance.Register("test-instant", typeof(InstantSuccessMinigame), new MinigameDefinition { Title = "Instant", Instruction = "Test", DefaultTimeLimitSeconds = 0 });
        }

        [TearDown] public void TearDown()
        {
            MinigameManager.Instance.CancelActive();
            Object.Destroy(player); MinigameInputLock.ClearBlockers(); Time.timeScale = 1f;
        }

        [UnityTest] public IEnumerator EarthOpensPausesLocksInputAndRestoresOnCancel()
        {
            int calls = 0; MinigameResult result = null;
            MinigameManager.Instance.StartMinigame(new MinigameContext { MinigameId = EarthImageCalibrationMinigame.IdValue, MissionId = "earth", EventId = "t", RandomSeed = 1, AllowCancel = true },
                r => { calls++; result = r; });
            yield return null;
            Assert.IsTrue(MinigameManager.Instance.IsActive);
            Assert.AreEqual(0f, Time.timeScale); Assert.IsFalse(input.enabled);
            MinigameManager.Instance.CancelActive();
            yield return null;
            Assert.AreEqual(1, calls); Assert.AreEqual(MinigameOutcome.Cancelled, result.Outcome);
            Assert.AreEqual(1f, Time.timeScale); Assert.IsTrue(input.enabled); Assert.IsFalse(MinigameManager.Instance.IsActive);
            Assert.IsNull(Object.FindAnyObjectByType<UI.MinigameShellView>());
        }

        [UnityTest] public IEnumerator SuccessShowsResultThenRestoresAfterContinue()
        {
            int calls = 0; MinigameResult result = null;
            MinigameManager.Instance.StartMinigame(new MinigameContext { MinigameId = "test-instant", MissionId = "earth", EventId = "t" }, r => { calls++; result = r; });
            yield return null; yield return null;
            Assert.IsTrue(MinigameManager.Instance.ShowingResult); Assert.AreEqual(0, calls); Assert.IsFalse(input.enabled);
            MinigameManager.Instance.ContinueFromResult();
            yield return null;
            Assert.AreEqual(1, calls); Assert.AreEqual(MinigameOutcome.Success, result.Outcome); Assert.AreEqual(100, result.Score);
            Assert.IsTrue(input.enabled); Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest] public IEnumerator SecondStartIsRejectedAndUnknownIdFailsSafely()
        {
            MinigameResult rejected = null, unknown = null;
            MinigameManager.Instance.StartMinigame(new MinigameContext { MinigameId = EarthImageCalibrationMinigame.IdValue, MissionId = "earth" }, r => { });
            MinigameManager.Instance.StartMinigame(new MinigameContext { MinigameId = "moon-power-routing", MissionId = "moon" }, r => rejected = r);
            yield return null;
            Assert.AreEqual(MinigameOutcome.Cancelled, rejected.Outcome); Assert.IsTrue(MinigameManager.Instance.IsActive);
            MinigameManager.Instance.CancelActive();
            MinigameManager.Instance.StartMinigame(new MinigameContext { MinigameId = "does-not-exist" }, r => unknown = r);
            LogAssert.ignoreFailingMessages = true;
            yield return null;
            Assert.AreEqual(MinigameOutcome.Cancelled, unknown.Outcome); Assert.AreEqual(1f, Time.timeScale);
        }

        [Test] public void AllFourMissionMinigamesAreRegistered()
        {
            foreach (var id in new[] { "earth-image-calibration", "moon-power-routing", "mars-power-management", "jupiter-shield-allocation" })
            {
                Assert.IsTrue(MinigameManager.Instance.Registry.TryResolve(id, out var e), id);
                Assert.IsFalse(string.IsNullOrEmpty(e.Definition.Instruction), id);
            }
        }
    }
}
