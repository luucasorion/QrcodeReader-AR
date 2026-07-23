using NUnit.Framework;

namespace QRReader.Tests.EditMode
{
    /// <summary>
    /// Placeholder EditMode test that proves the test assembly, the Test Runner, and CI are wired
    /// end to end. The real tests land in milestone M2 (implementation-plan.md M2-T7): classifier
    /// extension-vs-header precedence and content-resolver guard logic (non-HTTPS, over-cap,
    /// timeout). At that point this assembly gains a reference to QRReader.Runtime and this file is
    /// replaced by those suites.
    /// </summary>
    public class SmokeTests
    {
        [Test]
        public void TestHarness_IsWiredUp()
        {
            Assert.Pass("EditMode test assembly compiles and runs.");
        }
    }
}
