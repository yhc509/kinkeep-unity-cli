using UnityCli.Protocol;

namespace UnityCli.Cli.Tests
{
    public sealed class FriendlyKeyAliasCatalogTests
    {
        [Theory]
        [InlineData(typeof(UnityEngine.Rigidbody), "damping", "m_Drag")]
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
        public void TryGetCanonicalPath_ReturnsExpectedPath(Type componentType, string key, string expectedPath)
        {
            bool found = FriendlyKeyAliasCatalog.TryGetCanonicalPath(componentType, key, out string canonicalPath);

            Assert.True(found);
            Assert.Equal(expectedPath, canonicalPath);
        }

        [Fact]
        public void TryGetCanonicalPath_UsesCaseInsensitiveKeyComparison()
        {
            bool found = FriendlyKeyAliasCatalog.TryGetCanonicalPath(typeof(UnityEngine.Rigidbody), "ISKINEMATIC", out string canonicalPath);

            Assert.True(found);
            Assert.Equal("m_IsKinematic", canonicalPath);
        }

        [Fact]
        public void TryGetCanonicalPath_WalksBaseTypeChain()
        {
            bool found = FriendlyKeyAliasCatalog.TryGetCanonicalPath(typeof(UnityEngine.BoxCollider), "isTrigger", out string canonicalPath);

            Assert.True(found);
            Assert.Equal("m_IsTrigger", canonicalPath);
        }

        [Fact]
        public void TryGetCanonicalPath_ExpandsRendererMaterialArrayAliases()
        {
            bool found = FriendlyKeyAliasCatalog.TryGetCanonicalPath(typeof(UnityEngine.MeshRenderer), "sharedMaterial[3]", out string canonicalPath);

            Assert.True(found);
            Assert.Equal("m_Materials.Array.data[3]", canonicalPath);
        }

        [Theory]
        [InlineData("materials[+1]")]
        [InlineData("materials[ 0]")]
        [InlineData("materials[00]")]
        [InlineData("materials[1 ]")]
        public void TryGetCanonicalPath_ReturnsFalseForInvalidRendererMaterialArrayIndexAliases(string key)
        {
            bool found = FriendlyKeyAliasCatalog.TryGetCanonicalPath(typeof(UnityEngine.MeshRenderer), key, out string canonicalPath);

            Assert.False(found);
            Assert.Equal(string.Empty, canonicalPath);
        }

        [Fact]
        public void TryGetCanonicalPath_ExpandsRendererMaterialArrayAliasesWithCanonicalIndexText()
        {
            bool found = FriendlyKeyAliasCatalog.TryGetCanonicalPath(typeof(UnityEngine.MeshRenderer), "materials[42]", out string canonicalPath);

            Assert.True(found);
            Assert.Equal("m_Materials.Array.data[42]", canonicalPath);
        }

        [Fact]
        public void TryGetCanonicalPath_ReturnsFalseForUnregisteredKey()
        {
            bool found = FriendlyKeyAliasCatalog.TryGetCanonicalPath(typeof(UnityEngine.Rigidbody), "unknownKey", out string canonicalPath);

            Assert.False(found);
            Assert.Equal(string.Empty, canonicalPath);
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
