using LegendsViewer.Frontend;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendsViewer.Backend.Tests;

[TestClass]
public class WebAppStaticServerTests
{
    [TestMethod]
    public void HasMatchingBuildAssets_RejectsIndexMissingHashedJavascript()
    {
        string root = Path.Combine(Path.GetTempPath(), "legends-frontend-test-" + Guid.NewGuid().ToString("N"));
        string assetRoot = Path.Combine(root, "assets");
        Directory.CreateDirectory(assetRoot);
        try
        {
            File.WriteAllText(Path.Combine(root, "index.html"),
                "<html><head><link rel=\"stylesheet\" href=\"/assets/index-456.css\"></head>" +
                "<body><script type=\"module\" src=\"/assets/index-123.js\"></script></body></html>");
            File.WriteAllText(Path.Combine(assetRoot, "index-456.css"), "body{}");

            Assert.IsFalse(WebAppStaticServer.HasMatchingBuildAssets(root, out string missing));
            StringAssert.Contains(missing, "index-123.js");

            File.WriteAllText(Path.Combine(assetRoot, "index-123.js"), "export {};");
            Assert.IsTrue(WebAppStaticServer.HasMatchingBuildAssets(root, out string complete), complete);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void HasMatchingBuildAssets_RejectsMissingIndexAndUnbuiltViteEntry()
    {
        string root = Path.Combine(Path.GetTempPath(), "legends-frontend-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Assert.IsFalse(WebAppStaticServer.HasMatchingBuildAssets(root, out string missingIndex));
            StringAssert.Contains(missingIndex, "index.html");

            File.WriteAllText(Path.Combine(root, "index.html"),
                "<html><script type=\"module\" src=\"/src/main.ts\"></script></html>");
            Assert.IsFalse(WebAppStaticServer.HasMatchingBuildAssets(root, out string unbuilt));
            StringAssert.Contains(unbuilt, "No compiled Vite asset");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
