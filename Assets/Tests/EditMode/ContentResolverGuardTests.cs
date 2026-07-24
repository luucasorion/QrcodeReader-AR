using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using QRReader.Configuration;
using QRReader.Resolver;
using UnityEngine;

namespace QRReader.Tests.EditMode
{
    /// <summary>
    /// Tests for the resolver's HTTPS-only transport gate (M2-T2) and its typed no-throw contract on
    /// the rejected path (M2-T5). These run fully offline: a blocked URL is rejected before any
    /// network I/O, so no live request is made. The timeout, over-cap-on-the-wire, and successful GET
    /// paths need a real transfer and are verified on device (M6-T2); the pure size-cap accounting is
    /// covered by <see cref="DownloadSizeGuardTests"/>.
    /// </summary>
    public class ContentResolverGuardTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in _created)
            {
                if (obj != null)
                {
                    Object.DestroyImmediate(obj);
                }
            }

            _created.Clear();
        }

        [TestCase("https://example.com/pic.png")]
        [TestCase("https://example.com/")]
        [TestCase("HTTPS://EXAMPLE.COM/pic.png")] // scheme is case-insensitive
        public void HttpsOnly_Allows_Https(string url)
        {
            var resolver = new ContentResolver(MakeConfig(httpsOnly: true));
            Assert.That(resolver.IsRequestAllowed(url), Is.True);
        }

        [TestCase("http://example.com/pic.png")]
        [TestCase("ftp://example.com/pic.png")]
        [TestCase("file:///etc/passwd")]
        [TestCase("example.com/pic.png")] // no scheme
        [TestCase("not a url")]
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void HttpsOnly_Rejects_Everything_Else(string url)
        {
            var resolver = new ContentResolver(MakeConfig(httpsOnly: true));
            Assert.That(resolver.IsRequestAllowed(url), Is.False);
        }

        [TestCase("http://example.com/pic.png", true)]
        [TestCase("https://example.com/pic.png", true)]
        [TestCase("ftp://example.com/pic.png", true)]
        [TestCase(null, false)]  // empty is never requestable, even with the gate off
        [TestCase("", false)]
        [TestCase("   ", false)]
        public void HttpsOnly_Disabled_Allows_Any_Nonempty_Url(string url, bool expected)
        {
            var resolver = new ContentResolver(MakeConfig(httpsOnly: false));
            Assert.That(resolver.IsRequestAllowed(url), Is.EqualTo(expected));
        }

        // The rejected path returns before GetAsync ever issues a request (or awaits), so the Task is
        // already completed — resolving it synchronously here can't deadlock and needs no live network.
        [TestCase("http://example.com/pic.png")]
        [TestCase("ftp://example.com/pic.png")]
        [TestCase(null)]
        public void GetAsync_Blocked_Url_Fails_With_BlockedByPolicy_And_Does_Not_Throw(string url)
        {
            var resolver = new ContentResolver(MakeConfig(httpsOnly: true));

            Task<ResolveResult> task = resolver.GetAsync(url);

            Assert.That(task.IsCompleted, Is.True, "blocked URL must resolve without any network I/O");
            ResolveResult result = task.GetAwaiter().GetResult();
            Assert.That(result.Success, Is.False);
            Assert.That(result.Failure, Is.EqualTo(ResolveFailure.BlockedByPolicy));
        }

        // The gate flag is a private [SerializeField]; set it via reflection so tests don't depend on
        // an authored asset. Timeout/cap keep their (valid) defaults.
        private ContentResolverConfig MakeConfig(bool httpsOnly)
        {
            var config = ScriptableObject.CreateInstance<ContentResolverConfig>();
            typeof(ContentResolverConfig)
                .GetField("_httpsOnly", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(config, httpsOnly);
            _created.Add(config);
            return config;
        }
    }
}
