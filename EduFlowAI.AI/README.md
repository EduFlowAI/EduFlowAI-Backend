# EduFlowAI.AI — Knowledge & Intent Service

Owner: **Abdelrhman Saleem**

The conversational part of the platform. One chat endpoint takes a free-text message, works out what
the applicant actually wants, and routes it to one of three places:

- **knowledge** → answered from the indexed ITI documents (RAG)
- **status** → the Applications module's status agent
- **recommendation** → the Agents module's track recommendation service

It also owns the knowledge base itself: uploading documents, transcribing them, chunking, embedding
and storing the vectors.

Work in progress — this branch is not ready to merge yet, see [What's left](#whats-left).

---

## What works today

Verified end to end against a local PostgreSQL + pgvector database using a real ITI track PDF.
94 unit tests pass.

- Upload a **PDF, .txt or .md**, or paste text straight in. The file is stored, transcribed,
  chunked and embedded, and the document ends up `Indexed`.
- Ask a question in **English or Arabic** and get an answer grounded in the uploaded documents,
  in the same language, with the source documents returned alongside it.
- Intent routing is deterministic — the same question always routes the same way.
- Chat sessions with history, idle expiry and a per-session turn cap.
- Knowledge base management: list, delete, and a full re-sync that rebuilds every embedding.
- Long digit runs (National ID) are masked before any text leaves for the model.

## Endpoints

Chat is public by design — guests can use the assistant. The knowledge base endpoints are meant to
be SuperAdmin-only once auth exists.

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/ai/chat/session/create` | Start a session |
| POST | `/api/ai/chat/ask?sessionId=` | Ask a question |
| GET | `/api/ai/chat/session/{id}/history` | Turns so far |
| POST | `/api/ai/chat/session/{id}/end` | End a session |
| POST | `/api/ai/chat/classify` | Diagnostic — shows routing without answering |
| POST | `/api/ai/knowledge-base/upload` | Upload a PDF / .txt / .md |
| POST | `/api/ai/knowledge-base/text` | Index pasted text |
| GET | `/api/ai/knowledge-base/documents` | List documents |
| GET | `/api/ai/knowledge-base/{id}/status` | Status + chunk count |
| DELETE | `/api/ai/knowledge-base/{id}` | Delete document, chunks and file |
| POST | `/api/ai/knowledge-base/sync` | Rebuild all embeddings |

`ask` returns `answer`, `intent`, `confidence`, `requiresClarification`, `routedTo`, `language`
and `sources`. When confidence is below the configured threshold it returns a clarifying question
instead of guessing, and it returns 503 while a re-sync is running.

## Layout

```
Application/
  DbContextAbstraction/IAIDbContext.cs   module's slice of the shared DbContext
  Interfaces/                            contracts (repository, storage, agents, extractor, chat client)
  Services/
    AiChatService.cs                     builds the grounded answer + the ITI assistant prompt
    IntentClassifierService.cs           routing prompt, schema and parsing
    KnowledgeBaseService.cs              ingest, re-sync, delete
    KnowledgeBaseRetrievalService.cs     embeds the query, cosine search
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
Presentation/Controllers/                AiChatController, KnowledgeBaseController
```

Tables owned here: `ai.KnowledgeBaseDocument`, `ai.KnowledgeBaseChunk` (`vector(1536)`, exact cosine
search, no ANN index).

## Running it

1. PostgreSQL with the `vector` extension, database `EduFlowAIDb`.
2. Put the connection string and the Gemini key in `EduFlowAI.Api/appsettings.Development.json`
   (git-ignored — never commit keys).
3. `dotnet ef database update --project EduFlowAI.Persistence --startup-project EduFlowAI.Api`
4. `dotnet run --project EduFlowAI.Api` → `http://localhost:5232`, Swagger at `/swagger`
5. `dotnet test EduFlowAI.AI.Tests`

Config worth knowing: `Gemini:ChatModel`, `Gemini:EmbeddingModel`, `Gemini:EmbeddingDimensions`,
`IntentClassification:MinConfidence`, `ChatSession:IdleTimeout`, `Ingestion:UseAiExtraction`.

---

## What's left

**Models.** Everything currently runs on Gemini (`gemini-3.1-flash-lite` for chat and
transcription, `gemini-embedding-001` for embeddings). These were picked because they work on a
free key — `gemini-2.5-flash` returns 404 for newly issued keys. The models still need to be
reviewed and probably changed before the demo, and the embedding width (1536) should be revisited
at the same time since changing it means a migration plus a full re-sync.

**Hosted database.** All of this has only been applied to the local database. The hosted (Neon)
database still needs the `vector(1536)` migration and then a **full re-sync**, because both the
vector width and the chunk text have changed. To be done at the end, together with whoever owns
the database.

**Authorization — needs Karim.** `IdentityModule` currently registers `AddIdentityCore` only, with
no JWT bearer scheme, so `[Authorize]` cannot work yet. Once it exists: lock the knowledge base
endpoints to SuperAdmin, take the user id from the token instead of the request body, and require
login for status and recommendation questions. `UploadedByUserId` is a placeholder until then.

**Status agent — needs Halim.** `IStatusAgent` is a placeholder returning a fixed message. The
Applications module should implement it and swap the registration in `AIModule.cs`; no controller
change needed.

**Recommendations agent.** `IRecommendationsAgent` is a placeholder too, waiting on the real track
recommendation service.

**Frontend contract — needs Kamel.** The assistant and knowledge base screens consume the endpoints
above. Two things to agree: whether the response shape is what the UI expects, and whether answers
should show source citations (`sources` is already returned, currently unused).

**Also open.** More documents need to be indexed — the knowledge base currently only has one track
brochure, so questions about other tracks correctly return "I don't have that". Arabic answers could
use tuning for Egyptian Arabic. Retrieval quality has not been measured with a proper question set.
