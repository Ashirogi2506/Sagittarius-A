using System;
using NUnit.Framework;
using UnityEngine;
using MissionSimulation.Tutorial;

namespace MissionSimulation.Tests
{
    public class ControlsTutorialTests
    {
        TutorialDefinition definition;
        [SetUp] public void Setup() { definition = JsonUtility.FromJson<TutorialDefinition>(Resources.Load<TextAsset>("FarSignal.ControlsTutorial").text); }
        [Test] public void StartsAndUsesSuppliedLabel()
        {
            var tutorial = new ControlsTutorial(definition, new[] { new ControlLabel { actionId = "move", displayLabel = "WASD" } });
            tutorial.Start(); Assert.AreEqual("move", tutorial.Current().stepId); Assert.IsTrue(tutorial.Current().instruction.Contains("WASD"));
        }
        [Test] public void WrongActionsDoNotAdvance()
        {
            var tutorial = new ControlsTutorial(definition, null); tutorial.Start();
            Assert.IsFalse(tutorial.ReportAction("pickup")); Assert.AreEqual("move", tutorial.Current().stepId);
        }
        [Test] public void AllStepsCompleteInOrder()
        {
            var tutorial = new ControlsTutorial(definition, null); tutorial.Start();
            foreach (var step in definition.steps) Assert.IsTrue(tutorial.ReportAction(step.actionId));
            Assert.AreEqual(TutorialState.Completed, tutorial.State); Assert.IsNull(tutorial.Current());
        }
        [Test] public void MissingLabelIsVisibleAndCanBeUpdated()
        {
            var tutorial = new ControlsTutorial(definition, null); tutorial.Start(); Assert.IsTrue(tutorial.Current().bindingMissing);
            tutorial.SetBindings(new[] { new ControlLabel { actionId = "move", displayLabel = "Arrow keys" } });
            Assert.IsFalse(tutorial.Current().bindingMissing); Assert.IsTrue(tutorial.Current().instruction.Contains("Arrow keys"));
        }
        [Test] public void SkipStopsProgressUntilRestart()
        {
            var tutorial = new ControlsTutorial(definition, null); tutorial.Start(); tutorial.Skip();
            Assert.AreEqual(TutorialState.Skipped, tutorial.State); Assert.IsFalse(tutorial.ReportAction("move"));
            tutorial.Start(); Assert.AreEqual("move", tutorial.Current().stepId);
        }
        [Test] public void DuplicateLabelsAreRejected()
        {
            var tutorial = new ControlsTutorial(definition, new[] { new ControlLabel { actionId = "move", displayLabel = "WASD" } });
            Assert.Throws<ArgumentException>(() => tutorial.SetBindings(new[] { new ControlLabel { actionId = "move", displayLabel = "X" }, new ControlLabel { actionId = "move", displayLabel = "Y" } }));
            tutorial.Start(); Assert.IsTrue(tutorial.Current().instruction.Contains("WASD"));
        }
    }
}
