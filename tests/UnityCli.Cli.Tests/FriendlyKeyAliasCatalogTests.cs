using UnityCli.Protocol;

namespace UnityCli.Cli.Tests
{
    public sealed class FriendlyKeyAliasCatalogTests
    {
        [Theory]
        [InlineData(typeof(UnityEngine.Rigidbody), "damping", "m_Drag,m_LinearDamping")]
        [InlineData(typeof(UnityEngine.Rigidbody), "angularDamping", "m_AngularDrag,m_AngularDamping")]
        [InlineData(typeof(UnityEngine.Rigidbody), "collisionDetectionMode", "m_CollisionDetection")]
        [InlineData(typeof(UnityEngine.BoxCollider), "size", "m_Size")]
        [InlineData(typeof(UnityEngine.SphereCollider), "radius", "m_Radius")]
        [InlineData(typeof(UnityEngine.CapsuleCollider), "height", "m_Height")]
        [InlineData(typeof(UnityEngine.MeshCollider), "mesh", "m_Mesh")]
        [InlineData(typeof(UnityEngine.MeshRenderer), "materials[0]", "m_Materials.Array.data[0]")]
        [InlineData(typeof(UnityEngine.SkinnedMeshRenderer), "receiveShadows", "m_ReceiveShadows")]
        [InlineData(typeof(UnityEngine.Light), "shadowStrength", "m_Shadows.m_Strength")]
        [InlineData(typeof(UnityEngine.Light), "color", "m_Color")]
        [InlineData(typeof(UnityEngine.Camera), "fieldOfView", "field of view")]
        [InlineData(typeof(UnityEngine.Camera), "nearClipPlane", "near clip plane")]
        [InlineData(typeof(UnityEngine.Camera), "backgroundColor", "m_BackGroundColor")]
        [InlineData(typeof(UnityEngine.Camera), "orthographicSize", "orthographic size")]
        public void TryGetCanonicalPaths_ReturnsExpectedPaths(Type componentType, string key, string expectedPathsCsv)
        {
            bool found = FriendlyKeyAliasCatalog.TryGetCanonicalPaths(componentType, key, out IReadOnlyList<string> canonicalPaths);

            Assert.True(found);
            Assert.Equal(expectedPathsCsv.Split(','), canonicalPaths);
        }

        [Fact]
        public void TryGetCanonicalPaths_UsesCaseInsensitiveKeyComparison()
        {
            bool found = FriendlyKeyAliasCatalog.TryGetCanonicalPaths(typeof(UnityEngine.Rigidbody), "ISKINEMATIC", out IReadOnlyList<string> canonicalPaths);

            Assert.True(found);
            Assert.Equal(new[] { "m_IsKinematic" }, canonicalPaths);
        }

        [Fact]
        public void TryGetCanonicalPaths_WalksBaseTypeChain()
        {
            bool found = FriendlyKeyAliasCatalog.TryGetCanonicalPaths(typeof(UnityEngine.BoxCollider), "isTrigger", out IReadOnlyList<string> canonicalPaths);

            Assert.True(found);
            Assert.Equal(new[] { "m_IsTrigger" }, canonicalPaths);
        }

        [Fact]
        public void TryGetCanonicalPaths_ExpandsRendererMaterialArrayAliases()
        {
            bool found = FriendlyKeyAliasCatalog.TryGetCanonicalPaths(typeof(UnityEngine.MeshRenderer), "sharedMaterial[3]", out IReadOnlyList<string> canonicalPaths);

            Assert.True(found);
            Assert.Equal(new[] { "m_Materials.Array.data[3]" }, canonicalPaths);
        }

        [Theory]
        [InlineData("materials[+1]")]
        [InlineData("materials[ 0]")]
        [InlineData("materials[00]")]
        [InlineData("materials[1 ]")]
        public void TryGetCanonicalPaths_ReturnsFalseForInvalidRendererMaterialArrayIndexAliases(string key)
        {
            bool found = FriendlyKeyAliasCatalog.TryGetCanonicalPaths(typeof(UnityEngine.MeshRenderer), key, out IReadOnlyList<string> canonicalPaths);

            Assert.False(found);
            Assert.Empty(canonicalPaths);
        }

        [Fact]
        public void TryGetCanonicalPaths_ExpandsRendererMaterialArrayAliasesWithCanonicalIndexText()
        {
            bool found = FriendlyKeyAliasCatalog.TryGetCanonicalPaths(typeof(UnityEngine.MeshRenderer), "materials[42]", out IReadOnlyList<string> canonicalPaths);

            Assert.True(found);
            Assert.Equal(new[] { "m_Materials.Array.data[42]" }, canonicalPaths);
        }

        [Fact]
        public void TryGetCanonicalPaths_ReturnsFalseForUnregisteredKey()
        {
            bool found = FriendlyKeyAliasCatalog.TryGetCanonicalPaths(typeof(UnityEngine.Rigidbody), "unknownKey", out IReadOnlyList<string> canonicalPaths);

            Assert.False(found);
            Assert.Empty(canonicalPaths);
        }
    }
}

namespace UnityEngine
{
    public class Object
    {
    }

    public class Component : Object
    {
    }

    public class Behaviour : Component
    {
    }

    public class Rigidbody : Component
    {
    }

    public class Collider : Component
    {
    }

    public class BoxCollider : Collider
    {
    }

    public class SphereCollider : Collider
    {
    }

    public class CapsuleCollider : Collider
    {
    }

    public class MeshCollider : Collider
    {
    }

    public class Renderer : Component
    {
    }

    public class MeshRenderer : Renderer
    {
    }

    public class SkinnedMeshRenderer : Renderer
    {
    }

    public class Light : Behaviour
    {
    }

    public class Camera : Behaviour
    {
    }
}
