# OpsPilot: instructor walkthrough

Use this as your rehearsal script and keep it beside PowerPoint during the session. All source paths below are relative to the repository root. The existing [Presenter Guide](Presenter_Guide.md) contains notes for each of the 22 slides; this guide explains the code and the live delivery in more depth.

Repository: https://github.com/Adarsh1999/OpsPilot

## 1. Understand the project before presenting

OpsPilot helps a person investigate fictional application incidents. You give it a service name, environment and logs. It adds the matching fictional runbook and deployment history, sends that context to an Azure OpenAI model, and requests a typed incident assessment. The application checks the response before showing it in a Blazor dashboard.

The result contains seven fields: severity, summary, evidence, likely causes, recommended actions, whether human approval is required, and a stakeholder update. Evidence points back to numbered input lines. Causes are hypotheses; the human still decides whether the interpretation makes sense.

**Your opening explanation:**

> “Imagine checkout starts failing just after a release. Someone has to read the logs, identify useful clues and explain the situation to the team. Today we will use AI to help with that first investigation. We will build a structured assessment, check its evidence and keep a person in control of any next step.”

This is a copilot with a single model request per analysis, not an autonomous agent. Its runbooks and deployment history are local JSON fixtures. It does not fetch production telemetry. Approval records text in the current browser session; it does not create a ticket or contact infrastructure.

### The end-to-end flow

1. A student selects a sample or enters fictional logs in the browser.
2. A Blazor Interactive Server event invokes `Home.razor.cs` on the server.
3. The page validates input and calls `IIncidentAnalyzer` through dependency injection.
4. `FoundryIncidentAnalyzer` acquires an analysis slot and loads matching fixture context.
5. `IncidentPromptBuilder` creates the system policy and a JSON user message with numbered logs.
6. `IChatClient` calls the configured Azure OpenAI deployment using Azure identity credentials.
7. The SDK requests a JSON schema response and deserializes it into `IncidentAssessment`.
8. `AssessmentSafety` checks required content, exact evidence lines and cause citation references.
9. The UI displays the assessment, or a safe fallback if the request/response fails.
10. A separate, deterministic C# method can create a local simulated remediation record after approval.

## 2. Prepare your screen and terminal

Open the **OpsPilot** folder in VS Code, rather than the parent folder or CampusPulseDemo. In Visual Studio, open `OpsPilot.sln`.

For this machine:

```bash
cd /home/adarsh/msa-project/OpsPilot
code .
dotnet restore OpsPilot.sln --locked-mode
dotnet build OpsPilot.sln --no-restore
dotnet test OpsPilot.sln --no-build
dotnet run --project src/OpsPilot.Web
```

For a fresh clone, use `git clone https://github.com/Adarsh1999/OpsPilot.git` and `cd OpsPilot`, then follow the Azure setup in [README](../../README.md). A private repository requires collaborator access. Each participant running live inference also needs suitable Azure deployment access; cloning does not supply it.

Open `http://localhost:5188`. Open [OpsPilot_MSA_Workshop.pptx](OpsPilot_MSA_Workshop.pptx) in PowerPoint and use Presenter View to see its notes. Keep [the PDF](OpsPilot_MSA_Workshop.pdf) as a visual backup and [the dashboard screenshot](../opspilot-dashboard.png) available if inference is unavailable.

Before students arrive:

- Run one sample analysis to check actual Azure access. `/health` checks that the app is alive, not that the model works.
- Make sure the selected Azure identity has inference access and local user-secrets are configured. The README has the exact commands. Do not display `dotnet user-secrets list`, credentials or real incident logs while sharing your screen.
- Increase editor/terminal font size. Close unrelated tabs and notifications.
- Pre-open the eight core files listed below. Use Ctrl+P to jump between files and Ctrl+F to find the named method.
- Prepare a second terminal for tests. Keep the application terminal running.
- If participants have no Azure access, let them run offline tests, inspect the prompts and follow the instructor's model demonstration.

## 3. A 60-minute delivery plan

| Time | Slides | What to do |
|---|---|---|
| 0–5 min | 1–3 | Introduce the problem and ask what students would investigate in a failing checkout service. |
| 5–15 min | 4–7 | Show the dashboard, explain the request flow, stack and typed contract. |
| 15–27 min | 8–11 | Show prompt boundaries, local configuration, identity and the actual structured model call. |
| 27–30 min | 12 | Explain the runbook and deployment fixtures. |
| 30–35 min | 13–14 | Perform the checkout analysis, evidence review, stakeholder copy and simulated approval. |
| 35–45 min | 15 | Let students change the fictional evidence and compare assessments. |
| 45–53 min | 16–18 | Compare the three incidents, show fallback and run focused tests. |
| 53–60 min | 19–22 | Discuss troubleshooting, scope, learning resources and questions. |

Treat this as a guided walkthrough plus an exercise. Building the entire application from an empty folder will require a longer session. If setup overruns, have students follow the instructor demo and complete local setup afterward.

## 4. Exact files to open and what to say

### Stop 1 — The contract: `src/OpsPilot.Web/Models/Incident.cs`

Show `IncidentRequest`, then the complete `IncidentAssessment` record and `IncidentSeverity` enum.

**Say:** “The input is three values: which service, which environment, and the logs. The output has seven named fields. Instead of asking the AI for an essay and trying to split it later, we ask for a shape our C# application understands.”

Point to the string arrays: evidence, causes and actions can be rendered as separate lists. Point to `JsonRequired`: missing properties must not silently become defaults. Explain that schema correctness does not establish whether a diagnosis is true.

### Stop 2 — Application wiring: `src/OpsPilot.Web/Program.cs`

Show `AddRazorComponents().AddInteractiveServerComponents(...)`, registrations for `IChatClient`, `IIncidentAnalyzer`, `IRunbookService` and `IDeploymentHistoryService`, then the `/health` mapping.

**Say:** “Dependency injection supplies the page and analyzer with their collaborators. The interface says what a service does, while registration selects the implementation. That is also why tests can substitute a fake model.”

Interactive Server means the UI's C# event handlers execute on the server over the Blazor connection. The browser does not hold the Azure credential. There is no custom `/analyze` REST endpoint in this version.

Keep detailed exception middleware and rate-limit settings for questions rather than reading all of `Program.cs` aloud.

### Stop 3 — The frontend: `src/OpsPilot.Web/Components/Pages/Home.razor` and `Home.razor.cs`

In `Home.razor`, show the sample cards, bound inputs, Analyze button, loading state and `AssessmentPanel` component. In the code-behind, show `AnalyzeAsync`, especially the request snapshot and call to `Analyzer.AnalyzeAsync(...)`.

**Say:** “The Razor file describes what the student sees. The code-behind handles input and state. Clicking Analyze validates the form, shows a loading state and waits for the service. Cancellation lets the user stop waiting.”

Show `InputChanged`: modifying the logs clears the old assessment and approval state. An assessment should not remain actionable after its evidence changes.

Optional supporting files: `Components/Shared/AssessmentPanel.razor` renders each result section; `wwwroot/app.css` styles the dashboard; `wwwroot/js/clipboard.js` implements browser clipboard access. These are not essential AI teaching stops.

### Stop 4 — The policy and input: `src/OpsPilot.Web/Services/IncidentPromptBuilder.cs`

Show these exact system-prompt lines:

```text
Do not invent evidence, metrics, deployments, outages, recovery, or completed actions.
Evidence must contain exact entries copied from LogLines, including their [L#] prefix.
Each likely cause must be a tentative hypothesis and cite one or more [L#] IDs
also present in Evidence.
```

Then show `Build`, including `LogLines`, `Runbook`, `RecentDeployments` and the two `ChatMessage` roles.

**Say:** “The system message defines the analyst's task. The user message contains incident data. We number the logs so the response can cite specific evidence. Runbooks suggest useful checks; deployment history provides context. Neither proves the cause.”

Point out that this class has no configuration or credential dependency. Logs remain untrusted data even if a line says ‘ignore previous instructions’. Prompt wording helps communicate the boundary, but the application still needs response validation and human judgment.

### Stop 5 — Connecting Azure: `src/OpsPilot.Web/Services/AzureChatClientFactory.cs`

Show `Create`, the three configuration names, `DefaultAzureCredential`, and the SDK adapter:

```csharp
.GetChatClient(deployment).AsIChatClient();
```

**Say:** “AzureOpenAIClient knows how to communicate with Azure. DefaultAzureCredential obtains an identity token; in our local setup it can use the Azure CLI login. AsIChatClient exposes the deployment through the Microsoft.Extensions.AI interface used by the analyzer.”

Show `appsettings.example.json` only for configuration names. Actual local values are outside source control in user-secrets. Signing in is not sufficient by itself: the identity also needs the model resource's inference role. Deployed hosting can use managed identity as described in the README.

### Stop 6 — The AI call: `src/OpsPilot.Web/Services/FoundryIncidentAnalyzer.cs`

This is the most important code to show. Find `AnalyzeAsync`. Start at the two fixture calls and highlight this block:

```csharp
var response = await client.GetResponseAsync<IncidentAssessment>(
    prompts.Build(request, runbook, history), JsonOptions,
    new ChatOptions { MaxOutputTokens = 2400 },
    useJsonSchemaResponseFormat: true, cancellationToken: timeout.Token);
```

**Say:** “This is where the model is called. IncidentAssessment supplies the output type. Native JSON schema output asks the deployment to return that structure. The cancellation token bounds how long we wait.”

Continue down to the finish-reason check, `AssessmentSafety.IsGrounded`, and the forced `RequiresHumanApproval = true`.

**Say:** “A completed JSON response is only the first check. We reject incomplete output and invalid evidence references. The application always requires approval, even if the model tries to return false.”

Finally show the catch blocks: caller cancellation propagates; request, timeout, context and parsing failures return a fallback. Logs record an analysis ID, duration and failure category without logging raw SDK exception bodies or prompt content.

### Stop 7 — Checking the answer: `src/OpsPilot.Web/Services/AssessmentSafety.cs`

Show `IsGrounded`, especially exact evidence matching and the citation checks, followed by `Fallback`.

**Say:** “The app verifies that an evidence entry is an exact numbered input line and that a cause cites evidence included in the answer. This catches fabricated or broken references. It cannot prove that a real log line logically supports a diagnosis; we still review the reasoning.”

The fallback has empty evidence and causes, requires human review, and uses High as a conservative review priority. Do not present this fallback severity as an AI finding about the incident. Approval is disabled for it.

### Stop 8 — The approval boundary: `src/OpsPilot.Web/Services/RemediationSimulation.cs`

Show `CanApprove`, `Describe` and `Create`. Then briefly show `Approve` in `Home.razor.cs`.

**Say:** “This button does not execute the model's recommended actions. Our own C# code creates a predefined text record. For checkout v2.4 it says ‘Rollback request created for checkout-api-v2.4.’ That is a simulation, not a real rollback request sent to another system.”

The UI rejects stale inputs and repeated approval for the same assessment. The record lives in the current circuit and is lost on refresh. There is no durable audit log, ticket integration or infrastructure client.

### Supporting files — use when discussing context or answering questions

| File | Explain |
|---|---|
| `src/OpsPilot.Web/Services/Contracts.cs` | The analyzer and context interfaces. These are seams for later implementations. |
| `src/OpsPilot.Web/Services/FixtureServices.cs` | Loads local files and selects context for the requested service. |
| `src/OpsPilot.Web/Fixtures/scenarios.json` | The three fictional incidents loaded by the sample cards. |
| `src/OpsPilot.Web/Fixtures/runbooks.json` | Service-specific investigation guidance. |
| `src/OpsPilot.Web/Fixtures/deployments.json` | Fictional recent release history. |
| `src/OpsPilot.Web/Services/AnalysisGate.cs` | Shared limit of 10 analyses/minute and two concurrent analyses, with no waiting queue. |
| `src/OpsPilot.Web/Services/IncidentRequestValidator.cs` | Service/environment format and log size/line limits. |
| `tests/OpsPilot.Tests/PromptBuilderTests.cs` | Policy/data separation, numbered logs, fixture context and invalid input tests. |
| `tests/OpsPilot.Tests/AnalyzerTests.cs` | Native schema, invalid response, fallback, cancellation and timeout tests with a fake client. |

## 5. Live demo: exactly what to do

### Checkout — the main five-minute demonstration

1. Select **Checkout fails after release**. Read the service/environment before analyzing.
2. Point to the v2.4 deployment, HTTP 500 responses, `TaxMapper.Map(cart.taxProfile)` exception and 82 errors out of 100 requests. Ask: “What do we know, and what is still a hypothesis?”
3. Click **Analyze incident**. During loading, explain that the server combines the logs with fixture context and makes one structured model request.
4. Read the summary. Open one evidence entry and match its `[L#]` to the corresponding input line. Follow a likely cause's citation to that evidence. Explain why the release timing is a clue rather than proof.
5. Review recommendations. Ask which actions are investigation and which would need an operator's decision.
6. Click **Copy stakeholder update** and paste into an empty local text editor. Read it as a concise update to a team. Do not actually send it anywhere.
7. Point to the simulation label and proposed record, then click **Approve remediation**. Show the new activity entry and disabled repeated approval.
8. Say: “No deployment changed. We have demonstrated a user decision and a local record.” Edit the input to show the prior assessment becomes invalid.

Do not promise exact wording or severity between runs. Review grounding, cautious language and the workflow rather than memorizing a particular response.

### Compare the other two scenarios

**PostgreSQL pool exhaustion:** show `active=100`, `idle=0`, queued requests, connection acquisition timeout and the long transaction. A reachable database does not mean the application can obtain a pooled connection. Ask students to distinguish pool pressure from proof that the database is down.

**Authentication issuer mismatch:** show expected versus actual issuer, HTTP 401, valid signature and unexpired token. A signature can be valid while the token comes from an unexpected issuer. Ask what configuration should be checked. Do not recommend disabling validation as a fix.

### A controlled failure demonstration

In a second terminal, from the repository root:

```bash
AZURE_OPENAI_DEPLOYMENT=missing-workshop-deployment dotnet run --project src/OpsPilot.Web -- --urls http://localhost:5189
```

Open port 5189 and analyze a sample. This process-only override leaves saved settings untouched. Show the unavailable assessment, empty evidence/causes and disabled approval. Stop the second process with Ctrl+C and return to port 5188. If the request takes too long, explain the 60-second timeout and continue with the offline fallback test.

Also try empty logs to show validation before any model request. Demonstrate Cancel when a request lasts long enough; a fast response may finish before you can click it.

## 6. Ten-minute student exercise

Ask students to select checkout, then remove the deployment and exception lines while leaving the HTTP error observations. Analyze again.

**Instructions to say:** “You removed evidence about the release and exception. Does the answer now make more cautious claims? Check every cited line. Deployment history still exists as context, so the model may suggest checking a release; it should not claim that context proves the release caused the failures.”

Give pairs five minutes to compare, three minutes to inspect citations, and two minutes to share one observation. Success means they can distinguish observed failure from a suggested cause, not that everyone gets identical wording.

For students without Azure access, inspect `IncidentPromptBuilder.Build` and run `PromptBuilderTests`. Have them explain what the input messages contain and which data should never be added to a prompt. An optional follow-up is adding a fourth fictional scenario in JSON and re-running the app.

## 7. Show that the behavior is tested

In the second terminal:

```bash
dotnet test OpsPilot.sln --filter 'FullyQualifiedName~PromptBuilderTests|FullyQualifiedName~AnalyzerTests'
```

Open these methods by name:

- `Separates_untrusted_data_and_numbers_log_evidence`: verifies how the request is assembled.
- `Requests_native_schema_includes_context_and_forces_approval`: verifies the model request contract and application approval policy.
- `Parse_failures_return_safe_fallback`: verifies malformed output becomes a reviewable fallback.
- `Ai_failure_never_exposes_exception_details_in_result_or_logs`: verifies sensitive error detail is not exposed.

**Say:** “These tests use a fake chat client so they are repeatable without Azure. They check the application's response to known inputs. A live model test is separate and opt-in; passing unit tests does not prove Azure credentials or model availability.”

## 8. Questions you are likely to get

**Is this a chatbot?** It uses a chat-model API internally, but the product is a focused incident form and structured assessment, without conversation history.

**Does it use RAG?** No. It selects small local fixtures by service name and includes them directly in the prompt. There is no vector store, search index or retrieval pipeline.

**Are the runbook methods Foundry tools?** Not yet. The application invokes them before the model call. Their interfaces make later tool integration possible, but there is no model-directed tool calling here.

**Can it fix the incident?** No. It recommends actions and creates an in-memory simulation record after a click. Real execution would be a separate project requiring actual authorization and operational controls.

**Does JSON schema stop hallucinations?** It constrains the response shape. Exact-line checks constrain references. Neither guarantees that a diagnosis is correct.

**Why Blazor?** It lets this workshop keep UI event handling and backend services in C#, using Bootstrap for presentation and a small JavaScript clipboard helper.

**Why does Azure login still fail?** Authentication identifies you; RBAC authorizes inference. Check the selected identity, tenant, resource and Cognitive Services OpenAI User role using the README. Roles can take time to propagate.

**Can everyone use my running app?** There is no application authentication and the analysis gate is shared. For this workshop, use the instructor's local demo or independently configured student instances. Hosting instructions are in the README; this is not a production incident portal.

## 9. Closing words

> “You have followed an AI feature from a Blazor form to a typed model response, checked the evidence references and kept approval in application code. The practical lesson is to make the model's answer inspectable, handle failure deliberately and be precise about what the application can actually do.”

Share the repository according to its access settings and direct participants to the README setup and tests. Use slide 21 for Microsoft Learn resources.

## Presentation editing

The editable PowerPoint and PDF are included beside this guide. The PowerPoint retains the supplied Microsoft template assets and has speaker notes. For ordinary edits, use PowerPoint directly.

The optional `build_deck.py` generator requires Python with `python-pptx` and the original `MSLearn_SA_adarsh.pptx` supplied separately:

```bash
python docs/workshop/build_deck.py --template /path/to/MSLearn_SA_adarsh.pptx
```

It rebuilds the PowerPoint and generated Presenter Guide in this folder; it does not overwrite this detailed walkthrough or regenerate the PDF. Export the revised PowerPoint as PDF afterward. The generator is specific to the supplied template's layouts, not an arbitrary-template converter.
