using NUnit.Framework;
using UnityEngine;

namespace CargoKing.Streets.Editor.Tests
{
    public class StreetSegmentProfileTests
    {
        private Mesh tile;
        private Material material;

        [SetUp]
        public void SetUp()
        {
            // A flat 4 m by 16 m tile running along +X, built here so it is readable by definition.
            tile = new Mesh { name = "Test Tile" };
            tile.vertices = new[]
            {
                new Vector3(0f, 0f, -8f), new Vector3(4f, 0f, -8f),
                new Vector3(0f, 0f, 8f), new Vector3(4f, 0f, 8f),
            };
            tile.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            tile.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            tile.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            tile.RecalculateBounds();

            material = new Material(Shader.Find("Sprites/Default")) { name = "Test Street" };
        }

        [TearDown]
        public void TearDown()
        {
            StreetTestFactory.DestroyAll();
            Object.DestroyImmediate(tile);
            Object.DestroyImmediate(material);
        }

        private StreetProfile TiledProfile()
        {
            StreetProfile profile = StreetTestFactory.Profile(16f);
            profile.tileMesh = tile;
            profile.material = material;
            profile.forwardAxis = StreetMeshAxis.X;
            return profile;
        }

        [Test]
        public void RoadWidth_ComesFromTheProfile()
        {
            StreetSegment segment = StreetTestFactory.Create("Road", Vector3.zero, new Vector3(50f, 0f, 0f));
            segment.profile = StreetTestFactory.Profile(9f);

            Assert.That(segment.RoadWidth, Is.EqualTo(9f));
        }

        [Test]
        public void WithoutAProfile_BuildsNoMeshAndSaysWhy()
        {
            // The silent version of this cost a whole debugging round: no mesh, no message.
            StreetSegment segment = StreetTestFactory.Create("Road", Vector3.zero, new Vector3(50f, 0f, 0f));
            segment.profile = null;

            segment.Rebuild();

            Assert.That(segment.GetComponent<MeshFilter>().sharedMesh, Is.Null);
            Assert.That(segment.TileProblem, Does.Contain("profile"));
        }

        [Test]
        public void TileProblem_NamesAProfileWithoutATile()
        {
            StreetSegment segment = StreetTestFactory.Create("Road", Vector3.zero, new Vector3(50f, 0f, 0f));

            Assert.That(segment.TileProblem, Does.Contain(StreetTestFactory.SharedProfile.name));
        }

        [Test]
        public void Rebuild_BuildsTheMeshFromTheProfilesTile()
        {
            StreetSegment segment = StreetTestFactory.Create("Road", Vector3.zero, new Vector3(50f, 0f, 0f));
            segment.profile = TiledProfile();

            segment.Rebuild();

            Mesh built = segment.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(built, Is.Not.Null);
            Assert.That(built.vertexCount, Is.GreaterThan(0));
            Assert.That(segment.TileProblem, Is.Null);
        }

        [Test]
        public void Rebuild_AppliesTheProfilesMaterial()
        {
            StreetSegment segment = StreetTestFactory.Create("Road", Vector3.zero, new Vector3(50f, 0f, 0f));
            segment.profile = TiledProfile();

            segment.Rebuild();

            Assert.That(segment.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(material));
        }
    }
}
