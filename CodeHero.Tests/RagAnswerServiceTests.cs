using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeHero.Tests
{
    [TestClass]
    public class RagAnswerServiceTests
    {
        [TestMethod]
        public async Task Answer_IncludesCitations_WhenProvidedContexts_Skeleton()
        {
            // TODO: Implement test to verify RagAnswerService formats citations.
            // Approach (high level):
            // - Create an IHttpClientFactory that returns an HttpClient whose handler
            //   returns a crafted OpenAI/Foundry response containing the expected
            //   assistant text with citation markers.
            // - Provide AnswerRequest with Contexts containing SearchHit items with
            //   path metadata and assert the returned AnswerResponse contains
            //   citations formatted as expected (e.g., "(Source: docs/..:10-20)").

            Assert.Inconclusive("Test skeleton: implement RagAnswerService citation formatting tests.");
            await Task.CompletedTask;
        }
    }
}
