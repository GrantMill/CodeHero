# CodeHero.Indexer

Phase2: Indexer implementation (local mock + pluggable providers)

Usage:
- Build: `dotnet build` in `CodeHero.Indexer`
- Run: `dotnet run --project CodeHero.Indexer`
- Output: `data/indexed.json` at repo root containing passages, metadata, and mock embeddings.

## CI: GitHub Actions workflow
The repository includes a hardened GitHub Actions workflow at `.github/workflows/indexer.yml` that runs the indexer and publishes the generated `data/indexed.json` as a build artifact. Key points and rationale:

- Triggers
 - `push` limited to `docs/**` and `CodeHero.Indexer/**` to reduce unnecessary runs.
 - Scheduled daily run (`02:00 UTC`) to ensure periodic re-indexing.
 - `workflow_dispatch` enables manual runs for recovery/testing.

- Security & permissions
 - `permissions: contents: read` limits job token scope to only what is required to checkout code.

- Concurrency and timeouts
 - `concurrency` keyed to the branch (`indexer-${{ github.ref }}`) prevents overlapping runs for the same ref and avoids unnecessary duplicate work.
 - `timeout-minutes:20` bounds CI runtime and prevents runaway jobs.

- Deterministic / reproducible checkout & build
 - `actions/checkout@v4` with `fetch-depth:0` ensures full history is available if the indexer needs commit metadata or diffs.
 - `actions/setup-dotnet@v4` is used to install the SDK. The workflow currently specifies `dotnet-version: '8.0.x'`; update this to `'10.0.x'` if you want the runner SDK to match repository TFMs (projects target .NET10).
 - `DOTNET_CLI_TELEMETRY_OPTOUT` and `DOTNET_SKIP_FIRST_TIME_EXPERIENCE` are set for CI hygiene and speed.

- Build and publish
 - Restores, builds, and publishes the `CodeHero.Indexer` app to a self-contained output folder and runs the published binary (matches how production CI should run published artifacts).

- Verification and artifacts
 - The workflow verifies that `data/indexed.json` is present and non-empty; the job will fail if verification fails to avoid silent success with no output.
 - Uses `actions/upload-artifact` to persist `data/indexed.json` with a7-day retention for debugging and audits.

## Next steps / Recommendations
- If you plan to wire real embeddings or Azure Cognitive Search in CI, add secure secrets via GitHub Actions secrets or use an ephemeral service principal with minimal privileges.
- Align `dotnet-version` in the workflow with the repo TFM (`10.0.x`) to avoid SDK/tooling mismatch when targeting .NET10.
- Replace the local `MockEmbedding` with a `FoundryEmbeddingClient` implementing retries, timeouts, and safe request limits; add tests for incremental hashing and deletion detection.

## Next development steps
- Implement `FoundryEmbeddingClient` and `CognitiveSearchClient`.
- Add incremental indexing (hash/ETag checks), deletion detection, and instrumentation.
- Add unit tests for chunking, hashing, and embedding plumbing.
