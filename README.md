# CV Management System

Blazor Server (.NET 10) + EF Core + PostgreSQL. Attribute values live in a normalized EAV
table and are resolved live — a CV stores no content of its own.

## Stack

| Layer | Choice |
|---|---|
| Language / framework | C# / Blazor Server (Interactive Server) |
| Database | PostgreSQL via Npgsql |
| ORM | Entity Framework Core (migrations in `Data/Migrations`) |
| CSS | Bootstrap 5 |
| Markdown | Markdig (render) + EasyMDE (edit) |
| Tag input | Tagify (with autocomplete from existing tags) |
| Real-time | SignalR (`/hubs/discussion`) |
| Auth | ASP.NET Core Identity, Google + GitHub social login |

## Run locally

```bash
dotnet run --project CvManagement            # applies migrations + seed, then serves
dotnet run --project CvManagement -- --selfcheck   # in-memory access-rule checks, no DB needed
```

### Sample content

Against an **empty** database in Development, a demo set is created so the app is not three
empty tables. It never runs against a database that already has positions.

| Account | Password | Role |
|---|---|---|
| `recruiter@demo.local` | `Demo1234!` | Recruiter |
| `candidate@demo.local` | `Demo1234!` | Candidate |

Included: 4 built-in attributes plus `IELTS Score`, `Remote Work Available`,
`Presentation Skills` and a Markdown `Professional Summary`; 3 projects; 3 positions (two
public, one Restricted by `IELTS Score > 6.5`); a published CV with a like and a draft CV;
and 3 discussion posts. Sign in as the recruiter to see the CV list, likes and search; as the
candidate to see the profile, projects and CV editing.

Set `Seed:DemoData` to `true` to enable it outside Development, or `false` to disable it:

```json
{ "Seed": { "DemoData": false } }
```

Configuration lives in `appsettings.json` (or environment variables). Only the connection
string is required to boot:

```json
{
  "ConnectionStrings": { "DefaultConnection": "Host=localhost;Database=cvmanagement;Username=postgres;Password=..." }
}
```

### Optional integrations

| Setting | Purpose |
|---|---|
| `Authentication:Google:ClientId` / `:ClientSecret` | Google sign-in. Buttons stay hidden-safe when unset. |
| `Authentication:GitHub:ClientId` / `:ClientSecret` | GitHub sign-in. |
| `Cloudinary:CloudName` / `:UploadPreset` | Drag-and-drop image upload. The preset must be an **unsigned** upload preset, because the browser posts the file straight to Cloudinary — see below. |
| `Seed:AdminEmail` / `Seed:AdminPassword` | Creates the first administrator on startup. No default account ships. |
| `Seed:DemoData` | Sample content. Defaults to on in Development, off otherwise. |

`Cloudinary:ApiKey` / `:ApiSecret` are present for completeness but are not used by the
upload path: an unsigned preset means the browser uploads directly to Cloudinary and only
the resulting URL is stored.

## Architecture notes

- **Optimistic locking** — every editable row carries a row version. Saves send the version
  they read; a mismatch raises `ConcurrencyConflictException` and the UI offers reload +
  retry. Covering profile auto-save, positions, attributes and projects.
- **No CV snapshots** — `CvRecord` holds technical fields only. Attribute values are read
  from `ProfileAttributeValues` (one value per candidate per attribute) and projects are
  filtered by the position's tags, capped at `MaxProjects`.
- **Images** — uploaded by the browser directly to Cloudinary; the server and database only
  ever see the URL (`wwwroot/js/image-upload.js`, `Components/Shared/ImageUpload.razor`).
- **Full-text search** — PostgreSQL `tsvector` columns maintained by DB triggers, including
  one over `ProfileAttributeValues` so CV *content* is searchable.
- **Deletion** — configured as database-level cascade; no manual row-by-row deletion loops.
- **Tag writes** — diffed and saved in one round-trip (`PositionService.SetTagsAsync`).

## Deployment

`Dockerfile` + `render.yaml` (Render web service + managed PostgreSQL). Set the optional
keys above as environment variables; `Cloudinary__UploadPreset` is required for image upload.
