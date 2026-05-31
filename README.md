# AI Chat Assistant

Academic .NET 8 RAG chat assistant with a standalone API and MVC front end.

## API quick start

Project: `src/AI.Chat.Assistant.API`

### Prerequisites

- .NET 8 SDK
- SQL Server with migrations applied (`AiAssistantDb`)
- Pinecone and Azure Blob settings in `appsettings.json` (or overrides in `appsettings.Development.json`)
- **Groq API key** — create one at [Groq Console](https://console.groq.com), then set `LLM:Groq:ApiKey` in `appsettings.Development.json` (replace `YOUR_GROQ_API_KEY_HERE`)
- **ONNX embedding files** — see [models/README.md](src/AI.Chat.Assistant.API/models/README.md)

### Run the API

```bash
dotnet run --project src/AI.Chat.Assistant.API/AIChatAssistant.API.csproj
```

Swagger UI (Development): `https://localhost:7167/swagger`

### Authentication

All endpoints except `/swagger` and `/api/diag` (Development only) require header:

```
X-Api-Key: secret-key
```

(`ApplicationSettings:ApiKey` in `appsettings.Development.json`)

JWT is configured for future MVC integration (`Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience`).

### Test chat flow

1. Ensure a user exists in `AspNetUsers` (e.g. `Id = 1` from MVC registration or manual insert).
2. **Create session** — `POST /api/ai-chat/sessions`
3. **Send message** — `POST /api/ai-chat/messages` with `sessionId` from step 2.
4. Optional: upload knowledge via `POST /api/knowledge/upload-text` or `upload-file`.

Example (PowerShell; adjust port and `userId`):

```powershell
$base = "https://localhost:7167"
$key = "secret-key"
$headers = @{ "X-Api-Key" = $key }

# Diagnostics (no API key in Development)
Invoke-RestMethod "$base/api/diag"

# Create session
$session = Invoke-RestMethod "$base/api/ai-chat/sessions" -Method POST -Headers $headers `
  -ContentType "application/json" -Body '{"userId":1}'
$sessionId = $session.sessionId

# Chat
Invoke-RestMethod "$base/api/ai-chat/messages" -Method POST -Headers $headers `
  -ContentType "application/json" `
  -Body (@{ sessionId = $sessionId; message = "Hello" } | ConvertTo-Json)
```

```bash
# curl examples (use -k if using dev HTTPS with self-signed cert)
curl -k https://localhost:7167/api/diag

curl -k -X POST https://localhost:7167/api/ai-chat/sessions \
  -H "X-Api-Key: secret-key" -H "Content-Type: application/json" \
  -d '{"userId":1}'

curl -k -X POST https://localhost:7167/api/ai-chat/messages \
  -H "X-Api-Key: secret-key" -H "Content-Type: application/json" \
  -d '{"sessionId":"<guid-from-create>","message":"Hello"}'
```

## Build

```bash
dotnet build AI.Chat.Assistant.sln
```

## Database migrations (EF Core)

Use **local/dev** SQL only. Do not run `Update-Database` or `dotnet ef database update` against production.

| PMC / CLI role | Project |
|----------------|---------|
| **Default project** (migrations live here) | `AI.Chat.Assistant.Infrastructure` |
| **Startup project** (host + `appsettings`) | `AI.Chat.Assistant.API` — not Infrastructure |

Connection string is read from `ApplicationSettings:ConnectionString` in `src/AI.Chat.Assistant.API/appsettings.Development.json` via `IDesignTimeDbContextFactory` (with a localhost dev fallback if the file is missing).

### Package Manager Console (Visual Studio)

1. **Tools → NuGet Package Manager → Package Manager Console**
2. Set **Default project**: `AI.Chat.Assistant.Infrastructure`
3. Set **Startup project** (Solution Explorer → right-click): `AI.Chat.Assistant.API`
4. From the solution directory (`AI.Chat.Assistant`):

```powershell
Update-Database
```

Add a migration:

```powershell
Add-Migration YourMigrationName
```

### .NET CLI (from repo root)

```bash
dotnet ef database update ^
  --project src/AI.Chat.Assistant.Infrastructure/AIChatAssistant.Infrastructure.csproj ^
  --startup-project src/AI.Chat.Assistant.API/AIChatAssistant.API.csproj
```

```bash
dotnet ef migrations add YourMigrationName ^
  --project src/AI.Chat.Assistant.Infrastructure/AIChatAssistant.Infrastructure.csproj ^
  --startup-project src/AI.Chat.Assistant.API/AIChatAssistant.API.csproj
```

On Linux/macOS, replace `^` with `\`.

## MVC web UI

Project: `src/AIChatAssistant.MVC`

Runs the same in-process services as the API (SQL, Pinecone, Groq, ONNX embeddings). ONNX files are linked from `AI.Chat.Assistant.API/models/` at build time.

### Run MVC

```bash
dotnet run --project src/AIChatAssistant.MVC/AIChatAssistant.MVC.csproj
```

Open `https://localhost:7001` (see `launchSettings.json`).

### End-to-end test flow

1. **Register** — `/Home/Register` → creates user with role `User`
2. **Login** — `/Home/Login` → stores JWT in `localStorage`
3. **Chat** — blue chat button (bottom-right) sends `Authorization: Bearer` to `POST /api/chat/send`
4. **Admin** (optional):
   - Promote a user in Development:
     ```bash
     curl -k -X POST https://localhost:7001/api/auth/assign-admin \
       -H "Content-Type: application/json" \
       -d '{"email":"you@example.com"}'
     ```
   - Re-login to refresh JWT with `Admin` role
   - Open **RAG settings** (admin modal):
     - **Upload to storage** — file goes to Azure Blob; DB record is `Pending` (not vectorized yet)
     - **Process for RAG** — parse, chunk, embed, upsert to Pinecone
     - **Delete related data** — removes Pinecone vectors and DB chunks/RAG metadata; keeps Azure blob and source row (`Pending`); use **Process for RAG** to re-index
     - **Delete all** — removes Pinecone vectors, DB source/chunks, and blob
   - Apply migration if needed — see [Database migrations (EF Core)](#database-migrations-ef-core)

### Admin knowledge endpoints (MVC, JWT Admin role)

| Method | Path | Description |
|--------|------|-------------|
| GET | `/KnowledgeBase/Sources` | List file sources (optional `?userId=`) |
| POST | `/KnowledgeBase/Upload` | Upload to blob, status `Pending` |
| POST | `/KnowledgeBase/process/{sourceId}` | Run RAG pipeline |
| DELETE | `/KnowledgeBase/DeleteRelatedData/{sourceId}` | RAG-only delete (vectors + chunks; blob kept) |
| DELETE | `/KnowledgeBase/source/{sourceId}?deleteBlob=true` | Full delete (vectors + DB + blob) |
| DELETE | `/KnowledgeBase/source/{sourceId}?deleteBlob=false` | Same as DeleteRelatedData |

API mirror (requires `X-Api-Key`): `DELETE /api/knowledge/source/{sourceId}?deleteBlob=true|false`.

### Config

`appsettings.Development.json` must include `ApplicationSettings:ConnectionString`, `LLM:Groq`, `PineconeSettings`, `AzureBlobStorage`, and `AI` model paths (same as API).

JWT for MVC uses `JwtSettings` (`Secret`, `Issuer`, `Audience`) — separate from API `Jwt:Key`.

## Azure Blob Storage (knowledge file uploads)

Admin **Upload to storage** and API `POST /api/knowledge/upload-file` store files in Azure Blob Storage via `AzureBlobStorage` in configuration.

### Setup

1. **Create a storage account** in [Azure Portal](https://portal.azure.com) (e.g. `contextfilesllm`).
2. **Create a blob container** (private access is fine). Note the name exactly — e.g. `ai-data`. The app does not require `ai-sources`; use whatever container you create.
3. **Copy the connection string**: Storage account → **Access keys** → **key1** → **Connection string** (show).
4. **Configure both hosts** (MVC and API use the same keys when run separately):

| File | Section |
|------|---------|
| `src/AIChatAssistant.MVC/appsettings.Development.json` | `AzureBlobStorage` |
| `src/AI.Chat.Assistant.API/appsettings.Development.json` | `AzureBlobStorage` |

Example (replace `YOUR_ACCOUNT_KEY` with the key from the portal):

```json
"AzureBlobStorage": {
  "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=contextfilesllm;AccountKey=YOUR_ACCOUNT_KEY;EndpointSuffix=core.windows.net",
  "ContainerName": "ai-data"
}
```

### Requirements

- **Connection string format** must be the standard Azure Storage connection string from Access keys (compatible with `Azure.Storage.Blobs`). Do not use SAS-only strings here.
- **`ContainerName` must match exactly** the container in the portal (case-sensitive). If your container is `ai-data`, set `"ContainerName": "ai-data"`. You do not need to create `ai-sources` unless you choose that name in config.
- **MVC and API** should use the same `ContainerName` and storage account when both are used for uploads.
- If upload fails, the admin UI shows a message for common cases (container not found, auth failed). Check logs for full `RequestFailedException` details.

Production: set `AzureBlobStorage` via environment variables or secure secrets (`AzureBlobStorage__ConnectionString`, `AzureBlobStorage__ContainerName`), not committed keys.

## Custom admin system prompt

Admins configure the assistant’s **system prompt** in the MVC admin modal (**RAG settings → System prompt → Save prompt**). That text is **not** part of the knowledge base and is **not** sent to Pinecone.

### Before (old way)

- `SettingsController` saved the prompt via `DocumentProcessingService.ProcessTextAsync`, treating it as a `TextKnowledgeSource` document.
- The pipeline chunked the prompt, embedded it, and stored vectors in Pinecone.
- `GetCustomSystemPromptAsync` read the latest `TextKnowledgeSource` content.
- **Problems:** the prompt competed with real documents in vector search; prompt text could surface as RAG “context”; every edit re-indexed vectors; configuration was mixed with searchable knowledge.

### Now (new way)

| Concern | Where it lives |
|--------|----------------|
| Admin system prompt | SQL table `UserChatSettings` (`CustomSystemPrompt`, keyed by admin `UserId`) |
| RAG / document chunks | Pinecone + `KnowledgeSources` / `ContentChunks` |

- **Save:** `POST /Settings/UpdatePrompt` → `IChatRepository.SaveCustomSystemPromptAsync(adminUserId, prompt)` (upsert one row per admin).
- **Chat:** `AiChatService` calls `GetCustomSystemPromptAsync()` → reads the **most recently updated** non-empty prompt from `UserChatSettings` (any admin who saved). If the table is missing or empty, chat uses the built-in default prompt and does not crash.
- **LLM:** prompt is injected as the first `System` message; RAG hits are added in a separate `System` message (`Provided context: …`).

**Why SQL, not vectors:** the prompt is instruction/configuration, not retrievable knowledge. DB read/update is fast and keeps Pinecone retrieval limited to real uploaded documents.

**Reverting to the old approach** is possible (store prompt as `TextKnowledgeSource` again) but not recommended: prompts would re-enter similarity search, add indexing cost on every edit, and blur the line between “how to answer” and “what to cite.”

### Orphan rows in `TextKnowledgeSources` (manual cleanup)

If you saved the system prompt **before** this fix, you may still have a duplicate row in `TextKnowledgeSources` / `KnowledgeSources` (for example `Id = 2`) with the same text as `UserChatSettings`. New saves **do not** create these rows. The app does not auto-delete them (to avoid removing real text uploads by mistake).

After you confirm the row is only the old prompt (not a real knowledge document), remove it in SQL Server:

```sql
-- Replace @id with the orphan TextKnowledgeSource / KnowledgeSources Id
DECLARE @id INT = 2;

DELETE FROM ContentChunks WHERE SourceFileId = @id;
DELETE FROM TextKnowledgeSources WHERE Id = @id;
DELETE FROM KnowledgeSources WHERE Id = @id;
```

If that source was ever fully indexed, also delete its vectors in Pinecone (admin UI **Delete all** on a file row, or your index cleanup tool).

### Apply migration

Migration file: `20260522222854_AddUserChatSettingsConfig.cs` (class `AddUserChatSettingsConfig`).

**Package Manager Console** (Default project: `AI.Chat.Assistant.Infrastructure`, Startup: `AI.Chat.Assistant.API` or `AIChatAssistant.MVC`):

```powershell
Update-Database
```

To apply only this migration on an existing database:

```powershell
Update-Database -Migration AddUserChatSettingsConfig
```

**CLI:**

```bash
dotnet ef database update --project src/AI.Chat.Assistant.Infrastructure/AIChatAssistant.Infrastructure.csproj --startup-project src/AI.Chat.Assistant.API/AIChatAssistant.API.csproj
```
