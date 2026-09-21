using NUnit.Framework;

namespace CarCam.Tests
{
    public class AssemblySetupTests
    {
        [Test]
        public void Bridges_LiveInEditorAssembly()
        {
            Assert.AreEqual("MercedesPrototype.Editor", typeof(TimelineAiBridge).Assembly.GetName().Name);
            Assert.AreEqual("MercedesPrototype.Editor", typeof(AiCameraDirectorBridge).Assembly.GetName().Name);
            Assert.AreEqual("[[PROMPTRETURN]] SUCCESS", BridgeProtocol.SUCCESS);
        }
    }
}
