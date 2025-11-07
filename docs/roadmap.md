# Roadmap

High-level plan for requirements with timeline. Dates are indicative.

```mermaid
gantt
 title CodeHero Roadmap
 dateFormat YYYY-MM-DD
 axisFormat %Y-%m-%d

 section Repo UX (M0)
 Document Map (search/preview, styling) :done, dm1,2025-10-16,3d
 Indexer UI (run+history local persistence) :done, iu1,2025-10-17,2d
 Nav & polish :done, ui2,2025-10-18,1d

 section Orchestrator & Agentic Core
 REQ-002 Orchestrator LLM (plan+act) :active, req2,2025-10-20,14d
 REQ-004 MCP Code Tools (plan/edit/diff) : req4,2025-10-27,14d

 section Knowledge & Answers
 REQ-003 RAG over Repo : req3,2025-10-22,10d

 section Conversational UX
 Conversational tests (OnPhrase/integration) : convtests,2025-10-23,5d

 section Ops & Docs
 Feature flags/docs for Continuous : flags,2025-10-24,3d
```

Notes
- Repo UX M0 completed (Document Map, Indexer UI, nav polish).
- Next focus: Orchestrator LLM core and RAG bootstrap in parallel.
- Dates will adjust as velocity data emerges.
