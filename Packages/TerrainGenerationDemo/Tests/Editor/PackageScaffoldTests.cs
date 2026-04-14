using System.IO;
using System.Linq;
using BitBox.TerrainGeneration.Core;
using NUnit.Framework;

namespace BitBox.TerrainGeneration.Tests.Editor
{
    public sealed class PackageScaffoldTests
    {
        private const string PackageJsonPath = "Packages/TerrainGenerationDemo/package.json";
        private const string DemoScenePath = "Packages/TerrainGenerationDemo/Scenes/TerrainGenerationDemo.unity";

        [Test]
        public void PackageJson_Exists()
        {
            Assert.IsTrue(File.Exists(PackageJsonPath), "Expected TerrainGenerationDemo package metadata to exist.");
        }

        [Test]
        public void DemoScene_ExistsInPackage()
        {
            Assert.IsTrue(File.Exists(DemoScenePath), "Expected TerrainGenerationDemo scene to exist in the package.");
        }

        [Test]
        public void CoreAssembly_DoesNotReferenceUnityEditor()
        {
            bool referencesUnityEditor = typeof(TerrainGenerator).Assembly
                .GetReferencedAssemblies()
                .Any(assemblyName => assemblyName.Name == "UnityEditor");

            Assert.IsFalse(referencesUnityEditor, "Core assembly must not reference UnityEditor.");
        }
    }
}
