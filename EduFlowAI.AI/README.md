# EduFlowAI.AI — Knowledge & Intent Service

Owner: **Abdelrhman Saleem** (RAG, Knowledge Indexing, Intent Router)

This is the AI engine behind the assistant. It has **no endpoints of its own** — per the task
distribution, Kamel owns the presentation layer and calls into the services below.

Given a free-text message it works out what the applicant wants (`knowledge` / `status` /
`recommendation`) and, for knowledge questions, produces an answer grounded in the indexed ITI
documents. It also owns the knowledge base: storing uploads, transcribing, chunking, embedding and
searching the vectors.

Work in progress — this branch is not ready to merge, see [What's left](#whats-left).

---

## Services provided to Kamel

These five interfaces are the module's public surface. All live in `Application/Interfaces/` and are
registered by `services.AddAIModule()`.

| Interface | What it does |
|---|---|
| `IIntentRouter` | `ClassifyAsync(question, context)` → intent, confidence, `requiresClarification` |
| `IRagAnswerService` | `AnswerWithContextAsync(question, language)` → grounded answer + `sources` |
| `IKnowledgeIndexingService` | Upload / paste text / list / status / delete / full re-sync |
| `IKnowledgeRetrievalService` | `RetrieveContextAsync(query, limit)` → nearest chunks with their source |
| `IEmbeddingService` | `GenerateEmbeddingAsync(text)` → vector of `Gemini:EmbeddingDimensions` |

`ISessionManager` is also registered — conversation context is on my task list — so Kamel's assistant
endpoint can keep per-session history without building its own store.

Ingestion failures are typed so the endpoint can map them without inspecting messages:

| Exception | Suggested status |
|---|---|
| `KnowledgeBaseValidationException` | 400 |
| `KnowledgeBaseTooLargeException` | 413 |
| `KnowledgeBaseBusyException` | 409 (a re-sync is running) |

Integration notes for Kamel, Halim and Karim are shared directly rather than through the repo — ask
me for them.

## What works today

Verified end to end against a local PostgreSQL + pgvector database using a real ITI track PDF.
101 unit tests pass.

- A **PDF, .txt or .md** file — or pasted text — is validated, stored, transcribed, chunked,
  embedded and ends up `Indexed`.
- Questions in **English or Arabic** get an answer grounded in the uploaded documents, in the same
  language, with the source documents returned alongside.
- Intent routing is deterministic — the same question always routes the same way — and asks the user
  to clarify instead of guessing when confidence is below the configured threshold.
- **Multi-intent:** *"What is my application status, and which track should I choose?"* returns both
  intents, ranked most likely first, so the caller can dispatch each and combine the replies.
  `PrimaryIntent` stays the top-ranked entry, so single-intent callers are unaffected.
- Knowledge base management: list, delete, and a full re-sync that rebuilds every embedding from the
  stored originals.
- Long digit runs (National ID) are masked before any text leaves for the model.

## Layout

```
Application/
  DbContextAbstraction/IAIDbContext.cs   module's slice of the shared DbContext
  Exceptions/                            typed ingestion failures for the caller to map
  Interfaces/                            the five services above + repository, storage, extractor
  Services/
    AiChatService.cs                     IRagAnswerService — grounded answer + the ITI assistant prompt
    IntentClassifierService.cs           IIntentRouter — routing prompt, schema and parsing
    KnowledgeBaseService.cs              IKnowledgeIndexingService — validate, ingest, re-sync, delete
    KnowledgeBaseRetrievalService.cs     IKnowledgeRetrievalService — embeds the query, cosine search
    LanguageDetectionService.cs          ar / en
    InputSanitizerService.cs             masks long digit runs
Infrastructure/
  ExternalServices/GeminiChatClient.cs   single generateContent client (text, JSON mode, documents)
  ExternalServices/GeminiEmbeddingService.cs
  Processing/DocumentTextExtractor.cs    AI transcription, falls back to iText
  Processing/TextChunker.cs              sentence-aware chunking
  Persistence/KnowledgeRepository.cs
  Storage/LocalFileStorageService.cs
  Services/InMemorySessionManager.cs
  Configurations/                        EF mappings for the two KB tables
```

Tables owned here: `ai.KnowledgeBaseDocument`, `ai.KnowledgeBaseChunk` (`vector(1536)`, exact cosine
search, no ANN index).

## Running it

1. PostgreSQL with the `vector` extension, database `EduFlowAIDb`.
2. Put the connection string and the Gemini key in `EduFlowAI.Api/appsettings.Development.json`
   (git-ignored — never commit keys).
3. `dotnet ef database update --project EduFlowAI.Persistence --startup-project EduFlowAI.Api`
4. `dotnet test EduFlowAI.AI.Tests`

Config worth knowing: `Gemini:ChatModel`, `Gemini:EmbeddingModel`, `Gemini:EmbeddingDimensions`,
`IntentClassification:MinConfidence`, `ChatSession:IdleTimeout`, `Ingestion:UseAiExtraction`,
`Ingestion:MaxUploadBytes`.

---

## What's left

**Evaluation datasets.** RAG and router evaluation sets are on my task list and don't exist yet.
Routing has been checked against a hand-written golden set, but retrieval quality has not been
measured.

**Models.** Everything runs on Gemini (`gemini-3.1-flash-lite` for chat and transcription,
`gemini-embedding-001` for embeddings). These were picked because they work on a free key —
`gemini-2.5-flash` returns 404 for newly issued keys. They still need review before the demo, along
with the embedding width (1536), since changing it means a migration plus a full re-sync.

**Hosted database.** Only the local database has been migrated. The hosted (Neon) database still
needs the `vector(1536)` migration and then a **full re-sync**, because both the vector width and the
chunk text have changed. To be done at the end with whoever owns the database.

**Endpoints — Kamel.** Nothing in this module is reachable over HTTP until the assistant and
knowledge-base endpoints exist. Integration notes sent separately.

**Uploader identity — Karim.** `UploadedByUserId` is the placeholder `"system-seed"` until there is
a JWT scheme to read a real user id from.

**Content.** Only one track brochure is indexed, so questions about other tracks correctly return
"I don't have that". Arabic answers could also use tuning for Egyptian Arabic.
