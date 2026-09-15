# OpsPilot workshop presenter guide

Duration: 60 minutes. Audience: students with basic C# familiarity.

## Before the session

- Share the OpsPilot folder and README with participants. Repository: https://github.com/Adarsh1999/OpsPilot; arrange participant access before the workshop.
- Test the configured Azure identity with an actual sample; `/health` checks liveness only.
- Start the app at http://localhost:5188. Keep the saved dashboard screenshot ready for network issues.
- Have participants work in pairs where Azure access is limited. Use only fictional data.
- Speaker notes are also embedded in each PowerPoint slide.

## 01. OpsPilot (1 min)

Welcome students. Introduce yourself as Adarsh Gupta, Beta Student Ambassador, retaining the designation from the supplied deck. Explain that today we will explore a working AI incident copilot and modify fictional inputs. All remediation is simulated. Start the 60-minute session clock.

## 02. What you will be able to do (2 min)

Audience: students with basic C# familiarity. Azure experience is helpful but not required. Students without model access can follow the instructor demo and run the offline tests. This is a code walkthrough and modification workshop, not a claim that everyone will write the full application from scratch in one hour. Run of show: 5 minutes context, 10 minutes tour, 15 minutes implementation, 15 minutes demo and practice, 10 minutes review, 5 minutes wrap-up.

## 03. Checkout is failing. What do you know? (2 min)

Ask for observations before explanations. The logs show a new deployment, HTTP 500s, a TaxMapper exception and 82 failures in 100 checkout requests in a 60-second window. The release is correlated with the error onset; it is not proof that the release is the root cause. Ask: what would you inspect next? All data is fictional. The displayed log text is shortened for teaching; the application receives the full fixture.

## 04. Meet OpsPilot (3 min)

Show the actual dashboard. It is a Blazor Web App using Interactive Server rendering. Select the checkout scenario, point out editable logs, and explain that Azure receives only the submitted fictional incident data and fixture context. The screenshot is from this project, not a mock product image. Live actions will be shown again during the demo. Mention that output wording varies across calls.

## 05. One request, from browser to assessment (3 min)

Walk through the diagram left to right. Blazor component events execute on the server and call the analyzer through dependency injection; there is no custom REST analysis endpoint. The analyzer enforces input and analysis limits, reads JSON fixtures, then sends a strict system message and an untrusted data message through IChatClient. AzureOpenAIClient authenticates through DefaultAzureCredential. Typed JSON is validated before display. Failure returns a conservative response. No function calling, RAG, or multi-agent loop is enabled.

## 06. The small stack behind the demo (2 min)

Keep this slide about each technology's job. Bootstrap is a third-party CSS library, so do not reuse the old deck's claim of no third-party packages. The app targets .NET 8. Microsoft.Extensions.AI and its OpenAI adapter provide the abstraction; Azure.AI.OpenAI supplies AzureOpenAIClient; Azure.Identity provides DefaultAzureCredential. Dependency versions are pinned in the project and lock files. The only custom JavaScript is clipboard interop.

## 07. A useful answer has a contract (2 min)

Open Models/Incident.cs. The real record has JsonRequired on all seven properties and a string enum converter. This simplified code excerpt omits those attributes for legibility. Explain that native structured output helps return the shape; application validation still checks the fields and citation integrity. Do not equate valid JSON with correct reasoning. Severity is Low, Medium, High, or Critical. The application forces RequiresHumanApproval to true.

## 08. The prompt sets boundaries (3 min)

Open Services/IncidentPromptBuilder.cs. The builder has no configuration dependency. It creates two messages: a system policy and JSON-encoded data. Numbered lines use [L1], [L2], and so on. Runbook and deployment content are context, not evidence of the incident. Logs are untrusted input and might contain instructions; the prompt explicitly says to ignore them. Prompting is a defense, not a proof against injection; application validation and lack of execution capability provide additional boundaries.

## 09. Before you run: tools, identity, configuration (3 min)

Preflight before the workshop: distribute the OpsPilot project folder; do not point students to the old CampusPulse repository. Install a patched .NET 8 SDK and Azure CLI. An Azure OpenAI deployment must support native structured outputs; gpt-4.1-mini was used for this demo. Deployment name is not necessarily the model name. The signed-in identity requires Cognitive Services OpenAI User on the model resource; subscription Owner alone does not supply model data-plane access. Allow RBAC propagation. User-secrets are local development storage outside the project, not encrypted production secret storage. Optional AZURE_TENANT_ID can select the tenant. Keep the endpoint as the resource root, not a Foundry project URL. Full commands are in OpsPilot/README.md. Source: https://learn.microsoft.com/en-us/azure/foundry-classic/openai/how-to/managed-identity?view=foundry-classic

## 10. Connect the model through IChatClient (3 min)

Open Services/AzureChatClientFactory.cs. This teaching excerpt leaves out endpoint checks, optional TenantId, disabled SDK content logging, network timeout and retry settings; those remain in the actual implementation. DefaultAzureCredential uses a credential chain, which can use the current Azure CLI identity locally and a managed identity when hosted. No API key is embedded in code. The factory returns the Azure chat client as IChatClient and Program.cs registers it with DI. Source: https://learn.microsoft.com/en-us/dotnet/ai/quickstarts/structured-output

## 11. Request a type. Then validate it. (3 min)

Open Services/FoundryIncidentAnalyzer.cs and AssessmentSafety.cs. This is a shortened excerpt; the real analyzer also supplies serializer options and a token budget. Explain the cancellation token. Native JSON schema comes from IncidentAssessment. The validator requires real enum values, bounded nonempty fields, exact evidence quotes, and cause references pointing to included evidence. It rejects invalid responses with a safe fallback. Show the final override that makes RequiresHumanApproval true. This is one model request, not an autonomous agent workflow.

## 12. Give the model useful context (3 min)

Open Fixtures/runbooks.json and deployments.json, then Services/Contracts.cs. The analyzer calls both services directly before inference. The interfaces are read-only and accept cancellation tokens. Unknown services have no fixture context; this is an intentional behavior, not a database lookup failure. The same methods could be wrapped as Foundry function tools in a future workshop. This project does not register tools, use retrieval, or query real deployment history.

## 13. Let's investigate a live demo (0.5 min)

Switch to the browser at http://localhost:5188. Keep the terminal visible nearby. Start the app in advance with dotnet run --project src/OpsPilot.Web from the OpsPilot folder. If Azure is unavailable, show the saved dashboard screenshot and demonstrate the real fallback without pretending a live assessment succeeded.

## 14. The demo: from log to local request (4.5 min)

Demo steps: 1. Select Checkout fails after release. 2. Click Analyze incident and show loading/cancel. 3. Trace a likely cause back to exact evidence. 4. Read the stakeholder update and copy it. 5. Point to the predetermined demo record, then click Approve remediation. 6. Show the session activity and disabled duplicate approval. 7. Change the environment field and show that the old assessment disappears. Explain that the approval is bound to the assessed input. The model does not execute a rollback; the record is generated by local C# code. Source: Components/Pages/Home.razor.cs and Services/RemediationSimulation.cs.

## 15. Your turn: change the evidence (10 min)

Run this ten-minute paired exercise. Minute 0-2: open the shared OpsPilot folder, start the app, select one scenario and predict the likely issue before calling the model. Minute 2-5: analyze it and verify every cause has a citation supporting it. Minute 5-8: remove the most diagnostic log line (TaxMapper, pool saturation, or issuer mismatch) and analyze again; compare uncertainty rather than expecting exact text. Minute 8-10: copy the update and explain to a partner which recommendation needs review. Students with no Azure access should pair with a configured machine, inspect the fixtures and prompt, and run the offline tests. Do not share personal access tokens or keys. Success: one supported observation, one hypothesis and a justified next check. Do not add real data.

## 16. Three incidents, three different clues (3 min)

Invite a pair to report from each scenario. Database: saturated pool, long stock-reservation transaction and timeouts; reachable database means connectivity alone does not explain the wait. Checkout: release timing and a TaxMapper exception; timing alone is insufficient. Authentication: expected and actual issuer differ even though the signature is valid and token not expired. Never solve the authentication case by disabling validation. These are instructor expectations from fictional fixtures, not fixed model outputs.

## 17. A safe demo must handle bad answers (3 min)

Explain the safeguards by behavior. Input validation bounds identifiers, logs and line count. The model receives strict instructions, but the app also validates the output. Evidence checks establish quote and citation integrity; they do not prove reasoning is correct. A failed request, invalid JSON, invalid evidence or context failure returns fallback. Its High enum is explicitly labeled a conservative review priority, not a measured incident severity. The UI disables approval on fallback. Every assessment requires human approval; model text never becomes a command. App authentication is intentionally out of scope, so this is a controlled workshop demo.

## 18. How do we know it works? (2 min)

Run dotnet test from the OpsPilot folder. Offline tests use deterministic fake IChatClient responses and do not need Azure. They cover prompt boundaries, malformed/missing fields, evidence integrity, caller cancellation, timeout, fixture lookup, request limits, simulation and safe exception handling. The live Azure test is deliberately opt-in, exercises all three scenarios and incurs model usage. It must fail on fallback, not silently pass. Browser verification covers interaction, copy, approval, input invalidation and mobile layout. GET /health is liveness only, never proof that Azure access works. Use current commands rather than promising a fixed test count forever.

## 19. If the workshop hits a snag (2 min)

Use metadata-only logs, not raw exceptions, credentials or user log content. 401/403: verify Azure CLI account and tenant, and the inference role on the selected model resource; newly assigned roles can take time to propagate. In this implementation session propagation took about ten minutes, but do not promise a fixed delay. 400: verify endpoint, deployment and native structured-output support. 429: distinguish Azure quota from the app's process-wide 10 attempts/minute and 2 concurrent analyses. Pair students or stagger requests. Copy needs localhost or HTTPS. If the model produces invalid evidence, show fallback and review the input before retrying. Source: OpsPilot/README.md. Publish guidance uses managed identity, HTTPS and session affinity; no deployment is required during this workshop.

## 20. What you built, and where it stops (2 min)

Ask students to summarize the pattern in their own words. We have a UI, typed request/response contract, contextual model call, validation and a human approval boundary. We do not have a production incident-management platform. There is no real rollback, Kubernetes, GitHub, Azure Monitor, authentication, database persistence, RAG, Azure AI Search or multi-agent workflow. Possible follow-up exercise: add another fictional JSON scenario and a prompt test; keep scope small. Hosting guidance is in the README and uses managed identity. Use https://github.com/Adarsh1999/OpsPilot and check participant access.

## 21. Keep learning with Microsoft Learn (2 min)

All links on this slide are clickable. Share the existing OpsPilot folder plus its README; use https://github.com/Adarsh1999/OpsPilot and check participant access. The .NET AI hub and structured-output quickstart support the code pattern in this workshop. The Azure identity article explains keyless model access. The Student Hub offers learning and community opportunities; avoid promising admission, benefits or program requirements. Sources were checked while preparing the deck.

## 22. Thank you (1 min)

Close with a quick retrieval question: what is the difference between a model returning valid JSON and a trustworthy incident assessment? Invite students to name one evidence check and one approval boundary. Ask for questions. Thank the participants and direct them to Microsoft Learn and the shared project README. Keep this final slide visible while students finish.
