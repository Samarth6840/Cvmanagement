# CV Management System — Implementation Plan

## Architecture Overview

Blazor Server (.NET 10) + EF Core + PostgreSQL + ASP.NET Core Identity + SignalR. EAV-style data model for attributes. CV content resolved live (no snapshots).

---

## Phase 1: Foundation (NuGet + Entities + DbContext + Migration)

### 1.1 Add NuGet packages to `CvManagement.csproj`

```xml
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.*" />
<PackageReference Include="Microsoft.AspNetCore.Authentication.Google" Version="10.0.*" />
<PackageReference Include="Microsoft.AspNetCore.Authentication.GitHub" Version="10.0.*" />
<PackageReference Include="Markdig" Version="0.39.*" />
<PackageReference Include="CloudinaryDotNet" Version="1.26.*" />
<PackageReference Include="Microsoft.Extensions.Localization" Version="10.0.*" />
<PackageReference Include="Microsoft.AspNetCore.SignalR" Version="10.0.*" />
```

### 1.2 Entity Model

All files in `Data/Entities/`.

**Identity:**
- `ApplicationUser.cs` — extends `IdentityUser<Guid>`. Props: DisplayName, AvatarUrl, PreferredLanguage, PreferredTheme, CreatedAt, UpdatedAt, RowVersion. Nav: CandidateProfile, GivenLikes, DiscussionMessages.
- `ApplicationRole.cs` — extends `IdentityRole<Guid>`. Props: Description.

**Attributes (EAV):**
- `AttributeCategory.cs` — enum: Certification, DomainKnowledge, PersonalInformation, SoftSkills, TechnicalSkills, Education, Experience, Other
- `AttributeDataType.cs` — enum: String, Text, Image, Numeric, Date, Period, Boolean, OneOfMany
- `AttributeDefinition.cs` — Id, Name, Slug (unique), Category (enum), DataType (enum), Description, IsRequired, SortOrder, CreatedAt, UpdatedAt, RowVersion. Nav: Options, ProfileValues, PositionAttributeRules.
- `AttributeOption.cs` — Id, AttributeDefinitionId, Label, SortOrder. Nav: AttributeDefinition.

**Profiles:**
- `CandidateProfile.cs` — Id, UserId (unique), CreatedAt, UpdatedAt, RowVersion. Nav: User, AttributeValues, CandidateProjects.
- `ProfileAttributeValue.cs` — Id, CandidateProfileId, AttributeDefinitionId. Type-specific columns: StringValue, TextValue, ImageUrl, NumericValue, DateValue, PeriodStart, PeriodEnd, BoolValue, SelectedOptionId. Unique index on (CandidateProfileId, AttributeDefinitionId).

**Positions:**
- `Position.cs` — Id, Title, Description, Company, IsPublic, IsOpen, MaxProjects, CreatedByUserId, CreatedAt, UpdatedAt, RowVersion. SearchVector (tsvector shadow prop). Nav: CreatedByUser, AttributeRules, Tags, AccessRules, CvRecords, DiscussionMessages.
- `PositionAttributeRule.cs` — Id, PositionId, AttributeDefinitionId, IsRequired, SortOrder. Nav: Position, AttributeDefinition.
- `PositionAccessRule.cs` — Id, PositionId, AttributeDefinitionId, Operator (enum: GreaterThan, LessThan, Equals, NotEquals, Contains), FilterValue (string). Nav: Position, AttributeDefinition.
- `PositionAccessOperator.cs` — enum: GreaterThan, LessThan, Equals, NotEquals, Contains
- `PositionTag.cs` — Id, PositionId, Tag (string). Nav: Position.

**CVs:**
- `CvStatus.cs` — enum: Draft, Published
- `CvRecord.cs` — Id, CandidateProfileId, PositionId, Status, CreatedByUserId, LikeCount (denormalized counter), CreatedAt, UpdatedAt, RowVersion. SearchVector (tsvector shadow prop). Nav: CandidateProfile, Position, CreatedByUser, Likes, DiscussionMessages.

**Projects:**
- `Project.cs` — Id, Name, Description, Url, StartDate, EndDate, CreatedAt, UpdatedAt, RowVersion. Nav: Tags, CandidateProjects.
- `ProjectTag.cs` — Id, ProjectId, Tag. Nav: Project.
- `CandidateProject.cs` — Id, CandidateProfileId, ProjectId, Role, Contribution (Markdown). Unique on (CandidateProfileId, ProjectId). Nav: CandidateProfile, Project.

**Likes:**
- `CvLike.cs` — Id, CvRecordId, UserId, LikedAt. Unique on (CvRecordId, UserId). Nav: CvRecord, User.

**Discussions:**
- `DiscussionMessage.cs` — Id, PositionId, UserId, Content (Markdown), CreatedAt. Nav: Position, User.

### 1.3 DbContext & Configurations

- `Data/AuditableDbContext.cs` — SaveChanges override: sets UpdatedAt on modified entities, manages RowVersion.
- `Data/CvDbContext.cs` — extends `AuditableDbContext`, inherits from `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`. All DbSets. `ApplyConfigurationsFromAssembly`. Identity table renames to singular (Users, Roles, etc.). ConfigureConventions: strings default to varchar(256).
- `Data/Configurations/` — one `IEntityTypeConfiguration<T>` per entity. Key configs:
  - ProfileAttributeValue: unique on (CandidateProfileId, AttributeDefinitionId)
  - CvLike: unique on (CvRecordId, UserId)
  - Position: GIN index on SearchVector
  - CvRecord: GIN index on SearchVector
  - All FKs: cascade delete where appropriate (position → attribute rules/tags/access rules/cv records/discussion messages; attribute definition → options; candidate profile → attribute values)

### 1.4 Search Triggers (SQL migration)

```sql
CREATE OR REPLACE FUNCTION position_search_vector_update() RETURNS trigger AS $$
BEGIN
    NEW."SearchVector" :=
        setweight(to_tsvector('english', coalesce(NEW."Title", '')), 'A') ||
        setweight(to_tsvector('english', coalesce(NEW."Description", '')), 'B') ||
        setweight(to_tsvector('english', coalesce(NEW."Company", '')), 'C');
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER position_search_vector_trigger
    BEFORE INSERT OR UPDATE ON "Positions"
    FOR EACH ROW EXECUTE FUNCTION position_search_vector_update();

CREATE OR REPLACE FUNCTION cv_search_vector_update() RETURNS trigger AS $$
BEGIN
    NEW."SearchVector" :=
        setweight(to_tsvector('english', coalesce(NEW."Title", '')), 'A');
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER cv_search_vector_trigger
    BEFORE INSERT OR UPDATE ON "CvRecords"
    FOR EACH ROW EXECUTE FUNCTION cv_search_vector_update();
```

### 1.5 Identity Seed

- `Data/Seed/IdentitySeed.cs` — creates Candidate, Recruiter, Administrator roles + default admin user.

### 1.6 appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=cvmanagement;Username=postgres;Password=changeme"
  },
  "Authentication": {
    "Google": { "ClientId": "", "ClientSecret": "" },
    "GitHub": { "ClientId": "", "ClientSecret": "" }
  },
  "Cloudinary": { "CloudName": "", "ApiKey": "", "ApiSecret": "" }
}
```

### 1.7 Program.cs (foundation part)

Configure DB, Identity, auth middleware. Add `dotnet ef migrations add InitialCreate` + manual SQL trigger migration.

**Verify:** `dotnet build` succeeds, migration applies, DB tables created.

---

## Phase 2: Auth, Layout, Theme/Locale Infrastructure

### 2.1 Services

- `Services/Auth/ICurrentUserService.cs` + `CurrentUserService.cs` — wraps `IHttpContextAccessor`, exposes GetUserIdAsync, GetUserAsync, IsInRoleAsync, GetPreferredLanguage, GetPreferredTheme.
- `Services/Auth/RoleSeeder.cs` — called at startup.

### 2.2 Auth Pages

- `Components/Pages/Auth/Login.razor` — `@page "/login"`, `@layout AuthLayout`. Social login buttons (Google, GitHub) + email/password form.
- `Components/Pages/Auth/Register.razor` — `@page "/register"`, `@layout AuthLayout`.
- `Components/Pages/Auth/ExternalLoginCallback.razor` — handles OAuth callback, creates user if new, assigns Candidate role.
- `Components/Pages/Auth/AccessDenied.razor` — `@page "/access-denied"`.

### 2.3 Layouts

- `Components/Layout/AuthLayout.razor` — centered card layout for login/register (no sidebar).
- `Components/Layout/MainLayout.razor` — full rewrite: TopBar + role-based sidebar + @Body.
- `Components/Layout/NavMenu.razor` — rewrite with conditional NavLinks per role.
- `Components/Layout/TopBar.razor` — search input (always), theme toggle, language selector, user dropdown.

### 2.4 Theme & Locale

- `wwwroot/css/themes.css` — CSS custom properties for light/dark.
- `wwwroot/js/theme-toggle.js` — `applyTheme(theme)` sets `data-bs-theme`, persists to localStorage.
- `Components/Shared/ThemeToggle.razor` — toggle button.
- `Components/Shared/LanguageSelector.razor` — EN/second language dropdown.
- `Components/App.razor` — wrap body in `<CascadingAuthenticationState>`, add script references for theme/EasyMDE/Tagify/Cloudinary.

### 2.5 SignalR

- `builder.Services.AddSignalR()` in Program.cs.
- `app.MapHub<DiscussionHub>("/hubs/discussion")` (hub file created in Phase 7).

**Verify:** App runs, login/register work, social login buttons appear, theme toggles, nav is role-aware.

---

## Phase 3: Attribute Library + Profile

### 3.1 Attribute Service

- `Services/Attributes/IAttributeService.cs` + `AttributeService.cs` — CRUD for AttributeDefinition + AttributeOption. Methods: GetAllAsync, GetByIdAsync, CreateAsync, UpdateAsync, DeleteAsync, GetOptionsAsync, AddOptionAsync, RemoveOptionAsync, SearchAsync (prefix lookup), GetRecentlyUsedAsync.

### 3.2 Attribute Pages (Recruiter/Admin)

- `Components/Pages/Attributes/AttributeList.razor` — `@page "/attributes"`. TableView with columns: Name, Category, DataType, Required. Toolbar: Create, Edit, Delete. Search bar for prefix lookup. Category filter dropdown.
- `Components/Pages/Attributes/AttributeEdit.razor` — `@page "/attributes/edit/{Id?}"`. Form: Name, Category dropdown, DataType dropdown, Description, IsRequired checkbox. If OneOfMany: dynamic option list editor.

### 3.3 Profile Service

- `Services/Profiles/IProfileService.cs` + `ProfileService.cs` — GetOrCreateProfileAsync, GetProfileAsync, UpsertAttributeValueAsync (optimistic locking), SaveProfileAsync. Resolves attribute values from profile.

### 3.4 Profile Pages (Candidate)

- `Components/Pages/Profile/MyProfile.razor` — `@page "/profile"`. Sections: Me (built-in attributes), Info (user-selected attributes), Projects, CVs. Auto-save with PeriodicTimer(5s). Dirty flag. Catches DbUpdateConcurrencyException for conflict handling.
- `Components/Pages/Profile/PublicProfile.razor` — `@page "/profile/{UserId}"`. Read-only view.

### 3.5 Shared Components

- `Components/Shared/AttributeValueEditor.razor` — dispatches by DataType: InputText (String), textarea (Text), InputNumber (Numeric), InputDate (Date), two dates (Period), InputCheckbox (Boolean), dropdown (OneOfMany), ImageUpload (Image).
- `Components/Shared/MarkdownEditor.razor` — EasyMDE wrapper via JS interop.
- `Components/Shared/MarkdownPreview.razor` — server-side Markdig render.
- `Components/Shared/ImageUpload.razor` — Cloudinary widget wrapper via JS interop.
- `Components/Shared/TagInput.razor` — Tagify wrapper via JS interop.

### 3.6 JS Interop Files

- `wwwroot/js/easymde-init.js` — `initEasyMDE(dotNetRef, elementId)`.
- `wwwroot/js/tagify-init.js` — `initTagify(elementId, existingTags)`.
- `wwwroot/js/cloudinary-upload.js` — `openCloudinaryWidget(dotNetRef, uploadPreset)`.

### 3.7 CDN Links (in App.razor or Layout)

Add EasyMDE CSS/JS, Tagify CSS/JS from CDN in `App.razor` head/body.

**Verify:** Recruiter can CRUD attributes. Candidate can fill profile values. Auto-save works. Optimistic locking conflict shown.

---

## Phase 4: Positions & Projects

### 4.1 Position Service

- `Services/Positions/IPositionService.cs` + `PositionService.cs` — CRUD, GetAccessiblePositionsAsync (evaluates access rules), AddAttributeRuleAsync, AddTagAsync, RemoveTagAsync, AddAccessRuleAsync, RemoveAccessRuleAsync, DuplicateAsync.

### 4.2 Position Pages

- `Components/Pages/Positions/PositionList.razor` — `@page "/positions"`. TableView: Title, Company, Status (Open/Closed), Created, CV Count. Toolbar: Create, Edit, Delete, Duplicate, Toggle Open/Closed. Anonymous sees only public.
- `Components/Pages/Positions/PositionEdit.razor` — `@page "/positions/edit/{Id?}"`. Form: Title, Description (Markdown), Company, IsPublic toggle. Attribute rules editor (add/remove attributes from library, set required/optional). Tag editor (Tagify). Access rules editor (add attribute-based filters with operator + value). MaxProjects input.
- `Components/Pages/Positions/PositionDetail.razor` — `@page "/positions/{Id}"`. Read-only view + attribute list + tags + CV list (Recruiter/Admin) + discussion panel.

### 4.3 Project Service

- `Services/Projects/IProjectService.cs` + `ProjectService.cs` — CRUD for Project + ProjectTag + CandidateProject.

### 4.4 Project Pages

- `Components/Pages/Projects/ProjectList.razor` — `@page "/projects"`. TableView: Name, Tags, Created. Toolbar: Create, Edit, Delete.
- `Components/Pages/Projects/ProjectEdit.razor` — `@page "/projects/edit/{Id?}"`. Form: Name, Description (Markdown), Url, StartDate, EndDate, Tags (Tagify).

### 4.5 Shared Components

- `Components/Shared/TableView.razor` — generic `TItem`, sortable columns, checkbox selection, toolbar slot. No per-row buttons.
- `Components/Shared/SelectToolbar.razor` — action bar that appears when items selected.
- `Components/Shared/Pagination.razor`.
- `Components/Shared/ConfirmDialog.razor`.

**Verify:** Positions CRUD works. Attribute rules and access rules configured. Projects CRUD. TableView pattern used everywhere.

---

## Phase 5: CV System

### 5.1 CV Service

- `Services/Cv/ICvService.cs` + `CvService.cs` — CreateAsync (creates CvRecord with technical fields only), GetByIdAsync, GetAllAsync, DeleteAsync, PublishAsync (checks all required attributes filled), UnpublishAsync, GetCandidateCvsForPositionAsync (max 1 per position), CanPublishAsync (checks all required attrs have values), GetCvAttributeValuesAsync (resolves live from profile), GetCvProjectsAsync (filters by position tags, limits by max count).
- `Services/Cv/CvRenderer.cs` — RenderHtmlAsync: gets position attribute template → resolves candidate profile values → filters projects by tags → renders via Markdig.

### 5.2 CV Pages

- `Components/Pages/Cvs/CvList.razor` — `@page "/cvs"`. Candidate sees own CVs. Recruiter/Admin sees all published CVs. TableView: Title, Position, Status, Likes, Created. Toolbar: Create, Edit, Delete, Publish/Unpublish.
- `Components/Pages/Cvs/CvEdit.razor` — `@page "/cvs/edit/{Id?}"` or `@page "/cvs/create/{positionId}"`. Shows position's attribute template. Each attribute resolved from profile (pre-filled if available). Empty values highlighted red. Candidate can fill in-place (updates profile value). Publish button (disabled if required attrs empty). Recruiter: read-only. Admin: editable.
- `Components/Pages/Cvs/CvDetail.razor` — `@page "/cvs/{Id}"`. Rendered HTML view of CV. Empty attrs highlighted red.

### 5.3 Markdown Renderer

- `Services/Markdown/MarkdownRenderer.cs` — wraps Markdig pipeline (advanced extensions: tables, task lists, auto-links).

**Verify:** CV creation for position works. Attribute values resolved live. Publish gating works. Empty values highlighted. Editing from CV updates profile.

---

## Phase 6: Likes + Full-Text Search

### 6.1 Like Service

- `Services/Likes/ILikeService.cs` + `LikeService.cs` — ToggleAsync (Recruiter only), GetLikeCountAsync, HasUserLikedAsync, GetTopLikedAsync. Updates CvRecord.LikeCount denormalized field.

### 6.2 Like Component

- `Components/Shared/LikeButton.razor` — toggle heart icon + count badge. Recruiter-only visibility.

### 6.3 Search Service

- `Services/Search/ISearchService.cs` + `SearchService.cs` — SearchAsync using `plainto_tsquery` against Position and CvRecord SearchVector columns. Returns combined results with rank.

### 6.4 Search Components

- `Components/Shared/SearchBar.razor` — in TopBar, input with debounced autocomplete (optional), navigates to `/search?q=...`.
- `Components/Pages/Search/SearchResults.razor` — `@page "/search"`. Query param `q`. TableView of combined Position + CV results with rank, excerpt, type badge.

**Verify:** Recruiter can like/unlike CVs. Search returns relevant results. Search accessible from header on every page.

---

## Phase 7: Discussions + SignalR

### 7.1 Hub

- `Hubs/DiscussionHub.cs` — `SendMessage(positionId, content)`. Adds message to DB, broadcasts to group.

### 7.2 Discussion Service

- `Services/Discussions/IDiscussionService.cs` + `DiscussionService.cs` — GetMessagesAsync (paginated), AddMessageAsync.

### 7.3 Discussion Component

- `Components/Pages/Discussions/DiscussionPanel.razor` — embedded in PositionDetail. SignalR client: joins group on init, receives messages, appends to list. MarkdownEditor + Send button. Each message rendered with MarkdownPreview. Author name links to public profile (for Recruiter viewers).

**Verify:** Two browser tabs open same position. Send message in one → appears in other within 2-5s.

---

## Phase 8: Landing Page

### 8.1 Landing Page

- `Components/Pages/Home.razor` — rewrite. Sections:
  - Hero with tagline
  - Latest Positions table (top 10)
  - Most Popular Positions (top 5 by CV count)
  - Tag Cloud (all position + project tags, weighted by frequency)
  - Statistics: CVs in last 24h, total positions, total candidates, total recruiters, total CVs

### 8.2 Tag Cloud Component

- `Components/Shared/TagCloud.razor` — renders tags sized by frequency. For Recruiters: links to CVs. For Candidates: links to positions. Anonymous: links to position list.

**Verify:** Landing page shows all sections. Tag cloud links are role-aware. Stats are live.

---

## Phase 9: Admin + Localization

### 9.1 Admin Pages

- `Components/Layout/AdminLayout.razor` — admin-specific sidebar.
- `Components/Pages/Admin/Dashboard.razor` — `@page "/admin"`. Stats overview, user count by role.
- `Components/Pages/Admin/UserManagement.razor` — `@page "/admin/users"`. TableView: UserName, Email, Roles, Created, Status. Toolbar: Block, Unblock, Delete, Assign/Remove Role.

### 9.2 Localization

- `Resources/SharedResource.resx` — English strings.
- `Resources/SharedResource.pl.resx` — Polish (or other second language).
- Inject `IStringLocalizer<SharedResource>` in components. Replace hardcoded UI strings with `[nameof]` lookups.
- Persist language choice on ApplicationUser.PreferredLanguage.

**Verify:** Admin can manage users. UI switches language. Preference persists.

---

## Phase 10: Polish & Hardening

1. Audit all queries for SELECT * — ensure explicit column selection.
2. Verify no N+1 queries — use `.Include()` / `.AsSplitQuery()`.
3. Test optimistic locking conflict flow end-to-end (profile auto-save, position edit, attribute edit).
4. Test cascade deletes (delete AttributeDefinition → values cascade; delete Position → rules/tags/cv records/discussions cascade).
5. Verify all hard constraints: no per-row buttons, table views only, no JSON blobs, no dynamic tables.
6. Responsive design audit (mobile).
7. Error handling: toast notifications for conflicts, validation errors.
8. Security audit: role-based access on all pages, anonymous access rules.

---

## Verification Commands

```bash
dotnet build                                          # zero errors
dotnet ef database update                             # all tables created
dotnet run                                            # app starts
# Manual testing:
# 1. Register → auto-assigned Candidate role
# 2. Login as admin → assign Recruiter role to test user
# 3. As Recruiter: create attributes, create position with attribute rules
# 4. As Candidate: fill profile, create CV for position, publish
# 5. As Recruiter: view CV, like CV, search for CV
# 6. Both: post in discussion, verify real-time
# 7. Toggle theme, switch language, verify persistence
```
