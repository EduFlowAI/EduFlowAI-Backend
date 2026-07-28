# Seif — Step-by-Step Implementation Guide

**Role:** Document Intelligence and Deployment Engineer
**Priority source for the messaging/handler design:** `SeifDocumentVerifyIntegrationGuide.md` (wins over the two team-distribution docs wherever they disagree)
**Scope docs reconciled here:** `EduFlowAI_Final_Team_Distribution_Plan.md` (Dev 2), `Fucken_tasks.md` ("Seif"), `SeifDocumentVerifyIntegrationGuide.md`

This guide was written after auditing the actual `EduFlowAI-Backend` repository (not just the planning docs), on branch `claude/seif-integration-guide-fcbrrq`. Several things described as "planned" in `BACKEND_ARCHITECTURE.md` are already built by your teammates. A few assumptions in the integration guide don't match the real project-reference graph. Both are called out below so you don't waste time re-deriving them.

---

## 0. What already exists vs. what is actually yours to build

### Already built by the team (do not duplicate)

| Piece | Owner | Where |
|---|---|---|
| `VerifyApplicantDocumentV1` command contract | Ali | `EduFlowAI.Shared.Messaging.Contracts/Documents/V1/VerifyApplicantDocumentV1.cs` |
| `ApplicantDocumentVerificationCompletedV1` / `FailedV1` result contracts | Ali | same folder — **match the integration guide exactly, don't touch** |
| `DocumentVerificationOutcomeV1`, `VerificationFieldComparisonV1`, `DocumentVerificationDetailsV1` | Ali | same folder |
| RabbitMQ queues + Wolverine publish/listen routes for all of the above | Ali | `MessagingConfiguration.cs`, `MessagingQueueNames.cs` — Worker already listens on `eduflow.documents.verification.v1` |
| Outbox publish of `VerifyApplicantDocumentV1` on submit | Mansy | `DocumentSubmissionService.SubmitPackageAsync` |
| Consumer handlers that apply your result to `ApplicantDocument.Status` | Mansy | `ApplicantDocumentVerificationCompletedV1Handler.cs`, `ApplicantDocumentVerificationFailedV1Handler.cs` |
| Protected file storage with `OpenReadAsync(storageKey, ct)` | Mansy | `EduFlowAI.Documents/Infrastructure/Storage/FileStorageService.cs` (interface: `EduFlowAI.Documents.Application.Interfaces.IFileStorageService`) |
| Gemini HTTP call plumbing (model/key fallback, JSON-schema responses, inline-data document upload) | Khorshid | `EduFlowAI.AI/Infrastructure/ExternalServices/GeminiChatClient.cs` (`IGeminiChatClient`) |

**Net effect:** the entire messaging skeleton around your feature is done. There is nothing to build in `EduFlowAI.Worker`, `EduFlowAI.Persistence/Messaging`, or `Shared.Messaging.Contracts`. Your job is 100% inside `EduFlowAI.AI` (the handler + verification logic), a thin read-only slice of `EduFlowAI.Documents` (query endpoints), and deployment.

### Missing / genuinely yours

Confirmed by grep — none of this exists yet anywhere in the repo:

- No `Features/DocumentVerification` folder in `EduFlowAI.AI`.
- No `[assembly: WolverineModule]` attribute file in `EduFlowAI.AI` at all (every other module — Documents, Communication, Api, Worker — has one; AI does not). **Without this, Wolverine will never discover your handler**, no matter how correctly you write it.
- No `IDocumentVerificationService`, no `IDocumentVerificationFileReader`, no `IDocumentVerificationContextReader`, no `VerifyApplicantDocumentV1Handler`.
- No query endpoints for verification detail/history (`GET /api/documents/{id}/verification...`).
- No Dockerfiles anywhere in the repo, no root `docker-compose.yml` (only a stray `EduFlowAI.Api/docker-compose.yml` that just spins up RabbitMQ — not a real multi-service compose), no `.github/workflows`.

### Correction to the integration guide's dependency assumption

The integration guide says: *"The Documents project already references AI, so it can implement the AI-owned interface without creating a circular dependency."*

**That's backwards in this repo.** Check `EduFlowAI.AI.csproj`: it has `<ProjectReference Include="..\EduFlowAI.Documents\EduFlowAI.Documents.csproj" />`. `EduFlowAI.Documents.csproj` does **not** reference AI. So:

- `EduFlowAI.AI` → `EduFlowAI.Documents` (direct)
- `EduFlowAI.AI` → `EduFlowAI.Admission` (direct)
- `EduFlowAI.Documents` → `EduFlowAI.Admission` → `EduFlowAI.Identity` (transitive), so `EduFlowAI.AI` also has legitimate compile-time access to `EduFlowAI.Identity` types through that chain.

Practical implication: **you can implement `IDocumentVerificationFileReader` and `IDocumentVerificationContextReader` yourself, inside `EduFlowAI.AI`**, by injecting Mansy's `IFileStorageService` and a couple of read-only Admission/Identity contracts directly. You do not need to ask Mansy to implement an AI-owned interface. Keep the two interfaces (don't just inject `IFileStorageService` straight into the handler) — that still isolates the handler from Mansy's exact storage API and matches the guide's intent — but you own both the interface *and* the implementation.

---

## 1. Decisions to lock before writing code

These are genuine gaps in every doc you were given. Don't guess silently — resolve them in a 10-minute Slack/standup message to the relevant owner, then write the decision into your PR description.

1. **Expected-field mapping per `DocumentType`.** `DocumentType` (owned by Abdallah, `EduFlowAI.Admission.Domain.Enums.DocumentType`) has four values: `NationalId`, `BirthCertificate`, `GraduationCertificate`, `MilitaryCertificate`. `ApplicantProfile` (Karim, `EduFlowAI.Identity.Domain.Entities.ApplicantProfile`) has `FullNameEn/Ar`, `NationalId`, `DateOfBirth`, `University`, `Faculty`, `Major`, `GraduationYear`, `MilitaryStatus`, etc. Nobody has written down which profile fields Gemini should extract-and-compare for each document type. Propose something like:
   - `NationalId` → `FullNameAr`, `NationalId`, `DateOfBirth`
   - `BirthCertificate` → `FullNameAr`, `DateOfBirth`
   - `GraduationCertificate` → `FullNameEn`, `University`, `Faculty`, `Major`, `GraduationYear`
   - `MilitaryCertificate` → `FullNameAr`, `MilitaryStatus`

   Confirm this with Abdallah/Karim before writing the prompt — it's the actual "business logic" of your feature.

2. **Attempt history persistence.** The integration guide is explicit: your handler is `[NonTransactional]`, does **no business-table write**, and only publishes messages. But `Fucken_tasks.md` gives you a `GET /api/documents/{documentId}/verification/attempts` endpoint and a recommended `DocumentVerificationResult` table with `AttemptNumber`. Today, Mansy's handler stores only the *latest* result as `ApplicantDocument.VerificationDetailsJson` + `VerifiedAt` — there is no attempt history anywhere.
   - **Recommendation (follow this unless told otherwise):** ship MVP as "current result only." Build `GET /api/documents/{id}/verification` reading `ApplicantDocument.VerificationDetailsJson`, and make `/attempts` return a single-element array wrapping that same result. Do **not** add a new `DocumentVerificationResult` table yourself — it would duplicate data Mansy already owns and re-introduce the direct-DB-write the integration guide forbids you from doing in the handler. If real attempt history becomes a hard requirement, that's a schema conversation with Mansy (he owns `ApplicantDocument`), not something to bolt on unilaterally.

3. **Temporary-file cleanup on Gemini's side.** Decide whether you're using the Gemini Files API (upload once, reference by URI, explicit delete) or inline-data (base64 in the request body, nothing to clean up). See §4 — inline-data is what the codebase already does elsewhere and is simpler; recommend it unless request-size limits force the Files API.

---

## 2. Phase 1 — AI module scaffolding

1. Create the folder `EduFlowAI.AI/Application/DocumentVerification/` (mirrors the integration guide's namespace, `EduFlowAI.AI.Application.DocumentVerification`).
2. Create `EduFlowAI.AI/Application/DocumentVerification/Messaging/` for the handler.
3. Create `EduFlowAI.AI/Infrastructure/DocumentVerification/` for the Gemini call implementation and the two reader adapters.
4. Add the missing assembly attribute — **this is the single most important step, skipping it means your handler silently never fires**:

   ```csharp
   // EduFlowAI.AI/AssemblyInfo.cs
   using Wolverine;
   using Wolverine.Attributes;

   [assembly: WolverineModule]
   ```

---

## 3. Phase 2 — Interfaces you own (`Application/DocumentVerification`)

Create these exactly as specified in the integration guide (§4–5), file `IDocumentVerificationService.cs` / `IDocumentVerificationFileReader.cs` / `IDocumentVerificationContextReader.cs`:

```csharp
namespace EduFlowAI.AI.Application.DocumentVerification;

public interface IDocumentVerificationFileReader
{
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken);
}

public sealed record DocumentVerificationContext(
    Guid ApplicationId,
    string DocumentType,
    IReadOnlyDictionary<string, string> ExpectedFields);

public interface IDocumentVerificationContextReader
{
    Task<DocumentVerificationContext> GetAsync(
        Guid applicationId, string documentType, CancellationToken cancellationToken);
}

public sealed record DocumentVerificationInput(
    Guid DocumentId,
    string DocumentType,
    string OriginalFileName,
    Stream File,
    IReadOnlyDictionary<string, string> ExpectedFields);

public sealed record DocumentVerificationResult(
    DocumentVerificationOutcomeV1 Outcome,
    DocumentVerificationDetailsV1 Details);

public interface IDocumentVerificationService
{
    Task<DocumentVerificationResult> VerifyAsync(
        DocumentVerificationInput input, CancellationToken cancellationToken);
}
```

`DocumentVerificationOutcomeV1` and `DocumentVerificationDetailsV1` are Ali's existing shared contracts — reference `EduFlowAI.Shared.Messaging.Contracts.Documents.V1`, don't redefine them.

Also add `DocumentVerificationFinalException` exactly as the integration guide's §7 specifies, in the same folder.

---

## 4. Phase 3 — File reader adapter (in `EduFlowAI.AI`, not in Documents)

Because AI already references Documents, implement the adapter yourself:

```csharp
// EduFlowAI.AI/Infrastructure/DocumentVerification/DocumentVerificationFileReader.cs
using EduFlowAI.Documents.Application.Interfaces;

namespace EduFlowAI.AI.Infrastructure.DocumentVerification;

public sealed class DocumentVerificationFileReader : IDocumentVerificationFileReader
{
    private readonly IFileStorageService _fileStorageService;

    public DocumentVerificationFileReader(IFileStorageService fileStorageService)
        => _fileStorageService = fileStorageService;

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
        => _fileStorageService.OpenReadAsync(storageKey, cancellationToken);
}
```

`IFileStorageService.OpenReadAsync` already throws `FileNotFoundException` for a missing file and validates the storage key against path traversal — map `FileNotFoundException` to `DocumentVerificationFinalException("DOCUMENT_FILE_NOT_FOUND", ...)` in the handler (§7 below), don't re-implement that check.

---

## 5. Phase 4 — Context reader adapter

**Coordinate one small contract change with Halim/Abdallah first.** `IApplicationAcessReader` (`EduFlowAI.Admission.Application.Interfaces.Services`) currently exposes `CanAccessDocumentsAsync` and `GetProgramIdAsync`, but nothing returns the applicant's `userId` for an `applicationId`. Ask for one additional read-only method:

```csharp
Task<string> GetApplicantUserIdAsync(Guid applicationId, CancellationToken cancellationToken);
```

This is a one-line, non-breaking addition to an interface Admission already owns and implements — low-friction, matches the existing pattern (`GetProgramIdAsync` does the same kind of lookup).

Then implement your reader using that plus Karim's existing `IProfileService.GetAsync`:

```csharp
// EduFlowAI.AI/Infrastructure/DocumentVerification/DocumentVerificationContextReader.cs
using EduFlowAI.Admission.Application.Interfaces.Services;
using EduFlowAI.Identity.Application.Interfaces;

namespace EduFlowAI.AI.Infrastructure.DocumentVerification;

public sealed class DocumentVerificationContextReader : IDocumentVerificationContextReader
{
    private readonly IApplicationAcessReader _applicationAccessReader;
    private readonly IProfileService _profileService;

    public DocumentVerificationContextReader(
        IApplicationAcessReader applicationAccessReader,
        IProfileService profileService)
    {
        _applicationAccessReader = applicationAccessReader;
        _profileService = profileService;
    }

    public async Task<DocumentVerificationContext> GetAsync(
        Guid applicationId, string documentType, CancellationToken cancellationToken)
    {
        var userId = await _applicationAccessReader.GetApplicantUserIdAsync(applicationId, cancellationToken);

        var profileResult = await _profileService.GetAsync(userId, cancellationToken);
        if (!profileResult.IsSuccess || profileResult.Value is null)
        {
            throw new DocumentVerificationFinalException(
                "DOCUMENT_CONTEXT_NOT_FOUND",
                "Applicant profile could not be loaded for verification.",
                attemptCount: 1);
        }

        return new DocumentVerificationContext(
            applicationId, documentType, BuildExpectedFields(documentType, profileResult.Value));
    }

    private static IReadOnlyDictionary<string, string> BuildExpectedFields(
        string documentType, Identity.Application.DTOs.ProfileResponse profile) => documentType switch
    {
        "NationalId" => Map(("FullNameAr", profile.FullNameAr), ("NationalId", profile.NationalId),
                             ("DateOfBirth", profile.DateOfBirth?.ToString("yyyy-MM-dd"))),
        "BirthCertificate" => Map(("FullNameAr", profile.FullNameAr),
                                  ("DateOfBirth", profile.DateOfBirth?.ToString("yyyy-MM-dd"))),
        "GraduationCertificate" => Map(("FullNameEn", profile.FullNameEn), ("University", profile.University),
                                       ("Faculty", profile.Faculty), ("Major", profile.Major),
                                       ("GraduationYear", profile.GraduationYear?.ToString())),
        "MilitaryCertificate" => Map(("FullNameAr", profile.FullNameAr), ("MilitaryStatus", profile.MilitaryStatus)),
        _ => throw new DocumentVerificationFinalException(
                "DOCUMENT_CONTEXT_NOT_FOUND", $"Unsupported document type '{documentType}'.", 1)
    };

    private static IReadOnlyDictionary<string, string> Map(params (string Key, string? Value)[] pairs) =>
        pairs.Where(p => !string.IsNullOrWhiteSpace(p.Value))
             .ToDictionary(p => p.Key, p => p.Value!);
}
```

This is the field mapping from decision §1.1 above — get it confirmed before you write it for real.

---

## 6. Phase 5 — The Gemini verification service

Don't build a raw HTTP client from scratch. `IGeminiChatClient` (Khorshid's, already registered as a typed `HttpClient` in `AIModule`) already has:
- `GenerateFromDocumentAsync(instruction, byte[] document, mimeType, ct)` — sends the file as `inline_data` (base64), no upload/cleanup step needed.
- `GenerateJsonAsync(systemPrompt, userMessage, responseSchema, ct)` — forces `responseMimeType: application/json` with a schema.

Neither method alone does "document + forced JSON schema" — write your own small Gemini caller in `EduFlowAI.AI/Infrastructure/DocumentVerification/DocumentVerificationGeminiClient.cs` that combines both patterns (inline_data part + a text instruction part + `responseSchema`/`responseMimeType: application/json` in `generationConfig`), using the same `HttpClient` + `GeminiOptions` (`IOptions<GeminiOptions>`) DI pattern as `GeminiChatClient`. Keep it self-contained under `DocumentVerification/` rather than editing Khorshid's file — it's a different retry contract (retry-once here vs. the model/key-rotation retry loop `GeminiChatClient` does for chat).

`DocumentVerificationService : IDocumentVerificationService` then:

1. Reads `input.File` into a `byte[]` (small ID/certificate images/PDFs — fine to buffer).
2. Builds a strict prompt: "extract exactly these fields: {expected field names}; return outcome one of ExactMatch/ValidButDifferent/MissingRequiredData/UnreadableDocument/InvalidDocumentType; respond only in the given JSON schema."
3. Calls the Gemini client — **retry exactly once** on a malformed/failed structured response (per Dev 2 task list and integration guide §8), not on every transient network error (those should bubble up so Wolverine's retry/DLQ handles them — see §8 below).
4. Validates strictly:
   - exactly one `VerificationFieldComparisonV1` per expected field (no missing, no duplicate, no extra);
   - `Fields`/`MissingFields`/`Warnings` are never null (empty list instead);
   - mask `NationalId`'s `ExtractedValue`/`ExpectedValue` (and any other sensitive value) before it ever leaves this method — e.g. `"29001011234567"` → `"290***********67"`;
   - unknown/malformed output is never coerced into `ExactMatch`.
5. Returns `DocumentVerificationResult`.

Add Arabic/English fixture-based unit tests (see §9) before wiring the handler — the parsing/validation logic is the part most worth testing in isolation.

---

## 7. Phase 6 — The handler

Copy the integration guide's §6 handler verbatim into `EduFlowAI.AI/Application/DocumentVerification/Messaging/VerifyApplicantDocumentV1Handler.cs`, using the interfaces from §3–5 above:

- `[NonTransactional]` — no EF transaction, no `IAIDbContext` injected.
- Catch `DocumentVerificationFinalException` → publish `ApplicantDocumentVerificationFailedV1`.
- Let everything else (timeouts, transient `HttpRequestException`, Gemini 429/5xx, unexpected exceptions) escape — Ali's Wolverine error policies own retry/DLQ from there (integration guide §8, don't re-implement retry/backoff at this layer beyond the single verification-service-level retry in §6 above).
- On success, publish `ApplicantDocumentVerificationCompletedV1` with the fields exactly as Ali's contract defines them.
- Use `IMessageContext messages` injected by Wolverine — no manual RabbitMQ ack, no custom consumer loop.

Map failure causes to the recommended error codes from the integration guide §7:
```
DOCUMENT_FILE_NOT_FOUND        <- FileNotFoundException from the file reader
DOCUMENT_UNSUPPORTED_FORMAT    <- Gemini rejects the mime type / can't read it
DOCUMENT_PROVIDER_UNAVAILABLE  <- only if Gemini retries are exhausted inside the service (final)
DOCUMENT_EXTRACTION_FAILED     <- Gemini returned unusable content after retry
DOCUMENT_RESPONSE_INVALID      <- malformed JSON / schema validation failed after retry
DOCUMENT_CONTEXT_NOT_FOUND     <- profile/application context missing
```

---

## 8. Phase 7 — Wire into DI

Two different processes need different pieces:

- **Worker** (`EduFlowAI.Worker`) is the only process that needs to *run* verification — it consumes `VerifyApplicantDocumentV1` off the queue. Register the full set (`IDocumentVerificationService`, `IDocumentVerificationFileReader`, `IDocumentVerificationContextReader`, the Gemini caller, and an `HttpClient` for it) inside `AIModule.AddAIWorkerModule(this IServiceCollection services)` in `EduFlowAI.AI/AIModule.cs` — that's the method `Worker/Program.cs` already calls. It currently only registers recommendation services; add yours alongside.
- **Api** doesn't need to *produce* verification results, but it does need whatever you register for the read-only query endpoints in §9 (nothing Gemini-related — those endpoints just read `ApplicantDocument`).

Double-check `Worker/Program.cs` also needs the interfaces' concrete Admission/Identity dependencies resolvable — `IApplicationAcessReader` and `IProfileService` are already registered by `AddAdmissionWorkerModule()`/Identity's DI setup respectively if the Worker already builds successfully with those modules; verify at runtime rather than assuming.

---

## 9. Phase 8 — Verification-results query endpoints (your limited full-stack slice)

These belong in `EduFlowAI.Documents/Presentation` (per `Fucken_tasks.md`'s module map: `EduFlowAI.Documents/Features/VerificationResults` is explicitly listed as yours), reading Mansy's `ApplicantDocument` — read-only, no writes, consistent with "Seif does not own `ApplicantDocument.Status`."

```
GET /api/documents/{documentId}/verification            -> Applicant owner OR OperationsManager
GET /api/documents/{documentId}/verification/attempts    -> same authorization
```

Implementation sketch:
1. Reuse the existing ownership check pattern from `DocumentSubmissionService`/the `/file` endpoint (owner or reviewing Operations Manager — check how `GET /api/documents/{docId}/file` currently authorizes, in `EduFlowAI.Documents/Presentation/Controllers`, and mirror it).
2. Load the `ApplicantDocument` by id, deserialize `VerificationDetailsJson` into `DocumentVerificationDetailsV1`, and shape a response DTO with extracted-vs-expected pairs, `OverallResult` (derived from `Status`), and `ProcessedAt` (`VerifiedAt`).
3. `/attempts` returns that same single result wrapped in a one-element array (see decision §1.2 — don't build a new table for this yet).
4. If `VerificationDetailsJson` is null (still `Verifying`, or never ran), return a clear "processing" / "not yet verified" response rather than 404 — the applicant-facing UI needs to distinguish "still working" from "not found."

---

## 10. Phase 9 — Tests

Put these in `EduFlowAI.AI.Tests` (existing project, existing conventions — see `KnowledgeBaseTestDoubles.cs` for the fake-double style already used):

- Structured-output validator: missing field, duplicate field, extra/unexpected field, all four rejected.
- Malformed JSON from Gemini → `DocumentVerificationFinalException("DOCUMENT_RESPONSE_INVALID", ...)` after exactly one retry, not zero, not more than one.
- National ID masking is applied before the result ever reaches the message (assert on the returned `DocumentVerificationDetailsV1`, not on an internal Gemini response type).
- One Arabic-language fixture and one English-language fixture per document type, at minimum for `NationalId` and one certificate type.
- Handler-level test: transient exception from the context/file reader propagates unchanged (not swallowed into a failed event) — this is the one behavior most likely to regress by an over-eager `try/catch`.

---

## 11. Phase 10 — Local end-to-end verification

1. Set `Messaging:Enabled: true` and fill in `ConnectionStrings:DefaultConnection` + `ConnectionStrings:RabbitMq` in `appsettings.Development.json` (currently `Messaging:Enabled` is `false` in the checked-in `appsettings.json`, which stubs out RabbitMQ entirely — you need it on to see your handler actually fire).
2. `docker compose -f EduFlowAI.Api/docker-compose.yml up -d` to get RabbitMQ running (management UI on `:15672`, user/pass `eduflowai`/`eduflowai`), or fold this into the root compose file from §12.
3. Run both `EduFlowAI.Api` and `EduFlowAI.Worker`.
4. Upload a document as an applicant, submit the package, watch the RabbitMQ management UI for messages flowing through `eduflow.documents.verification.v1` → `eduflow.documents.apply-verification-result.v1` (or `-failure`), and confirm `ApplicantDocument.Status` flips to `Approved` or `NeedsHumanReview`.
5. Hit your two new query endpoints and confirm the extracted-vs-expected payload looks right for both a passing and a failing case.

---

## 12. Deployment (root-level ownership — separate workstream, can run in parallel with AI work)

Confirmed missing: no Dockerfiles, no real root compose, no CI. This is squarely yours per both distribution docs.

1. **`EduFlowAI.Api/Dockerfile`, `EduFlowAI.Worker/Dockerfile`** — standard multi-stage .NET 10 SDK build → ASP.NET/runtime image. Watch for the shared `SecureStorage` folder Mansy's `FileStorageService` writes to (`AppContext.BaseDirectory/SecureStorage`) — Api and Worker must see the **same** volume/mount, or verification will fail to find files the Api saved. Call this out explicitly in your Dockerfiles/compose (a named volume mounted into both containers at the same path).
2. **Root `docker-compose.yml`** — supersede the stray `EduFlowAI.Api/docker-compose.yml`. Services: `postgres` (with pgvector-capable image, since Khorshid's knowledge base needs `vector(1536)`), `rabbitmq` (reuse the existing config), `api`, `worker`, and the Angular app once it exists. Wire `DefaultConnection`, `RabbitMq`, and the shared storage volume through environment variables / mounts.
3. **CI** (`.github/workflows/`) — build + test both this backend (`dotnet test` across all `.Tests` projects already in the repo) and the Angular frontend once it exists. Gate merges on it.
4. **Migration deploy step** — an `dotnet ef database update` (or equivalent) run as a one-shot container/init step before Api/Worker start, against the shared `EduFlowAIDbContext`.
5. **Readiness/liveness** — add `/health` endpoints (or ASP.NET Core health checks) hit by the orchestrator; Worker needs a liveness signal too (e.g. a simple `HostedService` heartbeat or Wolverine's own health reporting).
6. **Secrets/env mapping doc + runbook** — `Gemini:ApiKey(s)`, `Jwt:SigningKey`, `Smtp:*`, `ConnectionStrings:*` are all blank/placeholder in the checked-in `appsettings.json` — document where each comes from in staging/prod (env vars, secret manager) without ever committing real values.
7. **Staging environment** — stand up the compose stack (or cloud equivalent) and verify Api → Worker → RabbitMQ connectivity end-to-end once, with a real submitted document, before calling this done.

You do **not** own `Program.cs` composition in either host (Karim owns Api's, Ali owns Worker's) — your Dockerfiles/compose/CI wrap those processes, they don't modify their startup code.

---

## 13. Final checklist

From the integration guide, plus the query-endpoint and deployment work layered on top:

- [ ] `[assembly: WolverineModule]` added to `EduFlowAI.AI`.
- [ ] `IDocumentVerificationFileReader`, `IDocumentVerificationContextReader`, `IDocumentVerificationService` defined and implemented (implementation lives in AI itself, per the corrected dependency direction in §0).
- [ ] One new read-only method added to Admission's `IApplicationAcessReader` (coordinated with Halim/Abdallah).
- [ ] Expected-field mapping per `DocumentType` confirmed with the team, not invented silently.
- [ ] `VerifyApplicantDocumentV1Handler` implemented, `[NonTransactional]`, no DB writes, publishes existing contracts unchanged.
- [ ] Sensitive values masked before any message leaves the handler.
- [ ] Retry-once inside the verification service only; transient/unexpected exceptions escape for Wolverine.
- [ ] Registered in `AIModule.AddAIWorkerModule`.
- [ ] `GET /api/documents/{id}/verification` and `/attempts` built read-only against `ApplicantDocument`, no new table added without a team decision.
- [ ] Arabic/English fixtures + validator + masking + retry tests passing in `EduFlowAI.AI.Tests`.
- [ ] End-to-end run confirmed locally with RabbitMQ enabled.
- [ ] Dockerfiles (Api, Worker), root compose, CI, migration step, health checks, secrets doc, staging verification.
- [ ] Did **not** touch `ApplicantDocument.Status`, `StorageKey`, Human Review, Replacement Requests, RabbitMQ/Outbox/Inbox configuration, or either host's `Program.cs`.
