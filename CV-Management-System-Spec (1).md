# CV Management System — Requirements Specification
**Track:** .NET (C# required, Blazor or MVC — implementer's choice), PostgreSQL
**Deadline:** submission to HR by 23.09.2026
**Source:** course brief + instructor clarifications (Discord messages, dated)

> This document restates the brief in structured form for reference during implementation. Nothing is added or assumed beyond what was stated by the instructor. Where the instructor said "nice to have" or "not crucial," that is preserved. Additional requirements are expected later (up to 3 third-party integrations) — do not build generic "store"/"blog" features preemptively.

---

## 1. Technology Constraints

| Layer | Requirement |
|---|---|
| Language | C# (required) |
| Web framework | Blazor **or** MVC — free choice |
| Client scripting | JavaScript/TypeScript as needed |
| Database | **PostgreSQL (chosen)**. Brief allowed SQL Server/MySQL/PostgreSQL; PostgreSQL selected per instructor's preference note — free hosting (e.g., Render) is more commonly available for it than for MySQL, and switching later via the ORM would be easy anyway if ever needed. |
| ORM | **Required** (e.g., Entity Framework). No raw ad-hoc table generation, no per-CV JSON blobs for structured data. |
| CSS framework | Required; Bootstrap recommended, any allowed |
| Full-text search | Required — external library or native DB feature |
| Architecture | No constraints — no forced microservices, no forced separate app/web server split |
| Other libraries | Freely allowed, "yes to all" per instructor, as long as they don't replace the mandated stack (C#, chosen web framework, relational DB) |

### Hard "Don'ts"
- No `SELECT *` full table scans.
- No uploading images to your own web server or database — use an external cloud storage / drag-and-drop uploader component.
- No DB queries inside loops.
- No action buttons inside table rows (see §2 UI rules).
- No storing CV content as JSON blobs — attributes live in relational tables, referenced, not duplicated.
- No generating DB tables dynamically at runtime.
- No tile/gallery views for Positions or CVs — table views only.

---

## 2. UI/UX Rules (Cross-Cutting)

- **No per-row action buttons** (View/Edit/Delete repeated in every row). Penalty: **-20%**.
  - Acceptable pattern: toolbar-based actions, selection (checkboxes) + toolbar, or contextual "appearing" actions (e.g., hover/selection reveals a single action area outside the repeated row structure).
- **No tile/gallery card layouts** for Positions or CVs. Penalty: **-20%**. Must be **table views**.
- **Full-text search accessible from the top header on every page.**
- **Consistent, convenient navigation** across the whole app.
- **Responsive design** — must work on mobile screen sizes.
- **Two UI languages**: English + one other (e.g., Polish/Spanish/Uzbek/Georgian). User selects language; choice is persisted. Only UI strings are translated — user-generated content (names, descriptions, projects) is never translated.
- **Two visual themes**: light and dark. User-selected, persisted.

---

## 3. Authentication & Authorization

### 3.1 Anonymous (non-authenticated) users may:
- Register an account
- Sign in
- Browse available positions **read-only**
- View public statistics (e.g., "10 new CVs in last 24h")

### 3.2 Anonymous users may NOT:
- Create/edit positions
- Browse or create CVs
- Comment or like CVs
- Access any personal pages

### 3.3 Social login
- Must support **at least two** social login providers (e.g., Google + Facebook, or others of choice).

### 3.4 Roles
Three roles: **Candidate**, **Recruiter**, **Administrator**. A user can potentially hold multiple roles (admins act as recruiter+candidate+admin; see §3.7).

### 3.5 Candidate permissions
- Manage own personal profile (Me / Info / Projects / CVs sections — see §5)
- Select & fill attributes from the shared Attribute Library (their own values only)
- Manage own project descriptions
- View positions they are allowed to access
- Create/edit CVs for positions they can access; **at most one CV per position**
- Participate in discussions (post to position discussion tabs)
- **Cannot** like CVs
- Access restricted to **own** profile and **own** CVs only

### 3.6 Recruiter permissions
Recruiters share responsibility for one common pool of positions and one common pool of attributes — **no ownership concept**. Any Recruiter can:
- Create positions
- Duplicate existing positions
- Edit any position
- Delete any position
- Configure access rules for positions
- Manage position templates (attribute selection per position)
- Manage the shared Attribute Library (create/edit/delete attributes)
- View candidate CVs **read-only** — via full-text search, via the position's CV list, or via a candidate's personal page
- Participate in discussions
- **Like** CVs (see §9)
- **Cannot** edit candidate profiles or CVs directly

### 3.7 Administrator permissions
- Unrestricted access to everything
- Can view **all** pages as the owner would (open any candidate page and edit as if they were that candidate: fix typos, edit attributes, etc.)
- Can edit any candidate profile, any CV, any position
- Can perform all Recruiter actions and all Candidate actions
- User management: view, block, unblock, delete users; assign/remove roles
- **Can remove their own Administrator role** (self-demotion is allowed)

---

## 4. Optimistic Locking (Cross-Cutting, Required)

Applies to: **profile auto-save**, **position edits**, **attribute edits** (anything editable, effectively).

Rules:
- Every editable record carries a **version** field (numeric counter, timestamp, or similar — implementer's choice per the instructor's explanation of trade-offs).
- Every save operation:
  1. Sends the version it was read with.
  2. Server updates only if `id` matches **and** version matches current DB version.
  3. On success: version is incremented/regenerated and the new version is returned to the client.
  4. On mismatch: update fails — **no partial/blind overwrite**.
- **Client must handle version conflicts gracefully** — this is a cross-cutting concern spanning client and server, not something to hide behind an API. At minimum: detect the conflict, inform the user, and allow reload/retry (instructor explicitly says full merge UI is not required for this small project; reload + retry is acceptable).
- No pessimistic locking (`locked_by`/`locked_at` fields) — optimistic only.
- Do not implement multi-DB replication/clustering — not needed at this scale; only the version-check mechanism itself matters.

---

## 5. Personal Profile

Every authenticated user has one profile page. Only the **owner** and **Administrators** may edit/view the full profile. Recruiters see only a read-only CV view (not profile editing).

### 5.1 Section: Me
- Mandatory **built-in** attributes that always exist and can never be removed (by Recruiters) — e.g., First Name, Last Name, Location, Personal Photo.
- Built on the **same attribute engine** as library attributes (so they *can* be referenced/added to position templates), but they are protected from deletion.

### 5.2 Section: Info
- User-selected attributes pulled from the shared Attribute Library.
- Candidate can add/remove attributes from their own profile and fill in values.
- Example attributes: IELTS Score, Presentation Skills, Remote Work Availability.

### 5.3 Section: Projects
Each project has:
- Name
- Period (date range)
- Description (Markdown-formatted — use a ready-made Markdown renderer/editor component)
- Technology Tags (autocomplete from previously entered tags across the system; use a proper tag-input UI component)

CRUD: add, edit, remove projects.

### 5.4 Section: CVs
- Lists all CVs created by this Candidate.
- **Max one CV per position** per Candidate.
- Existing CVs: editable, deletable.
- New CVs: creatable only for positions the Candidate currently has access to.
- Each CV list entry links to the CV page.

### 5.5 Auto-Save (profile pages)
- Changes tracked client-side.
- Saved automatically every **5–10 seconds** (not on every keystroke).
- Must use **optimistic locking** (see §4).

---

## 6. Killer Feature #1 — Attribute Library

Shared, global pool managed collectively by all Recruiters (create/edit/delete).

### 6.1 Attribute fields
- Category (one of a predefined fixed list, e.g., Certification, Domain Knowledge, Personal Information, Soft Skills)
- Name (globally unique)
- Description
- Data type (see 6.2)

### 6.2 Supported data types
| Type | Notes |
|---|---|
| String | single-line plain text |
| Text | Markdown-formatted |
| Image | external cloud storage, drag-and-drop upload widget (no server/DB storage of image bytes) |
| Numeric | |
| Date | |
| Period | date range |
| Boolean | checkbox |
| One of many | dropdown, with a defined option list |

### 6.3 Attribute selection UX (library may grow large)
Must support:
- Prefix lookup (type-ahead)
- "Recently used" list
- Category filtering

---

## 7. Killer Feature #2 — Positions

Shared pool, no ownership — any Recruiter can create/duplicate/edit/delete any position.

### 7.1 Position fields
- Title, Short Description (basic info)
- Access Rules: **Public** (any authenticated user) or **Restricted** (filter-based — see 7.2)
- Attributes: a selected subset from the Attribute Library, forming the CV template
- Project Tags: tags used to filter which of a candidate's projects are pulled into the generated CV
- Max number of projects to include in generated CV
- *(Nice-to-have, not crucial per instructor)*: extra descriptive position attributes like Company, Level (Junior/Middle/Senior/C-level) for filtering/sorting the positions table

### 7.2 Access rule filters
Example filter conditions, evaluated against the candidate's attribute values:
- Numeric attribute comparisons, e.g., `IELTS Score > 7.0`
- Boolean checkbox equals true, e.g., `Remote Work = checked`
- Dropdown equals a specific option, e.g., `Presentation Skills = "Advanced"`

Available operators depend on the attribute's data type (numeric → comparisons; boolean → is/is-not; dropdown → equals/one-of; etc.).

### 7.3 Position → CVs
- Each position shows the list of CVs generated from it.
- This list is visible only to **Recruiters and Administrators**.

### 7.4 Access changes and existing CVs
- If a Candidate loses access to a position (no longer matches the filter), their already-created CV for that position is **not deleted** — it is **hidden from UI** for everyone (Candidate view and Recruiter view) until/unless access is regained.
- Candidates may only create CVs for positions they are currently authorized to access.

---

## 8. Killer Feature #3 — CV Generation

### 8.1 Conceptual model — CV is "almost virtual"
This is an explicit clarification from the instructor and must shape the data model:

- A CV is **not** a snapshot/copy of profile data. Pressing "Create CV" for a position stores a CV record with essentially technical fields only (id, created_by, position reference, status, likes, version, etc.).
- All displayed **content** (attribute values, project list) is **looked up live** from:
  - The candidate's profile attribute values (undeletable "Me" attributes + Info attributes)
  - The candidate's projects (filtered by the position's project tags, capped at the position's max count)
- There is **one master value per attribute per Candidate** — e.g., a Candidate cannot have a different "English Level" in two different CVs. Editing an attribute anywhere (profile page or inside a CV) updates the single underlying value, and that change is reflected **everywhere** it's used.
- If a position's access filter changes such that the CV becomes inaccessible, the CV record still exists but is hidden in the UI — it is not deleted and its underlying attribute data is untouched.
- Do **not** store CV content as a duplicated/serialized snapshot (JSON or otherwise). Store attributes normalized in relational tables, referenced by profile and by position template, so that:
  - CVs for the same position remain structurally "compatible" (same attribute set) for table display, sorting, filtering, aggregation.
  - Changing a position's attribute set does not require rewriting/"fixing" existing CVs in bulk.

### 8.2 CV assembly logic
A generated CV includes:
- Undeletable "Me" profile attributes
- Position's selected library attributes — value pulled from the candidate's profile if present; otherwise shown **empty**
- Candidate's projects filtered by the position's project tags, limited to the position's max project count

### 8.3 Editing behavior
- Owning Candidate: attributes are pre-filled from the profile where available; missing ones can be filled **in place** on the CV page.
- Each attribute is editable in-place within the CV; there is only **one** stored value (in the profile) — editing it from the CV updates the profile value.
- Empty attribute values are **highlighted in red**.
- Recruiters: **read-only** rendered CV view; empty values still highlighted red; recruiters cannot edit CV/profile data directly.
- Administrators: can edit CVs directly (acting as owner).

### 8.4 CV status / publishing
- CVs need a **state/track** (e.g., Draft → Published).
- "Publish" action available **only when all required attributes are filled**.
- Publishing makes the CV visible to Recruiters (i.e., unpublished/incomplete CVs are not visible in Recruiter search/listing).

---

## 9. Likes

- Only Recruiters may like CVs.
- One Recruiter → at most one like per CV (toggle: like/unlike).
- Total like count displayed in CV list views and in search results.

---

## 10. Discussions

- Each Position has a Discussion tab.
- Post fields: author name, timestamp, Markdown-formatted text content.
- When viewed by a Recruiter, the author name is a link to that user's **public profile view**.
- Posts are strictly append-only, chronological — no inserting between existing posts, no reordering.
- Real-time-ish updates: new posts must appear to other active viewers within **2–5 seconds** (WebSockets, polling, SignalR, or any mechanism — implementer's choice).

---

## 11. Main (Landing) Page

Must contain:
- **Latest Positions** — table of most recently created/updated positions
- **Most Popular Positions** — top 5 by number of submitted CVs
- **Tag Cloud** of technology tags:
  - For Recruiters: tags link to CVs
  - For Candidates: tags link to positions
- **Statistics** block, e.g.:
  - CVs created in last 24h
  - Total positions
  - Total candidates
  - Total recruiters
  - Total submitted CVs

---

## 12. Full-Text Search

- Must be accessible from the top header on **every** page.
- Recruiters use it to find candidate CVs.
- Implementation: native DB full-text feature or external search library — either is acceptable.

---

## 13. Data Modeling Guidance (Instructor Directives)

### 13.0 Core rules

- **Do not** serialize CV data as JSON for storage.
- **Do** store attributes in one normalized table structure and **reference** them from profile/position/CV-related tables (EAV-style relational design, not per-position dynamic tables).
- **Do not** generate database tables on the fly per position.
- Attributes must remain editable at the definition level (e.g., rename a library attribute, or remove it from a position) without needing to bulk-rewrite existing CV data — because CV content is looked up live, not duplicated (see §8.1).
- All CVs generated from the same position must be structurally comparable/aggregable (same attribute set) for table display, sorting, filtering, and aggregate calculations.
- **Must use an ORM.**
- **No raw `SELECT *`, no queries inside loops.**

### 13.1 Attribute / value data model (EAV-style, explicit example)

- **Attribute definitions (name, type, category) are global**, managed by Recruiters, and shared by everyone. They are not tied to any single position.
- **Attribute values belong to Candidates**, not to CVs and not to positions. There is one value per attribute per Candidate (as already stated in §8.1).
- **A Position stores a subset of attribute references** (i.e., which global attributes are part of that position's template) plus its own settings (access rule filters, project tag filter, max project count) — a position does **not** own or duplicate attribute values.
- **Category** is purely a UI grouping/filtering aid:
  - Stored as `category_id` on each attribute, referencing a lookup table.
  - Category has **no effect on data/business logic** — a Recruiter can attach any attribute to any position regardless of category or position title (e.g., a "Dancing Skills" attribute on an "Engineer" position is valid and should not be blocked or "understood" semantically by the system).
  - The category lookup table can be extended at the DB level, but **no admin UI is required** for managing categories themselves (just a fixed/seeded lookup list).
- **Attributes are fully independent of each other** — no attribute-to-attribute relationships/dependencies need to be modeled.

Illustrative schema shape (values table):

| user_id | attribute_id | value |
|---|---|---|
| user_a | attr_007 | 21 |
| user_a | attr_009 | Mary |
| user_b | attr_007 | 32 |
| user_b | attr_009 | John |
| user_c | attr_007 | 10 |
| user_c | attr_009 | Ellen |
| user_c | attr_010 | Frisco |

### 13.2 Behavior when attribute definitions change after CVs exist

Worked example from instructor (must inform design/testing expectations):

1. Recruiter creates attributes `attr_007` = "Person Age" (numeric) and `attr_009` = "Person Name" (string), used on position "Generic Employee".
2. `user_a` creates a CV for that position: Age=21, Name="Mary".
3. `user_b` creates a CV for that position: Age=32, Name="John".
4. Recruiter **renames** `attr_007` from "Person Age" to "Finger Amount", and **adds** a new attribute `attr_010` = "Address" (string) to the position.
5. `user_c` creates a CV: Finger Amount=10, Name="Ellen", Address="Frisco".

Expected/accepted behavior:
- `user_a`'s and `user_b`'s existing CVs render the renamed attribute under its **new** label/meaning (i.e., their old "Person Age" values of 21 and 32 now display as "Finger Amount": 21 / 32). **This is expected and acceptable** — do not try to snapshot the old attribute name/meaning per-CV.
- `user_a`'s and `user_b`'s CVs show **no value** for "Address" (empty/highlighted red per §8.3), since it didn't exist when they created their CVs and they haven't filled it in.
- Aggregate calculations (e.g., "average Finger Amount across CVs for this position") will mix old and new semantics if the Recruiter renamed a field's meaning — this is a known, accepted quirk of the model per instructor: **do not overcomplicate the implementation to guard against Recruiters renaming attributes into something semantically different.** The system is not responsible for preventing or reconciling semantic drift caused by a Recruiter's own edits.
- This confirms and reinforces §8.1 / §13: never duplicate/snapshot attribute name or value into the CV; always resolve live from the current attribute definition + current candidate value.

### 13.3 Project tag filtering example

- A position can restrict which of a Candidate's projects appear in the generated CV by **tag matching** plus a **max count**.
- Example: position requires projects tagged with both "Python" and "Data Engineering", max 3. When a Candidate generates their CV, only the **most recent** (up to) 3 of their own projects that match those tags are included — irrelevant/older/non-matching projects are excluded automatically, not manually curated per CV.

### 13.4 Deletion policy

- No explicit legal/compliance requirements exist for this project (no mandated data retention or right-to-erasure rules to model), so the **simplest approach is acceptable and preferred**.
- **Preferred approach: DB-level cascade deletion** (`ON DELETE CASCADE` via FK constraints / ORM cascade configuration) rather than hand-written application code that deletes related rows one-by-one or in loops.
  - Rationale (instructor): manually deleting related rows in application code (e.g., "delete position → delete its comments → delete its CVs in a loop") is slower, is not atomic/safe against mid-operation failures (e.g., a network hiccup after partially deleting), and unnecessarily duplicates work the database already does reliably.
  - Where multi-step deletion logic genuinely can't be avoided, wrap it in a transaction — but prefer configuring cascade delete at the schema/ORM level first.
- **Soft delete (a `deleted`/`is_deleted` flag plus filtering logic through all layers) is allowed but not required** — only introduce it if it's genuinely convenient for a specific case; it is not mandated anywhere in this project. Default to hard delete + cascade unless there's a clear reason not to.

## 13a. Code Quality & Engineering Practices (Instructor Directives)

These are explicit "how to build it" directives, separate from functional requirements — they will likely be checked during code review/defense.

### 13a.1 Use ready-made components, don't hand-roll
- Prefer existing, well-known libraries/controls over custom-built equivalents wherever one exists.
- Examples explicitly called out in the brief: Markdown renderer, drag-and-drop image uploader, tag-input control (with autocomplete), tag cloud renderer.
- Goal stated by instructor: "the less custom code your app contains, the better." Don't reinvent things libraries already solve well.
- Do not copy-paste code from tutorials/other sources — use the library as a dependency, understand what it does, be able to explain it.

### 13a.2 Professional UI
- The UI should look like a real product, not a bare scaffold — consistent spacing, typography, and component styling via the chosen CSS framework.
- Consistent navigation across all pages (already required in §2), consistent table styling, consistent form styling.
- Respect the anti-patterns already listed in §2 (no per-row buttons, no tile/gallery layout) — these are as much about professional UI as they are about hard rules.

### 13a.3 Small, well-named methods
- Keep methods/functions small and single-purpose (one clear responsibility each) rather than large procedural blocks.
- Method and variable names should be descriptive and self-explanatory (professional naming) — a reviewer/defender should be able to understand what a method does from its name and signature alone, without reading the body.
- Avoid "magic" literals: no unexplained hard-coded numbers or strings scattered through the code. Use named constants, enums, or configuration values instead (e.g., attribute type codes, role names, status values, category lists should all be named/enumerated, not inlined as raw strings/numbers).
- This applies equally to query code — avoid ad-hoc/opaque query logic; prefer clearly named repository/service methods (e.g., `GetPublishedCvsForPosition(positionId)` rather than inline, unnamed, hard-to-follow query blocks scattered across controllers).

### 13a.4 Images: direct-to-cloud upload, never through your own DB/server
- Already stated as a hard "don't" in §2, restated here for emphasis because it affects both UI component choice and architecture:
  - Images (e.g., profile photo, Image-type attribute values) must be uploaded via drag-and-drop directly to an **external cloud storage service**.
  - Your own web server and database must **never** receive, store, or proxy the raw image bytes — only the resulting URL/reference from the cloud storage service is persisted in the database.
  - This should be implemented as a separate, decoupled upload flow (client uploads straight to cloud storage — or via a short-lived signed URL/token issued by your server — not routed as file bytes through your application server).

## 14. Optional Requirements (Only Count If Core Requirements Are 100% Complete)

Graded separately; do not attempt before core spec is fully done:

1. Printable PDF export of CVs with a QR code linking back to the app.
2. Form-based authentication with email confirmation (as an alternative to social login).
3. Badge/achievement system (e.g., "10 projects", "5 CVs", "25 likes") — rendered as an SVG panel on the profile, downloadable.
4. Field "tuning" options: text length limits, regex validators, numeric ranges, etc., configurable per attribute.
5. Export CVs for a given position to an aggregate CSV/Excel file.

---

## 15. Known Future Additions (Do Not Pre-Build)

- Instructor has stated additional requirements will come later: **up to 3 integrations with third-party applications/services**.
- Explicit warning: do **not** build generic/speculative features like a "store" or "blog" — the future requirements will be specific. Keep the current build strictly scoped to this spec.

---

## 16. Screening / Process Notes (Non-Functional, Informational Only)

- A screening test task exists separately from the project; project work continues regardless of test outcome, but test result contributes to overall score.
- Submission deadline: **23.09.2026**, submitted to HR (not to the instructor).
- Grading emphasizes **understanding your own code** over feature count — be ready to explain and modify any line during defense, including on the spot.
- Partial completion is acceptable if well understood and well executed; polish and comprehension over raw feature count.

---

## 17. Quick Compliance Checklist

Use this to self-audit before submission:

- [ ] C# + (Blazor or MVC) + relational DB + ORM
- [ ] CSS framework in use, responsive on mobile
- [ ] Full-text search wired up, reachable from header on every page
- [ ] No per-row buttons anywhere (toolbar/selection pattern instead)
- [ ] Positions and CVs shown as tables, never tiles/cards
- [ ] Anonymous access rules enforced exactly as in §3.1/3.2
- [ ] ≥2 social login providers
- [ ] Roles + permissions match §3.5–3.7 exactly (esp. Recruiter = no edit rights on CV/profile, only like + read-only view)
- [ ] Optimistic locking implemented end-to-end (version field, conflict detection, client handling) for profile auto-save, positions, attributes
- [ ] Profile: Me (undeletable, same engine as library attrs) / Info / Projects / CVs sections
- [ ] Auto-save every 5–10s, not per keystroke
- [ ] Attribute Library: category, unique name, description, 8 supported types, prefix search, recently-used, category filter
- [ ] Positions: shared pool, no ownership, duplicate/edit/delete, access rule filters by attribute type, attribute set = CV template, project tag filter + max project count
- [ ] CV data model is **not** JSON-serialized; attributes normalized & referenced; CV content resolved live, not duplicated
- [ ] One master attribute value per candidate, shared across all their CVs and profile
- [ ] Losing position access hides (not deletes) existing CV
- [ ] In-place CV editing updates profile; empty values shown in red
- [ ] Recruiters: read-only CV view only; cannot edit
- [ ] CV Draft/Published states; Publish gated on all attributes filled; only Published CVs visible to Recruiters
- [ ] Likes: Recruiter-only, one per CV, toggleable, count shown in lists/search
- [ ] Discussions: per-position tab, Markdown, append-only chronological, ~2–5s live update, author links to public profile for Recruiter viewers
- [ ] Main page: latest positions, top-5 popular positions, tag cloud (role-aware links), stats block
- [ ] Two languages (UI only, not user content), persisted per user
- [ ] Two themes (light/dark), persisted per user
- [ ] No raw `SELECT *`, no queries in loops, no images stored in DB/server, no dynamic table generation
- [ ] Ready-made components used for Markdown, image upload, tag input, tag cloud (not custom-built)
- [ ] UI is polished/consistent, not a bare scaffold
- [ ] Methods are small, single-purpose, descriptively named; no magic numbers/strings (use constants/enums)
- [ ] Query/data-access logic lives in clearly named repository/service methods, not inline opaque blocks
- [ ] Images upload directly to external cloud storage; server/DB only ever stores the resulting URL, never raw bytes
- [ ] Attribute definitions are global (name/type/category); values are keyed to Candidate + attribute only; positions reference attributes, never own/copy values
- [ ] Category is a simple lookup used only for filtering/grouping in UI — no business logic depends on it, no category admin UI required
- [ ] Renaming/adding attributes on a position does not require rewriting existing CVs; old CVs simply render under the new attribute name/definition (accepted quirk, not a bug to "fix")
- [ ] Project inclusion in a CV = tag match (all required tags) + most-recent-first, capped at position's max count
- [ ] Deletion implemented via DB-level cascade (FK/ORM cascade), not manual multi-step loop deletion in app code; transactions only where cascade truly can't cover it
- [x] Database engine decided: PostgreSQL
- [ ] Deployable "Hello, world" baseline exists and a working deployable build is kept at all times
