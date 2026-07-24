using System.Collections.Generic;
using NUnit.Framework;
using QRReader.Rendering;
using UnityEngine;

namespace QRReader.Tests.EditMode
{
    /// <summary>
    /// Tests for <see cref="ErrorIcon"/> (M4-T5): it builds its icon texture, toggles visibility via
    /// the renderer, and destroys cleanly (freeing its owned texture/material at runtime).
    /// </summary>
    public class ErrorIconTests
    {
        private readonly List<GameObject> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _created)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _created.Clear();
        }

        // Note: Awake/OnDestroy don't run on AddComponent in edit mode, so the visual is initialized
        // lazily via SetVisible (the same EnsureVisual path Awake uses at runtime).

        [Test]
        public void Builds_An_Icon_Texture_When_Shown()
        {
            ErrorIcon icon = NewIcon();

            icon.SetVisible(true);

            Assert.That(icon.IconTexture, Is.Not.Null);
        }

        [Test]
        public void SetVisible_Toggles_The_Renderer()
        {
            ErrorIcon icon = NewIcon();
            var meshRenderer = icon.GetComponent<MeshRenderer>();

            icon.SetVisible(false);
            Assert.That(meshRenderer.enabled, Is.False);

            icon.SetVisible(true);
            Assert.That(meshRenderer.enabled, Is.True);
        }

        [Test]
        public void Destroy_Is_Clean()
        {
            ErrorIcon icon = NewIcon();
            icon.SetVisible(true);
            Assert.That(icon.IconTexture, Is.Not.Null);

            Assert.That(() => Object.DestroyImmediate(icon.gameObject), Throws.Nothing);
        }

        private ErrorIcon NewIcon()
        {
            var go = new GameObject("error");
            _created.Add(go);
            return go.AddComponent<ErrorIcon>();
        }
    }
}
