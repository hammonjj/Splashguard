#if UNITY_EDITOR
using System;
using System.Linq;
using BitBox.Toymageddon.SceneManagement.Editor;
using NUnit.Framework;
using NUnitAssert = NUnit.Framework.Assert;
using UnityEditor;

namespace BitBox.Toymageddon.Tests.Editor
{
    public sealed class SceneManagementBootstrapTests
    {
        private const string SandboxScenePath = "Assets/Scenes/Sandbox.unity";
        private EditorBuildSettingsScene[] _originalScenes;

        [SetUp]
        public void SetUp()
        {
            _originalScenes = EditorBuildSettings.scenes
                .Select(scene => new EditorBuildSettingsScene(scene.path, scene.enabled))
                .ToArray();
        }

        [TearDown]
        public void TearDown()
        {
            EditorBuildSettings.scenes = _originalScenes;
        }

        [Test]
        public void GetOrCreateConfig_AddsSandboxSceneToBuildSettingsWhenMissing()
        {
            EditorBuildSettings.scenes = EditorBuildSettings.scenes
                .Where(scene => !string.Equals(scene.path, SandboxScenePath, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            SceneManagementBootstrap.GetOrCreateConfig();

            NUnitAssert.IsTrue(
                EditorBuildSettings.scenes.Any(scene =>
                    string.Equals(scene.path, SandboxScenePath, StringComparison.OrdinalIgnoreCase)
                    && scene.enabled),
                "Expected Sandbox to be added to the build/shared scene list.");
        }

        [Test]
        public void GetOrCreateConfig_EnablesSandboxSceneWhenPresentButDisabled()
        {
            EditorBuildSettings.scenes = EditorBuildSettings.scenes
                .Select(scene => string.Equals(scene.path, SandboxScenePath, StringComparison.OrdinalIgnoreCase)
                    ? new EditorBuildSettingsScene(scene.path, false)
                    : new EditorBuildSettingsScene(scene.path, scene.enabled))
                .ToArray();

            SceneManagementBootstrap.GetOrCreateConfig();

            EditorBuildSettingsScene sandboxScene = EditorBuildSettings.scenes.FirstOrDefault(scene =>
                string.Equals(scene.path, SandboxScenePath, StringComparison.OrdinalIgnoreCase));

            NUnitAssert.IsNotNull(sandboxScene);
            NUnitAssert.IsTrue(sandboxScene.enabled, "Expected Sandbox to be enabled in the build/shared scene list.");
        }
    }
}
#endif
