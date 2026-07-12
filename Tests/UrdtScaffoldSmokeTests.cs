using NUnit.Framework;

namespace KBP.URDT.Tests
{
    /// <summary>
    /// Smoke test proving the URDT assembly wiring (asmdef references) compiles and links.
    /// </summary>
    public sealed class UrdtScaffoldSmokeTests
    {
        [Test]
        public void RuntimeAssembly_IsLinked()
        {
            Assert.AreEqual("DavASkoURDT", UrdtRuntimeInfo.PACKAGE_NAME);
        }
    }
}
