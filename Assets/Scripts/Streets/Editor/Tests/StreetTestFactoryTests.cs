using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CargoKing.Streets.Editor.Tests
{
    public class StreetTestFactoryTests
    {
        [TearDown]
        public void TearDown()
        {
            StreetTestFactory.DestroyAll();
        }

        [Test]
        public void DestroyAll_LeavesNothingForTheTestRunnersUndoToRestore()
        {
            // The Test Runner closes the scene a run took place in and only then reverts the run's
            // undo history. A merge records the destruction of the dragged segment. If that record
            // outlived the test, reverting it after the scene was gone failed a native assertion
            // ('targetScene != nullptr') once per merge - and before the factory recorded creations
            // as well, the segment even came back in whatever scene was open instead.
            //
            // Reproduced here with a scene of our own, closed before the revert just like the
            // runner's. A preview scene rather than an additive one: the scene tests run in is
            // untitled, and Unity refuses a second new scene next to an unsaved untitled one.
            Undo.IncrementCurrentGroup();
            int before = Undo.GetCurrentGroup();

            Scene scratch = EditorSceneManager.NewPreviewScene();

            try
            {
                StreetSegment target = StreetTestFactory.Create("Target", Vector3.zero, new Vector3(0f, 0f, 10f));
                StreetSegment dragged = StreetTestFactory.Create(
                    "Stray Test Segment", new Vector3(0f, 0f, 10f), new Vector3(0f, 0f, 30f));

                SceneManager.MoveGameObjectToScene(target.gameObject, scratch);
                SceneManager.MoveGameObjectToScene(dragged.gameObject, scratch);

                Assert.That(StreetSurgery.Merge(dragged, StreetEnd.Start, target, StreetEnd.End), Is.Not.Null);
                StreetTestFactory.DestroyAll();
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scratch);
            }

            Undo.RevertAllDownToGroup(before);

            LogAssert.NoUnexpectedReceived();
            Assert.That(GameObject.Find("Stray Test Segment"), Is.Null);
            Assert.That(GameObject.Find("Target"), Is.Null);
        }
    }
}
