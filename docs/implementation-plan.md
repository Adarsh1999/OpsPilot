# OpsPilot implementation plan

## Goal and design
Build a .NET 8 Blazor Web App (Interactive Server) that assesses fictional incidents through Azure OpenAI using Microsoft.Extensions.AI. Use one web project and one test project. The UI calls IIncidentAnalyzer through dependency injection; the analyzer reads JSON context through tool-ready interfaces, builds a bounded prompt, requests a typed IncidentAssessment, validates evidence citations, and returns a conservative fallback on failure. Only local memory is used for simulated approvals.

The supplied requirements authorize implementation after this plan. Execute stages inline, with build and tests at each checkpoint. No infrastructure is created for remediation, and no real remediation integrations exist.

## Folder structure
```text
OpsPilot/
  OpsPilot.sln
  global.json
  .gitignore
  README.md
  docs/implementation-plan.md
  src/OpsPilot.Web/
    Components/{Layout,Pages,Shared}/    # dashboard, result, error boundary
    Models/                            # request, assessment, fixture records
    Services/                          # analyzer, prompt, validation, fixtures, simulation
    Fixtures/                          # scenarios, runbooks, deployments JSON
    wwwroot/{bootstrap,js}/             # Bootstrap, app.css, clipboard interop
    Program.cs                         # DI, exception handling, limiter, health
    appsettings.json                   # logging only
    appsettings.example.json           # placeholders only
    Properties/launchSettings.json
  tests/OpsPilot.Tests/                 # prompt, fallback, validation, fixture, host tests
  scripts/configure-azure.sh           # optional local setup via az CLI; no keys
```

## Stage 1: Contracts, fixtures, and prompt safety
- [x] Scaffold the .NET 8 solution and xUnit test project. Resolve and pin current compatible stable packages and track NuGet lock files.
- [x] Define IncidentSeverity, IncidentRequest, IncidentAssessment and IIncidentAnalyzer.AnalyzeAsync(request, cancellationToken).
- [x] Define IRunbookService.GetRunbookAsync(serviceName, cancellationToken) and IDeploymentHistoryService.GetRecentDeploymentsAsync(serviceName, cancellationToken). Implement read-only JSON fixtures for the three specified incidents.
- [x] Write tests for numbered log evidence, untrusted input framing, contextual runbooks/deployments, unknown services, and request limits. Run tests before implementing the prompt builder and input validator.
- [x] Implement separate system/user messages. Keep configuration outside the prompt builder. Require verbatim evidence tagged [L#] and each likely cause to cite supplied evidence. Bound logs to 16,000 characters, 200 lines; service to 80 and environment to 40 characters.
- [x] Run `dotnet build OpsPilot.sln` and `dotnet test OpsPilot.sln`.

## Stage 2: Foundry integration and resilience
- [x] Write analyzer tests with a deterministic IChatClient fake for successful JSON, malformed/missing/invalid fields, unsupported evidence, request failure, timeout, and caller cancellation.
- [x] Register AzureOpenAIClient with DefaultAzureCredential and expose AsIChatClient. Use GetResponseAsync<IncidentAssessment> with native JSON schema. Force approval for every assessment; validate evidence and cause citations before returning.
- [x] Handle missing configuration and AI/parse/context failures with an explicit human-review fallback, no fabricated evidence or severity certainty. Propagate caller cancellation and apply a 60-second request timeout.
- [x] Add structured metadata-only logging, process-wide analysis rate limiting (also protecting Blazor circuit calls), safe HTTP exception handling, and /health.
- [x] Use Azure CLI to discover an existing suitable deployment. Store endpoint/deployment/tenant only in local user-secrets; do not retrieve keys. Verify actual inference with fictional inputs if permissions allow.
- [x] Run `dotnet build OpsPilot.sln` and `dotnet test OpsPilot.sln`.

## Stage 3: Workshop dashboard and delivery
- [x] Build a responsive Bootstrap dashboard: scenario cards, editable fields, logs, validation, loading/cancel, severity and complete assessment, clipboard action, and explicit simulated approval activity.
- [x] Use a navy/white operations workbench with blue controls, amber approval state and severity colors; system sans-serif for prose, monospace for logs. Left-aligned two-column workspace collapses on mobile. Fixture cards carry distinct service symptoms. Keep implementation details out of the interaction flow.
- [x] Keep approval bound to the assessed request and deterministic simulation record. Invalidate old results on input edits; allow one simulated approval per assessment. Disable approval on fallback. No external actions, authentication, persistence, or agent workflows.
- [x] Add README setup, Entra roles, user-secrets, Mermaid architecture, fixture/tool seam explanation, workshop script, limitations, tests, and optional managed-identity App Service deployment guidance.
- [x] Add host checks for dashboard/health/error handling and confirm UI behavior in a real browser where available. Test all three incidents live and verify fallback, loading, clipboard, and simulated approval.
- [x] Run final `dotnet build OpsPilot.sln`, `dotnet test OpsPilot.sln`, and Release publish. Record actual results and any live verification limitations.

## Checkpoint evidence

- Stage 1: clean build; 7 prompt/input tests passed after the initial failing run.
- Stage 2: clean build; 27 offline tests passed; native output, fallback, cancellation, context, limits and health verified.
- Stage 3: clean build; 34 offline tests passed; Release publish succeeded. The opt-in live Azure test passed, checking all three scenarios.
- Browser: desktop and 390px mobile layouts inspected; validation, loading/cancel, safe fallback, disabled fallback approval, input-change invalidation and full-budget Unicode paste checked.
- Azure CLI: existing compatible deployment selected, user-secrets configured, resource-scoped inference role assigned to signed-in account. Role propagation completed; the opt-in live test passed all three scenarios through the real analyzer.
- Final browser flow: live checkout assessment with 4 evidence entries; clipboard text matched; simulated rollback record created; duplicate approval disabled; changing environment cleared the result. Screenshot: `docs/opspilot-dashboard.png`.
- Published artifact smoke test: dashboard and all scenarios rendered; health returned 200; the HTTP request budget produced 429 responses while health remained 200.
- Locked restore and package vulnerability scan passed; no configured Azure resource identifiers were found in project source.
- Live model output is nondeterministic. Some earlier responses failed validation and correctly returned the safe fallback; no retry loop or weakened validation was introduced.
