using System.Collections.Generic;
using NUnit.Framework;
using QRReader.Rendering;
using UnityEngine;

namespace QRReader.Tests.EditMode
{
    /// <summary>
    /// Tests for <see cref="LoadingSpinner"/> (M4-T4): it builds its spinner texture, toggles
    /// visibility via the renderer, and is destroyable (freeing its owned texture/material).
    /// </summary>
    public class LoadingSpinnerTests
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

        // Note: Awake/OnEnable don't run on AddComponent in edit mode, so the visual is initialized
        // lazily via SetVisible (which calls the same EnsureVisual path Awake uses at runtime).

        [Test]
        public void Builds_A_Spinner_Texture_When_Shown()
        {
            LoadingSpinner spinner = NewSpinner();

            spinner.SetVisible(true);

            Assert.That(spinner.SpinnerTexture, Is.Not.Null);
        }

        [Test]
        public void SetVisible_Toggles_The_Renderer()
        {
            LoadingSpinner spinner = NewSpinner();
            var meshRenderer = spinner.GetComponent<MeshRenderer>();

            spinner.SetVisible(false);
            Assert.That(meshRenderer.enabled, Is.False);

            spinner.SetVisible(true);
            Assert.That(meshRenderer.enabled, Is.True);
        }

        [Test]
        public void Destroy_Is_Clean()
        {
            LoadingSpinner spinner = NewSpinner();
            spinner.SetVisible(true);
            Assert.That(spinner.SpinnerTexture, Is.Not.Null);

            // The owned texture/material are freed in OnDestroy at runtime; here we only assert teardown
            // doesn't throw (edit-mode AddComponent never runs Awake/OnDestroy, so freeing can't be
            // observed in an EditMode test).
            Assert.That(() => Object.DestroyImmediate(spinner.gameObject), Throws.Nothing);
        }

        private LoadingSpinner NewSpinner()
        {
            var go = new GameObject("spinner");
            _created.Add(go);
            return go.AddComponent<LoadingSpinner>();
        }
    }
}
