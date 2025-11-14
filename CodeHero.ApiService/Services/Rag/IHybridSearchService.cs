/*
PSEUDOCODE / PLAN (detailed):
1. Open the existing interface file at CodeHero.ApiService\Services\Rag\IHybridSearchService.cs.
2. Add a top-of-file multi-line comment describing the development plan (pseudocode) so it is visible in the file.
3. Add comprehensive XML documentation for the interface `IHybridSearchService`:
   a. Provide a clear <summary> explaining the interface responsibility.
   b. Add a <remarks> section that contains the "Azure MCP Server" instruction set:
      - Represent each rule clearly using XML <list> items and <para> paragraphs.
      - Ensure guidance is actionable and concise (e.g., when to call bestpractices tool).
   c. Add a second <remarks> subsection containing the "Microsoft Learn" tool instructions:
      - Describe the three tools and recommended workflow steps.
      - Use XML list items and paragraphs to make the doc scan-friendly.
4. Add XML documentation to the `SearchAsync` method:
   a. Describe the SearchRequest parameter and CancellationToken.
   b. Describe the Task<SearchResponse> return value.
   c. Add <remarks> with brief usage notes and reference to Azure/Microsoft guidance already documented at the interface level.
5. Ensure all XML is valid and uses standard tags: <summary>, <param>, <returns>, <remarks>, <list>, <item>, <description>, <para>.
6. Keep the code formatting consistent with project style and include necessary using directives.
7. Do not modify method signatures or behavior; only add documentation.

End of pseudocode.
*/

using CodeHero.ApiService.Contracts;

namespace CodeHero.ApiService.Services.Rag;

/// <summary>
/// Defines a hybrid search service that combines multiple retrieval and ranking strategies
/// (for example vector search and semantic ranking) to produce a consolidated search result.
/// </summary>
/// <remarks>
/// Azure MCP Server - Usage Rules (summary)
/// <para>The following guidance summarizes the Azure MCP Server instructions relevant to code and deployment:</para>
/// <list type="bullet">
///   <item>
///     <description>Use Azure Tools: When handling requests related to Azure, always use the provided Azure tools.</description>
///   </item>
///   <item>
///     <description>Use Azure Code Gen Best Practices: For generating Azure-related code or running Azure terminal commands, invoke the <c>bestpractices</c> tool if available. Call it only when you are certain the user is discussing Azure.</description>
///   </item>
///   <item>
///     <description>Use Azure Deployment Best Practices: When preparing or deploying applications to Azure, invoke the <c>bestpractices</c> tool if available and appropriate.</description>
///   </item>
///   <item>
///     <description>Use Azure Functions Code Gen / Deployment Best Practices: For Azure Functions specific code generation or deployment preparations, invoke the <c>bestpractices</c> tool when the context is Azure Functions.</description>
///   </item>
///   <item>
///     <description>Use Azure SWA Best Practices: For Static Web Apps, invoke the <c>bestpractices</c> tool when relevant.</description>
///   </item>
///   <item>
///     <description>Plan before editing files for Azure web apps: When generating code for Azure Functions or Azure Static Web Apps, always create a plan and explain it to the user before editing any files. Proceed only after user consent.</description>
///   </item>
///   <item>
///     <description>When asked about configuring or installing azure mcp in coding agent for the current repo, invoke the <c>bestpractices</c> tool with <c>resource="coding-agent"</c> and <c>action="all"</c>.</description>
///   </item>
/// </list>
/// </remarks>
/// <remarks>
/// Microsoft Learn MCP Server - Tooling and Workflow Summary
/// <para>The Microsoft Learn MCP Server exposes three primary helper tools and a recommended workflow:</para>
/// <list type="bullet">
///   <item>
///     <description><c>microsoft_docs_search</c> — Search official Microsoft documentation and return concise, high-quality content chunks. Use this first to ground answers in authoritative material.</description>
///   </item>
///   <item>
///     <description><c>microsoft_code_sample_search</c> — Search for code snippets and examples from official docs. Use when you need sample code or implementation examples; optionally filter by language.</description>
///   </item>
///   <item>
///     <description><c>microsoft_docs_fetch</c> — Fetch a complete Microsoft documentation page (URL must be on microsoft.com) and convert to markdown. Use this after search when full content or step-by-step procedures are required.</description>
///   </item>
///   <item>
///     <description>Recommended workflow: 1) Use <c>microsoft_docs_search</c> to find relevant documents; 2) Use <c>microsoft_code_sample_search</c> for practical snippets; 3) Use <c>microsoft_docs_fetch</c> to obtain full-page details when needed.</description>
///   </item>
/// </list>
/// </remarks>
public interface IHybridSearchService
{
    /// <summary>
    /// Executes a hybrid search using the provided request parameters and returns a consolidated search response.
    /// </summary>
    /// <param name="request">The search request containing query text, filters, retrieval settings, and any hybrid search configuration.</param>
    /// <param name="ct">A <see cref="CancellationToken"/> to cancel the operation.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> that resolves to a <see cref="SearchResponse"/> containing ranked and merged results from the hybrid retrieval strategies.
    /// </returns>
    /// <remarks>
    /// Usage notes:
    /// <para>- Implementations should respect the Azure MCP guidance above when performing Azure-related operations (e.g., invoking telemetry, using Azure CLI/tools).</para>
    /// <para>- Use appropriate timeouts, retries, and observability (logs/metrics/traces) for network and remote calls.</para>
    /// </remarks>
    Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken ct = default);
}