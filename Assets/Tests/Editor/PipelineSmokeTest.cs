using NUnit.Framework;

namespace UrdtSetup.SmokeTests
{
    /// <summary>
    /// Sanity test used only to confirm the `unity test` CLI pipeline discovers and runs
    /// EditMode tests in this project. Not part of the URDT package.
    /// </summary>
    public class PipelineSmokeTest
    {
        [Test]
        public void UnityCli_Test_Pipeline_Is_Reachable()
        {
            Assert.Pass("unity CLI EditMode test pipeline reached this test.");
        }
    }
}
