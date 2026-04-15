using System.IO;
using System.Linq;
using BitBox.TerrainGeneration.Core;
using NUnit.Framework;

namespace BitBox.TerrainGeneration.Tests.Editor
{
    public sealed class PackageScaffoldTests
    {
        private const string PackageJsonPath = "Packages/TerrainGenerationDemo/package.json";
        private const string ChangelogPath = "Packages/TerrainGenerationDemo/CHANGELOG.md";
        private const string LicensePath = "Packages/TerrainGenerationDemo/LICENSE.md";
        private const string DemoScenePath = "Packages/TerrainGenerationDemo/Scenes/TerrainGenerationDemo.unity";

        [Test]
        public void PackageJson_Exists()
        {
            Assert.IsTrue(File.Exists(PackageJsonPath), "Expected TerraForge package metadata to exist.");
        }

        [Test]
        public void PackageJson_HasReleaseMetadata()
        {
            string packageJson = File.ReadAllText(PackageJsonPath);

            AssertPackageValue(packageJson, "name", "com.bitboxarcade.terraforge");
            AssertPackageValue(packageJson, "displayName", "TerraForge");
            AssertPackageValue(packageJson, "version", "1.0.0");
            AssertPackageValue(packageJson, "license", "MIT");
            Assert.IsTrue(packageJson.Contains("\"name\": \"BitBoxArcade\""), "Expected package author to be BitBoxArcade.");
        }

        [Test]
        public void ReleaseFiles_Exist()
        {
            Assert.IsTrue(File.Exists(ChangelogPath), "Expected TerraForge changelog to exist.");
            Assert.IsTrue(File.Exists(LicensePath), "Expected TerraForge license to exist.");
        }

        [Test]
        public void DemoScene_ExistsInPackage()
        {
            Assert.IsTrue(File.Exists(DemoScenePath), "Expected TerraForge demo scene to exist in the package.");
        }

        [Test]
        public void CoreAssembly_DoesNotReferenceUnityEditor()
        {
            bool referencesUnityEditor = typeof(TerrainGenerator).Assembly
                .GetReferencedAssemblies()
                .Any(assemblyName => assemblyName.Name == "UnityEditor");

            Assert.IsFalse(referencesUnityEditor, "Core assembly must not reference UnityEditor.");
        }

        private static void AssertPackageValue(string packageJson, string key, string expectedValue)
        {
            string expectedEntry = $"\"{key}\": \"{expectedValue}\"";
            Assert.IsTrue(packageJson.Contains(expectedEntry), $"Expected package.json to contain {expectedEntry}.");
        }
    }
}
