# Next: multi-tenancy step 6, owner features (see "Multi-tenancy notes" near the end)

Claude writes features and Mike reviews them, as on the Postgres port and the blog posts.

## Where this stands — end of 2026-09-25

**All committed by Mike:** the schema per site (`c9cf08f`) and 5d, "My websites" (`61f312a`),
both browser-checked. 550 tests. Nothing is deployed.

**Start of next session: step 6, owner features.** Three parts: inviting admins, handing over
ownership, and deleting a site with its 30-day grace period. Already decided (multi-tenancy notes):
one owner per site, any number of admins who edit content only; memberships live in the platform's
`site_members` (role 1 owner, 2 admin; a unique index allows one owner per site); ownership goes
only to one of the site's admins; deleting a site or an owner's account waits 30 days before
anything is erased, tested with `FakeTimeProvider`; an owner who deletes their account takes their
sites with them. Design the pages with Mike before writing routes. Questions to settle first:
1. **Order.** Invites first is likely: the other two need an admin to exist, and the site_members
   table has no way to add one yet (`add_site` only adds the owner).
2. **Where an owner manages admins:** a page in the site's own admin (`/admin/…` on the site's
   host, behind a new owner-only policy next to `SitePolicies.Admin`), or on the platform beside
   My websites.
3. **Invites:** an emailed link with a random token stored hashed, as sign-up codes are; how long
   it lasts; revoking one; what happens when the email has no account yet (register, confirm,
   then accept), and whether an invite is tied to the email or to whoever holds the link. Removing
   an admin, and an admin leaving a site.
4. **Deleting a site:** what visitors and admins see during the 30 days (a 404, or a "closed"
   page), whether the owner can undo it, what runs at the end (`DROP SCHEMA … CASCADE`, the image
   folders, the hosts) and what runs it (a background service like the orphaned image sweep). A
   freed host can be signed up for again, which is the subdomain takeover risk noted in the
   multi-tenancy notes for custom domains.
5. **Deleting an account:** Identity's own "Delete personal data" page exists but doesn't know
   about sites; it would need to start the grace period for the account's owned sites and remove
   its admin memberships.

Deferred still: a profanity filter on site names; choosing the email provider (at deploy);
dropping orphan schemas; the review follow-ups under the multi-tenancy notes (operator role
removal isn't immediate, expired codes never deleted, subdomains are "same-site" with the
platform); one sign-in across subdomain sites via the cookie's domain (not decided).

## Earlier: 5d, "My websites", 2026-09-25 (committed `61f312a`)

**Committed by Mike (`61f312a`), 550 tests; browser check passed.**

**Agreed with Mike:** a "My websites" page on the platform, a table, and sign-up lands there rather
than on the new site's sign-in. An account is platform-wide (one Identity user), so the platform
session can list its sites from `site_members`; only the sign-in cookie is per host, so the page
says each website has its own sign-in with the same email and password.

**What changed:**
- **`/sites`** (`Pages/Platform/MySites/MySitesPage.razor`), platform only, `[Authorize]`: address
  (links to the site's home), role, and Manage (the site's `/admin`, which sends anyone not signed in there to its sign-in first; Mike, 2026-09-25).
  Sorted by address on the page; none says "You don't have a website yet." Both list and empty
  state link to `/signup`.
- **"My websites"** in the platform's top bar for anyone signed in (`PlatformNavMenu`).
- **Data:** `get_member_sites(user_id)` (site id, main host, role) and
  `SiteRepository.GetForMemberAsync`, returning `MemberSite` (`Domain/Sites`).
- **Links:** `PageUrls.SiteHome` and `PageUrls.SiteAdmin` build a site's address on the platform
  page's scheme and port; they replace `SignUpPage`'s own sign-in link.
- **Sign-up** redirects to `/sites`; its form is now enhanced like the others, since success no
  longer leaves for another host. `SiteSignUpResult.Made` carries nothing now.
- Tests: `App/MySitesTests`, a repository test, and the sign-up test's redirect.
- Seen while testing, not new: the whole-app tests log "An error occurred using the connection to
  database 'artist_shop_tests_app_identity'": EF's first connection to the Identity database the
  run just dropped, before it makes it. Present on the committed code too.

**Mike's browser check:** `./dev.sh`, sign in on `http://localhost:5176`, then "My websites" in the
bar: `site1.localhost` as Owner, its address opens its home, Manage opens its admin (its sign-in first
if you aren't signed in there). Sign up for another website with a code from `/operator`: it lands on My websites with the
new site listed. An account with no websites sees the empty message. **Passed (Mike, 2026-09-25)**; Manage then
changed from the site's sign-in to its `/admin`, at Mike's request.

**Later, noted for Mike (not decided):** one sign-in for the platform and every subdomain site by
setting the cookie's domain to `.artshop.mikesilverman.net`; it wouldn't reach custom domains
(step 7).

## Earlier: schema per site built, 2026-09-25 (committed `c9cf08f`)

**Uncommitted, 545 tests pass; Mike's browser check passed (2026-09-25).** Committed before it: 5c is `6edf69c`, and the schema-per-site
decision is `74cf711`. Nothing is deployed.

**Settled with Mike before building:** the site schemas live in the platform database; Identity
stays a database of its own for now; the app sets `search_path` as it opens a connection rather
than qualifying each call; the shared types go in a `site_types` schema.

**What changed:**
- **Platform `Scripts/0004_CreateSiteTypes.sql`** makes `site_types`: the three composite input
  types, and the two collations as well, since a collation belongs to a schema just as a type does
  and one of the types uses one. The site scripts lost their collations and their `0002`
  (`0003_CreatePosts` is now `0002`); nothing is deployed, so that's an edit, not a migration.
- **A site is the schema `site_<id>`** (`Database/SiteSchema.cs`). `SiteSchemas.Migrate` makes it and
  runs its migrations with `Search Path=site_<id>, site_types` and pooling off (each site's string
  differs, and Npgsql keeps a pool per string). DbUp's journal is in the site's schema.
- **One pool for every site** (`SiteDataSource`, keyed `"sites"` in `Program.cs`): the platform's
  connection string with `Search Path=site_types` and `MaximumPoolSize` 20 (was 5 per site; 20 is
  the plan's figure). `SiteDatabase.OpenConnectionAsync` opens from it and sets `search_path` to
  the site's schema. Repositories take a `SiteDatabase`; their SQL is unchanged.
- **A forgotten schema fails loudly:** Npgsql wipes session settings as a connection returns to the
  pool, which puts `search_path` back to `site_types`, where no site function is. Tested
  (`SiteProvisionerTests.AConnectionOpenedWithoutASiteFindsNoSitesFunctions`); with
  `NoResetOnClose` on, that test fails, so it guards what it says.
- **Not done: pinning each function's `search_path`.** The plan had it to guard a schema-qualified
  call made with another site's `search_path`; nothing qualifies calls, and a pinned function can't
  be inlined, so it cost something for nothing.
- **A site is made ready before its row is added** (Mike's idea): `SiteProvisioner.CreateAsync`
  reserves an id (`reserve_site_id`), makes the schema and folders, then adds the row (`add_site`
  or `add_site_with_sign_up_code`, both now taking the id); on any failure it drops the schema. So a
  listed site is always ready, and a failed sign-up leaves its code unused. Sign-up first checks
  the code (`sign_up_code_is_usable`) and the name (`HostDirectory`) without writing, so the usual
  mistakes don't make a schema only to drop it. Tested
  (`SiteProvisionerTests.ASiteThatCantBeAddedLeavesNoSchema`).
- **Gotcha found while testing in dev:** DbUp 7 reads `Search Path` from the connection string and
  takes the whole value as one schema's name, so it made an empty schema called
  `site_1, site_types`. `SchemaMigrator.Upgrade` now names the schema to DbUp. The tests didn't
  catch this; only dev's schema list showed it.
- Checked: Npgsql doesn't load each table's own type unless told to (`LoadTableComposites` is off
  by default), so the worry about loading tens of thousands of types at startup doesn't apply.
- The tests use one database (`artist_shop_tests`) for the platform, `site_types` and a site schema
  of their own; `TestApp` keeps its own platform database. The `SiteDatabaseNamePrefix` setting is
  gone. The platform tests add rows with `SiteRepositoryTestExtensions`.
- **Dev was reset:** its five site databases (all empty but three filler posts on site 2) and its
  platform database were dropped, so the sign-up codes went too. Site 1 was made again with
  `tools/add-site` (`site1.localhost`) and answers.

**Mike's browser check (passed 2026-09-25):** `./dev.sh`, then `http://site1.localhost:5176` and its admin; make a code
on `/operator`, sign up for a new site on `http://localhost:5176/signup` and reach its admin; try a
used code and a taken name.

**Fixed 2026-09-25 while reviewing 5c, also uncommitted:** registering again with an email whose
account was never confirmed sent "You already have an account", whose sign-in and reset links both
refuse an unconfirmed account. It now sends a new confirmation link (`AccountRegistration`), with a
test in `RegisterTests`.

**Then 5d**, "My sites". Deferred: a profanity filter on site names; choosing the email provider
(at deploy); dropping orphan schemas (a crash between making a schema and adding its row leaves
one, harmless since ids aren't reused).

## Earlier: step 5c built (reworked), 2026-09-25

**Committed by Mike (`6edf69c`), 542 tests; Mike's browser check passed.** 5c as first built took an email and password on `/signup`, which
told anyone holding a code whether an email had an account (Mike). Reworked the same day: real
email, a register page that never says whether an email has an account, and `/signup` behind
sign-in. Details under step 5c in the build order. Checked by Claude: the whole-app tests, and one
real email through MailKit to Mailpit.

**Mike's browser check:** restart `dev.sh` (the running app predates these changes; it starts
Mailpit with Postgres, or `docker compose up -d artist-shop-mailpit`). Then on
`http://localhost:5176`:
1. Log in page → "Make an account" → register a new email. It lands on "Check your email"; the
   email is at `http://localhost:8025`. Its link confirms the account; sign in.
2. Register `mike@example.com` again: the same "Check your email" page, and Mailpit has "You
   already have an account" instead.
3. Make a code on `/operator` (as the operator), then as the new account go to `/signup`: code and
   name only. It lands on the new site's sign-in, then its empty admin.
4. `/signup` when signed out goes to the log in page.

**Then**: the switch to a schema per site with one shared connection pool (decided with Mike
2026-09-25, not built; "Schema per site" in the multi-tenancy notes), and 5d, "My sites"; which
first is Mike's call. Deferred: a profanity filter on site names (Mike, 2026-09-25: later; the
popular library, Profanity.Detector, matches substrings and refused 429 of the 9,474 most common
English words, so it would be a list of our own, blocked anywhere for unambiguous words and only as
a whole hyphen-separated part for short ones); choosing the email provider (at deploy).

## Earlier: review of steps 4, 5a and 5b, 2026-09-25

**Committed by Mike (`107470c`), 508 tests.** A review of the last session's work (steps 4, 5a and 5b, all
committed by Mike: `1b6a222`, `721591c`, `82d22da`) found no bugs in them; it changed:
- `HostDirectory.ReloadAsync` runs one at a time (a `SemaphoreSlim`, .NET's lock that can be held
  across an `await`). Without it, two sign-ups at once could leave the newer site's host out of
  memory until a restart. `HostDirectory` now takes the loading function
  (`SiteRepository.GetHostsAsync`) rather than the repository, so
  `AnEarlierReloadDoesntUndoALaterOne` can hold one load part way through; that test fails with the
  lock taken out.
- Tests that run the whole app: `tests/.../App/TestApp.cs`, ASP.NET's `WebApplicationFactory`
  (package `Microsoft.AspNetCore.Mvc.Testing`), with its own databases
  (`artist_shop_tests_app_platform`, `artist_shop_tests_app_identity`, sites
  `artist_shop_tests_site_app_*`, all dropped at the start of each run) and hosts `platform.test`
  and `site1.test`. `HostRoutingTests` covers unknown hosts, `ServedOn` for pages and endpoints, and
  the host being checked before sign-in; `PlatformOperatorTests` covers `SyncAsync`. For this the
  app reads a new optional setting, `SiteDatabaseNamePrefix` (only the tests set it).
- "Make code" redirects after its post, like the other add forms, so a browser refresh can't make
  a second code. The code rides the redirect in a cookie encrypted with Data Protection
  (`MadeSignUpCodeCookie`: path `/operator`, one minute, deleted once read).
- Notes added under the build order ("Follow-ups from the 2026-09-25 review"): operator role
  removal and Identity's cookies, expired codes, and site subdomains being "same-site" with the
  platform.

**Mike's browser check:** make a code on `http://localhost:5176/operator`: it shows once; refresh
the page and it's gone, with no second code in the list.

**Then 5c**, sign-up with a code. The design is under step 5; settle the failure case (account
made, site not) and the name rules with Mike before building. 5c is the first caller of
`HostDirectory.ReloadAsync` after startup. Dev: the platform at `localhost` (Mike is its
operator), site 1 at `site1.localhost`, site 2 at `site2.localhost` (filler posts), both owned by
`mike@example.com`.

## Where this stands — end of 2026-09-24 (multi-tenancy session)

**All committed by Mike** (`9c56bea`), 459 tests. The session reviewed the previous one's work
(fixes in `1705adf`), designed multi-tenancy with Mike (decisions and the 7-step build order are in
"Multi-tenancy notes"), and built steps 1–3: the app's own Postgres role, per-site image folders,
and the platform database with one database per site, found by host. Mike's browser check of two
sites in dev passed. Dev now has site 1 (`site1.localhost`, `localhost`, empty) and site 2
(`site2.localhost`, 3 filler posts, a series, uploads). Nothing deployed: the VPS needs steps 1
and 3's notes applied together, from a fresh Postgres volume.

**Start of next session: step 4.** Agree its design with Mike before building. Open questions:
memberships `(user, site, role)` in the platform database (Identity's user id, no foreign key
across databases); owner vs admin checks replacing `RoleNames.Admin` on every admin page and
endpoint; what the platform operator is (Mike's global role, replacing the seeded admin) and what it
may do before step 5's dashboard; what `IdentitySeeder` becomes; and how the first site gets its
owner while startup still creates it.

## Earlier: end of the blog sessions, 2026-09-24

**All committed by Mike** (`75f4182`); 431 tests pass. Since `4e30dbe`: review fixes to the image
embed, "Mentioned in" on the artwork page, the public `/posts` list (headed Blog) with its filler-post
seeder, and the move to .NET 11 RC1 with a chiseled runtime image. Details are under ".NET 11" and
"After that" below. Not yet deployed.

**Review fixes to `75f4182`** (2026-09-24, uncommitted, 432 tests): `dev.sh` kills a leftover app
under any `net*` folder (it still looked in `net10.0`); `PageLinks.ReadPageNumber` caps the page at
1,000,000, since `?page=2147483647` overflowed the offset into a 500; "Mentioned in" reads a
`PostMention` whose date isn't nullable.

**Start of next session: the choice Mike was weighing.**
- **Auth today:** Microsoft's Identity pages (login, passkeys, 2FA, password reset) and one admin
  made by `IdentitySeeder`. No Register page, `IdentityNoOpEmailSender` (reset and confirmation
  emails never go out), no external providers.
- **Claude's recommendation:** a design session on multi-tenancy first, not the build, since it
  decides how auth is shaped. Who logs in to what: each artist administers only their shop, and
  customers are per shop or per platform. That decides whether an account carries a shop id, how
  roles are checked, and what the seeder becomes. Google sign-in only returns to callback addresses
  registered with Google, so shops on their own domains need either every domain registered or
  sign-in on one central domain handing back to the shop, which is a tenancy decision.
- **Either way, and small:** real email sending, and a Postgres role for the app instead of
  `postgres` (see the multi-tenancy notes).
- The multi-tenancy notes at the end of this file predate the Postgres port: they still speak of
  SQL Server Express and `SqlConnectionFactory`. Read them with that in mind, and refresh them in
  the design session.

**Still to see in a browser:** the lightbox checkbox greying out as the size changes; previous/next
through a post's lightbox images; the Video dialog on nonsense; an artwork upload, a bulk upload and
a post image upload after the shared `upload-request.js`. Post 4, "Claude video embed check", is
published and names two swept images, so it won't save until they're removed; delete it when done.
The 25 filler posts are in the dev database (`tools/seed-blog-posts/seed.cs -- --clean` removes
them).

**Built so far (blog):** the admin post list and editor (Quill 2), drafts and publishing, the
`/posts/{slug}` page, the unsaved-edit backup, the artwork, video and uploaded image embeds,
"Mentioned in", and `/posts`.

## .NET 11 (moved to RC1 on 2026-09-24)

Moved for the enhanced-navigation fix (dotnet/aspnetcore#64015: .NET 10 scrolled to the top at the
click, before the new page arrived; Mike saw it fixed in the browser). Docker tags are pinned to
the RC1 ones. Not yet deployed.
- EF Core, Identity and Npgsql's EF provider stay on 10.0 until npgsql/efcore.pg#3913 is fixed (its
  11.0.0-rc.1 pins an EF Core build nuget.org doesn't have). Move them, and the pinned tags, at RC2
  or GA (November).
- The 11 SDK's new warnings are fixed: `ValidateAsync` in `Login.razor` and `ImportArtworks.razor`,
  and the JS interop calls BL0016 flagged catch `JSDisconnectedException` (only that, so real
  JavaScript errors still surface).
- The runtime image is `aspnet:11.0.0-rc.1-resolute-chiseled-extra`: no shell or package manager,
  ICU included (the collation comparer and slugs need it), 218 MB built. Checked with the
  rehearsal compose: login, an upload, both volumes owned by 1654, login kept across a restart.
  No `docker exec … sh` into the app container any more.
- `scroll-restoration.js` is still needed on 11 (Back/Forward still has no scroll handling); its
  comments were rechecked against 11 RC1.

## After that

1. **Built 2026-09-24**: `/posts`, headed "Blog", with a Blog nav link after Home.
   Decided with Mike: title, date and a plain-text excerpt (`PostExcerpt`, 300 characters on a
   whole word, clamped to 3 lines), published posts only, 10 per page
   (`ArtistShopLimits.BlogPageSize`) with ← Newer / Older →. `ArtworkListPaging` became the
   shared `Components/Lists/PageLinks`.
   `tools/seed-blog-posts/seed.cs` adds 25 filler posts a few days apart for the paging (`--clean`
   removes them); they're in the dev database now. `GetPublishedAsync` is gone, and
   `get_post_list` lost its published-only flag.
2. **Built 2026-09-24**: "Mentioned in" on the artwork page (`ArtworkMentions`),
   below the description, one row per published post with its date, newest first. Chosen by Mike
   over a row in the details list.
3. **Built 2026-09-24**, checked by Mike in the browser: the uploaded-image embed, `artshop-image`.
   Decided with Mike:
   - Same upload pipeline and storage keys as artwork images, but **no table**: the embed's Delta
     value holds `{ storageKey, width, height, blur, size, layout, caption, alt }`, and the orphan
     sweeper also reads image keys out of post bodies with a jsonpath (as `set_post_artworks`
     does). Nothing needs a relation to these images.
   - **Any width may be uploaded** (`ImageVariants.MinimumPostImageWidth`), never enlarged. An
     image narrower than the medium embed also gets a copy at its own width
     (`ImageVariants.WidthsFor`), so the file an embed shows is the narrower of the size and the
     image, which the page and the editor stretch to the size. Artwork uploads keep
     `MinimumSourceWidth`, so none of them has such a copy.
   - **Alt text** is a field in the toolbar, filled with the file name without its extension,
     next to an ⓘ button that shows what alt text is for. Cleared stays cleared.
   - **Drop and paste** both upload. Quill 2's `uploader` module already hands a drop (at the
     caret) and a pasted file to one `handler(range, files)`, filtered by `mimetypes`, so our
     handler replaces its data-URL one. `FileDropZone` isn't the right fit: it's a box, and its
     uploader talks to a Blazor island; its token and error-message helpers are shared now.
   - While a new image uploads, a placeholder box (`artshop-image-upload`) stands where it will go,
     showing progress or the failure (Mike, after trying a status line under Quill's toolbar,
     which was out of sight). It comes and goes as `"api"` changes, and the editor's history is
     `userOnly`, so undo only ever sees the finished image. The editor leaves placeholders out of
     the form's value, so they're never saved or backed up. Replace image shows its progress in
     the toolbar.
   - Replace image keeps size, layout, caption and alt.
   - **A deleted file** (found 2026-09-24: images added to post 4 and saved more than dev's 5
     minute grace later were swept first, which Mike wants kept as it is, since it found this):
     saving refuses a post naming an uploaded image whose original is gone
     (`PostForm.ImagesMissingFrom`, as the artwork form does), and the post page leaves such an
     image out, as it does an artwork embed whose image was deleted.
   - **Lightbox** (built 2026-09-24, not yet seen in a browser): an "Open full screen when
     clicked" checkbox in the image toolbar, off by default and disabled when the upload has no
     file wider than the size shown (`ImageVariants.LargestWidthFor`, which the script mirrors and
     a test pins). The page checks the same rule, so a ticked image that a new size or Replace
     left with nothing wider just isn't clickable. `<post-document>` (`PostDocumentView.razor.js`)
     hands every such image in the post to the existing `ImageLightbox`, so the visitor steps
     through them all. Uploaded images only: an artwork embed already links to its page.
4. Open, small: a `#…` link is still dropped by the parser without a word. Headings have no ids
   to jump to anyway.

## How the blog posts are built

**Rules.** Never render post content with `MarkupString`. No bUnit: the logic is in the parser,
which is tested; Playwright for .NET is the candidate if the editor ever needs a test. Our own
Quill format and embed names carry an `artshop-` prefix (Mike), and the keys inside an embed's
value don't. Each embed is a Quill `BlockEmbed`, never an inline `Embed`: an inline embed's line
ends in a `\n`, which the parser would read as an empty paragraph after every embed.

**Storage and reading.** The Quill Delta is stored as `jsonb` (a CHECK requires an `ops` array).
`PostDocumentParser.Parse` turns it into typed blocks (`Domain/Publishing/PostDocument.cs`) and
drops anything it doesn't know, so no HTML sanitizer is needed and pasting is safe by construction.
Links must be http, https, mailto, or a path with one leading `/` (not `//host` or `/\host`).
Storage keys and video ids are checked against their exact shape. The parser is `partial` only for
`[GeneratedRegex]`.

**Schema.** `posts` has no status column: a NULL `published_at` is a draft, and visitors see
`published_at <= now()`, so scheduling later needs only a date picker. Slugs follow the title, and
a clash is `NameAlreadyInUseException`. `set_post_artworks` rebuilds `post_and_artworks_junction`
from the saved body with a jsonpath (`$.ops[*].insert."artshop-artwork".artworkId`, whole numbers
an int can hold, exactly as the parser reads them).

**Editor** (`Components/Pages/Admin/Publishing/Posts/`):
- `PostEditor` is one static SSR page for new and edit, with Post/Redirect/Get to `?saved=true`.
  Publishing is two submit buttons posting `Input.Status`. A save of a post deleted in another tab
  keeps the writing on the page, headed "Deleted post".
- `PostBodyEditor`'s `<post-body-editor>` loads the vendored Quill 2.0.3 (`wwwroot/lib/quill/`) on
  first use and writes the Delta into a hidden input on every change. **`data-permanent`** stops
  enhanced navigation from wiping Quill's DOM after a save.
- Its styles are in `PostBodyEditor.razor.css`, because `quill.snow.css` isn't in a layer and beats
  every Tailwind utility. That's also why the writing area's `box-sizing` is set there.
- `PostBackup` keeps a localStorage copy of unsaved edits and offers Restore / Discard, Mike's
  choice over a leave-page confirm, which can't stop Blazor's Back.
- **Rule (Mike): a form editing something that exists keeps Save disabled until it differs from
  what was loaded.** Static pages use `Forms/EnableSaveOnChange` with `data-waits-for-change`;
  islands set `disabled` from C#. Known wrinkle: jsonb reorders Quill's JSON, so undoing every edit
  leaves Save enabled.
- `globals.d.ts` declares `Blazor`, `Quill` and Floating UI for `checkJs`, and `jsconfig.json` no
  longer checks `wwwroot/lib`. TypeScript proper maybe later (Mike).

**Matching the page.** `Components/Publishing/EmbedLayoutClasses` holds each layout's Tailwind
classes (`center | left | right | floatLeft | floatRight`, named by `EmbedLayoutNames`). The editor
applies the same classes, so it wraps exactly as the page will, and wrapped embeds take their own
line below `sm`. `PostColumn.WidthClass` (42rem, content-box) is the text's width on the page and
in Quill's writing area, with the page's line height. The post body is `flow-root break-words`.

**Artwork embed.** `artshop-artwork` stores `{ artworkId, storageKey, size, layout }`, and the
storage key is required. Small and medium are the 160 and 400 variants, which every image has
(`ImageUrls.EmbedVariant`). The server renders image addresses with a `{storageKey}` placeholder,
so they stay a C# concern.
- `PostArtworkEmbed.razor.js` defines the blot and its toolbar buttons: Small / Medium,
  Left / Centre / Right, Wrap text, Change image. The toolbar itself is the shared one (see "Embed
  toolbars"), a native `popover` placed by Floating UI 1.8.0, vendored in `wwwroot/lib/floating-ui/`
  (see its `SOURCE.md`); Blueprint's `BbPopover` can't run on a static page.
- **Caption:** an optional `caption` key, stored as typed and applied on every keystroke (Quill
  merges them into one undo), so no way of closing the toolbar loses it. The parser trims it and
  reads a blank one as none. The page renders `<figure>` at the image's width with a
  `<figcaption>` (`ArtworkEmbedView.CaptionClass`, which the editor gets too), so a long caption
  wraps under the image. Enter in any embed toolbar's field means Done, since the toolbar is inside
  the post's form and Enter would submit it. To watch in the browser: whether the image flickers
  while typing, since each keystroke replaces the embed.
- **The picker** is `ArtworkPickerDialog`, a `ModalDialog` with `StartsClosed` holding an `<iframe>`
  of `ArtworkPicker/` pages in `FrameLayout`:
  1. The list reuses `Catalog/Artworks/ArtworkListBrowser`, which takes `RowHref`,
     `ClearFiltersHref` and `KeptFields`. It starts at `?images=yes`.
  2. An artwork's images.
  3. `ArtworkEmbedChoice`: size and layout, or only "Use this image" in
     `ArtworkPickerMode.ChangeImage`.
- `ArtworkPickerTrail` carries the mode and the list's query string through every step, so Back
  links keep the filters. The picker's script restores the scroll position on Back.
- The frame and the editor talk through `postMessage` (`choose` or `close`, since a frame keeps
  Escape to itself). The editor only listens to its own frame at this site's origin.
- **Public:** `SinglePost` loads every embed's image in one call
  (`ArtworkRepository.GetImagesByStorageKeyAsync`, keys cast to `char(32)[]` so the unique index is
  used). `ArtworkEmbedView` links to `/artworks/{slug}?image={n}`. An embed whose image is gone is
  left out.

**Embed toolbars.** `PostEmbedToolbar` (markup) and `PostEmbedToolbar.razor.js` are shared: the
popover, Floating UI, one-undo replace, Remove and Done. Each embed kind supplies its own buttons
and an `EmbedKind` (`readValue`, `showPressed`, `onButton`). Two popovers, one per kind; opening one
closes the other.

**Video embed.** `artshop-video` stores `{ provider, videoId, hash?, layout }`, where `provider` is
`youtube` or `vimeo` and `hash` is the second part of an unlisted Vimeo link. The parser checks each
part's exact shape and drops the video otherwise (`VideoEmbedBlock` over `YouTubeVideo` /
`VimeoVideo`). `VideoPlayerUrls` builds the player addresses, `youtube-nocookie.com` and Vimeo with
`dnt=1`, and hands the editor templates with placeholders.
- A video fills the column, or is a medium artwork's width with text wrapped beside it
  (`EmbedLayoutClasses.VideoFor`, the width from `ImageVariants` through a CSS variable). So its toolbar has Full width / Wrap left / Wrap right, not
  left and right on a line of their own.
- In the editor, the live player sits under a transparent cover, so a click opens the toolbar.
- The toolbar shows the video's own page as a link (opens a new tab) and **Change video**, which
  opens the address dialog with that link filled in and selected, and keeps the layout.
  `VideoUrls` builds both the player and the page addresses. Not an editable field in the toolbar:
  it sits inside the post's form, where Enter would submit the post.
- `VideoAddressDialog` is outside the post's `<form>`, in a `method="dialog"` form of its own, so
  Enter in its field can never submit the post. The editor finds it by id when opening it. Its
  script sends the link to `GET /admin/video-link` (`VideoLinkEndpoints`), and
  `VideoSources.FromLink` reads watch, `youtu.be`, `shorts/`, `embed/`, `live/`,
  `vimeo.com/{id}[/{hash}]`, channel and showcase links and `player.vimeo.com/video/{id}?h=`, with
  tests. It answers with the embed's parts, or 404, and the dialog says so. `VideoSources` also
  holds the shape checks the parser uses, so there's one copy of each pattern.
- The public iframe sets `referrerpolicy="strict-origin-when-cross-origin"`: YouTube's player
  refuses to play without a referrer.

# Todo: interactive image upload on the add-artwork form

Goal: drop or pick multiple images on `/admin/catalog/artworks/add`, watch each one upload,
reorder them by dragging, star one as primary, and have them saved with the artwork.

Approach: the page stays static SSR. One `InteractiveServer` island owns the image list.
Bytes go to a separate HTTP endpoint via XHR (not over the circuit). The island and the
form talk through hidden inputs inside the existing `<EditForm>`.

## Where this stands — 2026-09-21, end of day: porting to Postgres

**SQL Server is being replaced by Postgres 18, on the `postgresPort` branch.** `main` keeps the
SQL Server version. The reason: the VPS has 2 GB of RAM, and SQL Server on Linux won't start in
less than that. Azure SQL's free serverless tier allows about 110 awake minutes a day, and each
wake-up makes a visitor wait about a minute. The phased plan is at
`~/.claude/plans/refactored-percolating-backus.md`. **Read it before starting.** Claude writes the
port and Mike reviews it, with every place Postgres behaves differently called out. Names are
snake_case.

**Status: phases 1–4 are done. Only Phase 5 (deploy to the VPS) is left.**
- Phase 1 is committed ("phase 1"). Phases 2–4 are **uncommitted** on `postgresPort`.
- All 259 tests pass on Postgres.
- The app boots on an empty Postgres, creating both databases and the admin.
- Mike's browser pass found nothing broken, though he didn't try every action.
- Review fixes (2026-09-21, uncommitted): `update_artwork` locks the type before the artwork, the
  order `update_artwork_type` uses, so the two can't deadlock. `reorder_series_artworks` locks the
  series row instead of the whole junction table. `set_series_cover` raises SH006 after its last
  UPDATE, not before. New indexes on `artwork_images (artwork_id)` and the term junction's
  `term_id`, added to 0001, so the dev database needs recreating to get them.

**Things to know before touching it:**
- **Run `. ./env.sh` in every terminal before `dotnet test` or `dev.sh`.** A terminal from before the
  switch still holds the SQL Server connection string. Npgsql then logs in as `sa`, and reads
  `localhost,1433` as two hosts, one of them 0.0.5.153. Every test fails, because the database
  fixture belongs to the whole assembly.
- Dev images are throwaway test data (the real files are kept outside the app), so the orphan
  sweeper clearing `content/images` in dev is fine.
- Dev Postgres is `artist-shop-postgres` on host port **5434** (5432 and 5433 are speed-dungeon's
  and snowauth's). `POSTGRES_PASSWORD` is in `.env`. `artist-shop-mssql` can stay stopped; its
  volume is kept for `main`.
- Tests: `. ./env.sh`, then `dotnet test` (not `--nologo`, which runs none), or run the built
  binary `tests/ArtistShop.Web.Tests/bin/Debug/net10.0/ArtistShop.Web.Tests` (`-class` to pick one).

**How the port works**, for review and for anyone changing SQL from here on:
- **Schema** (`Database/Scripts/0001`, `0002`):
  - Two ICU collations, applied per column. `case_insensitive` (`und-u-ks-level2`) is on every
    unique name, on artwork names, and on product labels. `case_and_accent_insensitive`
    (`und-u-ks-level1`) is only for the title search's `LIKE`, which needs Postgres 18.
  - The `sort_order` UNIQUEs are `DEFERRABLE`. Postgres checks a plain UNIQUE after each row, so a
    one-statement swap would fail without it.
  - Products use `UNIQUE NULLS NOT DISTINCT`.
  - `created_at` defaults to `clock_timestamp()`, not `now()`, so artworks from one CSV import get
    different times.
  - Constraint names are cut to fit Postgres's 63-byte limit.
  - The table types became three composite types, mapped to C# records in `Database/InputTypes.cs`
    by `ShopDataSource.Create`. The app and the tests both build their data source with it.
- **Functions** (`Database/Procedures/`, one function per old procedure, same file names):
  - Every procedure became a function, because a Postgres procedure can't return rows.
  - Repositories call them as text: `SELECT * FROM f(@A)` for a table result, `SELECT f(@A)` for a
    single value or none. Npgsql turns `CommandType.StoredProcedure` into `CALL`, which only works
    for procedures.
  - An old multi-result procedure is several functions, run as several `SELECT`s in one command.
  - Errors are `RAISE … USING ERRCODE = 'SH0nn'`, from `SqlStates`. Unique violations are matched on
    `ConstraintName`.
- **Locks.** A function can't set its own isolation level, so each `SERIALIZABLE`/`UPDLOCK` became a
  specific lock:
  - `FOR SHARE` on the artwork type row.
  - `FOR KEY SHARE` on the chosen terms, series and product types.
  - An advisory lock for choosing a slug.
  - Series rows `FOR NO KEY UPDATE`, in id order, before appending to or reordering a series.
  - `LOCK TABLE series` for the `MAX(sort_order)` of a new series.
  - `set_series_cover` is two UPDATEs, because a partial unique index can't be deferred.
- **Traps:**
  - A plpgsql `RETURN QUERY` must return exactly the declared types, and its `RETURNS TABLE`
    columns are variables, so every column in its queries names its table.
  - A `LANGUAGE sql` function checks its body when it's created, so it may only call functions
    from files that sort before it.
  - `strpos`, `replace` and regular expressions refuse a non-deterministic collation.
  - NULLs sort last ascending and first descending, the reverse of SQL Server.
  - Npgsql 10 reads `date` as `DateOnly`. `DateOnlyTypeHandler` only exists because Dapper won't
    accept the type without one.
- **Identity:**
  - EF migrates at startup, which also creates the database, so there's no manual
    `dotnet ef database update`.
  - `IdentitySeeder` runs in every environment. It makes the `Admin:Email` account an admin, and
    reads `Admin:Password` only when it has to create that account. Dev sets both in `env.sh`.
  - The old SQL Server filter on the unique email index is gone, since Postgres lets NULLs through
    a UNIQUE anyway.

**Next: commit phases 2–4, then Phase 5**, the VPS deploy. The plan's Phase 5 section has the steps:
1. Measure on the VPS (`free -m`, `docker stats --no-stream`, `ss -ltn`).
2. Write `build-and-push.sh`, then rewrite `docker-compose.production.yml`, which is still SQL
   Server.
3. Add a Postgres container and the web container on `127.0.0.1:8089`, with the image bind mount.
4. Set memory limits from the step 1 numbers.
5. Add the nginx block (with the websocket headers), then run certbot.
6. Seed the first admin through `Admin__Email` / `Admin__Password`, then remove the password line.

The domain is still undecided. Two follow-ups:
- `docker-compose.production.yml`'s header comment says the seeder only runs in Development. That's
  no longer true; fix it as part of Phase 5.
- In production, a database restored without its images, or pointed at the wrong image folder,
  would have its files swept as orphans. Decide at deploy time whether the sweeper should refuse
  to run when the database has no image rows but the folder has files.

## Where this stands — 2026-09-21, earlier

**The artwork page is built.** `/artworks/{slug}` is the big image, a thumbnail picker, a
full-screen view, and the panel of everything the work carries. It was done in four slices, each
one working before the next began, and everything below this section is history.

**The panel** (`ArtworkDetails`) shows the title, the work type, the products and their prices,
then date, size, duration, the vocabulary terms grouped under their vocabulary, and the series it
belongs to, linked. Only rows with a value appear — a field the work type has switched off is null
on the artwork anyway, so "show what is filled in" needs no knowledge of the type's fields.
Prices read `$1,200` through `Utilities/PriceText`. **Mike, 2026-09-21: a price is an amount in a
currency, the way a dimension is centimetres, and ought to be shown in the visitor's own. That is
its own session.** `PriceText` is the one place it is spelled, so that work has a single owner.
Products can still only be created by the CSV import — neither artwork form posts them and there
is no products admin, which is the next real gap on the commerce side.

**The images.** The stage keeps the shape of the artwork's *tallest* image, worked out on the
server, so clicking through the picker can never move the page; each picture sits in the middle of
it at its own shape, with its blur behind it, and the bars either side belong to the frame.
`TileImage` grew `IsEager` and `IsUncropped` for it. Every image is rendered into the stage hidden,
so the browser fetches one the first time it is shown and a repeat view is a repaint.

**The addresses.** `ArtworkPageQuery` owns the page's whole query string — `series` and `image` —
and builds every link to it: the series tiles, the picker, and the neighbour links. Images count
from one in the address and from zero everywhere else, converted at exactly two points. A thumbnail
is a link to *this page showing that image*, never to the image file: with the script it is caught
and swapped in place, and `history.replaceState` keeps the address accurate without stacking
history entries, so it can still be copied and sent.

**The full-screen view** is `Components/Dialogs/ImageLightbox`, not `ModalDialog` — that one opens
as soon as it is rendered and carries a title bar and padding a picture has no use for. It knows
nothing about artworks: it is handed an array of `<img>` and an index, and reports where it moved
with a bubbling `lightboxchange` event, so the gallery and the page follow it. Its picture copies
the `srcset` off the page's image rather than building one, so nothing downloads twice.
`LightboxTrigger` makes whatever should open it into a `<button>` — the picture's frame itself,
not a separate control — so the keyboard reaches it too. app.css gives it the zoom cursor only when
`html:has(image-lightbox:defined)`, asked of the document rather than tracked in a flag, because an
enhanced navigation connects the incoming lightbox and disconnects the outgoing one in whichever
order it likes. The gallery reads which image is showing off the page when it opens the lightbox,
never from a field: enhanced navigation patches `<artwork-gallery>` in place without connecting it
again, so a stored index survived into the next artwork (found and fixed 2026-09-21).

**Who owns left and right.** The lightbox owns them while it is open; the page owns them otherwise,
through `wwwroot/js/arrow-key-links.js`, which follows whichever links carry
`data-arrow-key-link`. It ignores held modifiers, anything typed in a field, and any moment a
`dialog[open]` exists. It clicks the link rather than setting the location, so enhanced navigation
patches the page as for any internal link.

**Moving through a series.** `dbo.GetArtworkNeighboursInSeries` filters the series to the places
a visitor may be sent once, in a CTE, then anchors on the artwork's own junction row and takes the
nearest of them either side with two `OUTER APPLY`s. `UNIQUE (SeriesId,
SortOrder)` is what makes strict `<` and `>` safe. `@OnlyArtworksWithImages` keeps the visitor rule
a parameter, as `GetSeriesWithCovers` does. The page falls back to the artwork's first series by
name (through `SeriesOrder`) when it was reached cold, so the arrows work from a shared link. An
artwork with no image still renders for anyone with its address, deliberately: an artist who lands
there sees an incomplete work to fix rather than a 404 for something that exists.

**Addresses have one spelling each.** `ArtworkPageQuery.Url` for the public artwork page and
`Components/PageUrls` for the series page and the artwork edit page; nothing else writes those
strings but the pages' own `@page` lines.
`tests/.../ArtworkNeighboursTests.cs` covers it, including a reordered series — **not yet run.**

### Next session: a first deployment (Mike, 2026-09-21)

The app in docker compose on the VPS, the database on **Azure SQL's free tier** (account set up
already), and the images on the VPS filesystem, reached from inside the container. The VPS has
2 GB of RAM and SQL Server in a container is what eats it, so renting the database out is what
makes the rest fit. See `deployment-notes.md`, and the notes on auto-pause, collation at creation
time, the firewall and the bind mount before starting.

If the free tier's shape doesn't suit the app, Mike's fallback is moving off T-SQL altogether — so
the first question is only whether it deploys at all, and it isn't worth sinking time into
Azure-specific tuning before that is answered.

### Worth doing next

- **A products admin**, so prices exist for work that wasn't imported.
- **The public artwork list**, the "search everything" way in. The admin list's query already takes
  every filter as a parameter; what's missing is a public page and a decision about search.
- The `@TODO` on `ImageVariants.Widths` is now answerable: the stage asks for
  `(min-width: 1200px) 760px, (min-width: 1024px) 63vw, 100vw`, so 1600 is the widest that earns
  its place until the layout changes.
- Small: no blur behind the lightbox picture, so a first look at an unseen image is blank against
  black; long titles in the neighbour links want truncating.

## Where this stands — 2026-09-20, end of the second session that day

**A visitor can now browse the catalog.** `/` is a grid of series cards (cover, name, count),
`/series/{slug}` is that series' artworks in the order the artist dragged them, and both lead to
`/artworks/{slug}`, which is still the old stub. Everything below the admin list in this file is
history; this section and the next are what the next session needs.

**The public rules, decided by Mike:** a visitor sees an artwork only when it has an image, and
sees it whether or not anything is for sale. A series with no cover — which is the same as a
series none of whose artworks are photographed — is hidden, and a card's count follows the same
rule, so it can't promise more than the page it opens. Artworks in no series stay unreachable
until there is a public version of the artwork list to link to.

The artist still sees everything, so those rules are parameters, never baked in.
`dbo.GetSeriesWithCovers` takes `@OnlyArtworksWithImages`, and `SeriesRepository` names the two
audiences: `GetAllWithCoversAsync` for the artist, `GetVisibleWithCoversAsync` for a visitor. The
series page calls `GetArtworkList` with `HasImages: true, IsForSale: null`, so the policy is one
filter object on one line.

`ArtworkListSort.SeriesOrder` was added for it: every other sort reads a column on `dbo.Artworks`,
so without it the public page would have thrown away the artist's dragging and the starred cover.
It reads `junction.SortOrder` for the chosen series through an `OUTER APPLY`, means nothing with
no series chosen, and the admin filter bar offers it only when a series is picked.
`ArtworkListItem` gained `Slug`, since public links need it, and `dbo.GetSeriesBySlug` is new.

**Shared, in `Components/Catalog/`:** `TileImage` (the box, the blur behind it while it loads, a
`srcset` over the variants the image actually has, AVIF), `TileGrid`, `LinkTile`, and
`ArtworkListQuery` + `ArtworkListPaging`, which moved out of the admin folder when the series page
became their second user. `SeriesGrid` is a section component, so the artist's name and anything
else goes above it in `Home.razor` without touching the grid. A masonry mode is a second grid
component and a different `BoxClass`; nothing else changes.

`ArtworkThumbnail` is now six lines over `TileImage` — once the blur went in, the admin thumbnail
and a browse tile differed only in width. It keeps its name and its `Class` default, and stays the
one place that names `AdminThumbnailWidth`.

## Next: the artwork page at `/artworks/{slug}` (Mike, 2026-09-20)

The stub there today is two `<h1>`s and an unstyled blur image. What it should be:

- **The image, large.** `ImageVariants.Widths` tops out at 1600, and the `@TODO` on that array —
  measure the real element and derive the widths — is finally answerable once this page is laid out.
- **A picker under it:** the artwork's other images as thumbnails, the size the admin sees.
- **Clicking the main image fills the screen**, and left and right move through that artwork's
  images. `ModalDialog` is the obvious start, but `.artist-shop-modal` is `max-h-[85vh]` with
  padding, so a full-bleed variant is needed.
- **Left and right on the page itself** go to the previous and next artwork **in the series**. An
  artwork can be in several series, so the page has to be told which one it was reached through —
  a query parameter from the series page is the obvious answer, with a fallback when someone
  arrives at the address cold. Decide that before writing the page.
- **A panel beside or under it** holding the title, the products and their prices, the date
  created, the dimensions, the description, and every vocabulary term the artwork carries. Only
  what the work type has switched on: a photograph with no depth shouldn't show an empty row.
  Worth deciding at the same time: the work type itself, which series it belongs to (linked), the
  duration for time-based work, and what a product row says when it is sold out or has no price.

Arrow keys mean a script, since the page is static SSR — a custom element in the house style, the
way `<modal-dialog>` and `<submit-on-change>` work.

### Also done 2026-09-20, after the admin list

**A review pass over the admin list**, all of it applied: the primary image is read the one way
(`IsPrimary = 1`, as the series procedures do) rather than by a second rule; the image count and
the for-sale test are worked out once in a `CROSS APPLY` instead of twice each; the sort numbers
are named constants like `GetArtworkNameMatches` does; `dbo.GetVocabulariesWithTerms` absorbed the
per-type copy through a nullable `@ArtworkTypeId`; vocabulary **terms** are sorted by name on the
filter bar, which they weren't, through the shared `VocabularyOrder`; `CheckboxGroup` and
`YesNoSelect` in `Components/Forms/` took about 70 lines of repeated markup out of
`ArtworkListFilters`; `.artist-shop-field-label` is one owner for the label styling.
`ArtworkListQuery.Read` is called with named arguments — six of its eight parameters are strings.

**Back after a CSV import no longer says "Document Expired".** Both import forms now post with
`Enhance`. Blazor's enhanced submit keeps a multipart body (`body = FormData`, so the file still
arrives) and pushes a history entry only for GET, so the review stops being a posted document that
the antiforgery headers forbid the browser to redisplay. The dialog still opens because
`<modal-dialog>` runs `showModal()` from `connectedCallback` and the review is rendered only when
there is a plan, so the enhanced patch *inserts* the element. That is what the comment on
`ModalDialog` now says: it opens when it arrives as a new element, however the page got there.

**The import's "already in the catalog" is per work type now.** It loaded every name in the
catalog, which the database never required (only `Slug` is unique) and which the bulk image
uploader never did either — it matches within a type. So a photograph and a screenshot can share a
title, while a repeat inside one type is still skipped, which is what keeps the uploader
unambiguous. `dbo.GetArtworkNames` takes `@ArtworkTypeId`, the snapshot field is `TypeArtworkNames`
beside `TypeVocabularies`, and the review says "Already in the catalog (Photograph)". Note that
slugs stay globally unique, so the second `Harbour` gets `/artworks/harbour-2` silently; if
cross-type repeats become normal, the public address is the next thing to think about.

**A clicked button goes busy where the artist is looking.** `ButtonBasic` carries a hidden spinner
that `ButtonBasic.razor.js` reveals by setting `data-busy` on `event.submitter` when an enhanced
form is submitted, cleared on `enhancedload`; CSS grays it and blocks further clicks. Immediate, no
delay. It covers every enhanced form at once (both import forms, add and edit artwork, add series,
add term). Not islands — their forms go over the circuit, never raise `enhancedload`, and would
spin for good; those want `IsWorking`, as `ConfirmDialog` has. Nothing was clicked when the filter
bar submits itself, so nothing spins there, and the artwork list keeps its own fade and indicator.
**Mike, 2026-09-20: no page-level loading indicator.** I put one in `MainLayout` and it was wrong —
indications belong on the button, in the table, where people expect to find them.

### The edit page, as built on 2026-09-19

- **Shared SQL, so add and edit can't drift.** `dbo.CheckArtworkChoicesAreCurrent` (type exists, no
  value for a switched-off field, terms and series still exist), `dbo.SetArtworkVocabularyTerms`
  (replace every row) and `dbo.SetArtworkSeries` (drop unticked, append newly ticked at `MAX + 1`).
  Both setters work unchanged on a brand-new artwork, where the `DELETE`s find nothing, so
  `AddArtwork` lost about 85 lines of checks and both junction writes to them. A table-valued
  parameter can be passed straight on to a nested procedure as `READONLY`, and deferred name
  resolution means DbUp's file order in the procedures pass doesn't matter.
- **The type is not a parameter to `UpdateArtwork`**: it can't change, so the procedure reads it off
  the row it locks with `UPDLOCK`, and a missing row is error 50003.
- **Slug rule**: keep the current slug when it equals the new name's slug or that slug plus a whole
  number, so an unchanged save doesn't walk `sunset-2` along; otherwise `ResolveArtworkSlug`.
- **`DeleteArtwork` is one `DELETE`.** Every table referencing an artwork cascades, and the image
  files are left for `OrphanedImageSweeper`, the same path an abandoned upload takes.
- **Error numbers are reused, lowest first, until release** — see the comment in
  `SqlErrorNumbers.cs`. This took the freed 50003.
- **One form class, `ArtworkForm`** (was `ArtworkCatalogAdditionForm`), with `FromArtwork` to seed it
  and `ToCatalogAddition`/`ToCatalogUpdate`. `[NonEmpty]` on `Images` couldn't survive that: it would
  fail every edit save. The rule is add-only anyway — the CSV import and the bulk uploader exist to
  catalogue first and photograph later — so it moved into `AddArtwork`'s submit handler beside the
  stale-upload check. `NonEmptyAttribute` had no other user and was deleted.
- **A duration input on both forms**, and `ArtworkDuration` holds the h:mm:ss / m:ss rule that the
  CSV import already used. Without it, saving the edit form would have wiped an imported duration.
- The add page, the pickers and the setup notice moved into
  `Components/Pages/Admin/Catalog/Artworks/`, next to the edit page, matching every other admin area.

**Images on the edit page, same day.** `dbo.SetArtworkImages` joins the other three shared
procedures: the form posts the whole list in the artist's order, so it replaces every row rather
than working out which moved. Nothing refers to an `ArtworkImages` row by its `Id`, so a kept image
taking a new one costs nothing, and a removed image's file is left with no row pointing at it, which
is exactly what `OrphanedImageSweeper` collects.

- `ImagesField` takes `SelectedImages` and `SelectedPrimaryImageKey` and seeds its list from them
  once. `SelectedImage.Result` is now an `ArtworkImage` rather than the endpoint's
  `ImageUploadResult`, whose `OriginalFileName` and `BlurDataUri` are non-nullable strings while
  both columns are nullable; a fresh upload is converted to one as it completes, and from then on a
  saved image and a new one are the same thing to that component.
- **This also fixed the add page**: an add that came back with an error (a term deleted in another
  tab, say) used to redraw the island empty and lose the uploaded images, since the island renders
  the hidden inputs that carry them. Both pages now hand it `Input.ToArtworkImages()`, which is the
  artwork's images on a fresh edit page and the posted ones after a failed submit.
- `ArtworkForm.FromArtwork` sets `PrimaryImageKey` only when the main image isn't the first one, so
  the list doesn't claim the artist hand-picked an image they never touched.
- **No minimum on the edit page.** Taking every image off an artwork is allowed, because an artwork
  with no images is a normal state — the CSV import makes them that way, and the bulk uploader
  attaches to exactly those.
- The artwork names in the series page's list link to their edit pages.

**`ProductTypes.IsDefault` (2026-09-19).** A form can't name a product type: the ids are IDENTITY
values, and an unused type will be deletable once the product-type admin exists, so
`WHERE Name = 'Original'` would be a rule the database is free to break — the same reasoning that
took `ShopItemTypeId.Painting` out of the artwork types. So the row says whether it is the usual
one, `UniqueIndex_ProductTypes_Default` (filtered, `WHERE IsDefault = 1`) allows only one, and the
seed sets Original. The CSV import page now preselects it, and the products island's first row will
use the same flag. When the product-type admin is built it needs to own this: a radio per type, and
deleting the default either refused or the flag moved first.

`GetProductTypes` returns rows in no defined order, so the import page sorts them by name itself,
the way a repository's caller decides display order everywhere else here.

### What was reviewed and fixed on 2026-09-19

The 2026-09-18 work was read back and these came out of it.

- **The upload run no longer rides on one interop call.** `StartUploadAsync` awaited
  `_uploader.InvokeVoidAsync("upload", …)`, and the JS did not resolve until every file was done.
  `CircuitOptions.JSInteropDefaultCallTimeout` defaults to **one minute** and `Program.cs` does not
  change it, so a long run faulted an `@onclick` handler, which tears the circuit down. `upload` now
  launches `run` and returns; the island hears the end through `OnUploadFinished` as before. Runs of
  1:05 and 1:25 have both been measured since, so this was not hypothetical.
- **A throw in a worker no longer strands the page.** `runWorker` and `upload` had no try/catch, so
  a non-JSON 200 body would reject `Promise.all` and skip `OnUploadFinished` — leaving the island
  "uploading" for good, with no way back but a reload. `run` ends in a `finally`.
- **A dropped folder during a run is refused.** The pickers and the drop zone now sit in a
  `<fieldset disabled="@_isUploading">`; `FileDropZoneFrame` already declines a drop onto a disabled
  input. `remember()` also bails, since clearing the JS file map under the workers was the real
  damage.
- **The name pre-check applies the same length rule as the endpoint.** `ArtworkName.CanMatchAnArtwork`
  holds it once. `dbo.ArtworkNameList` is `nvarchar(200)` and SQL Server truncates rather than
  refusing, so a long file name could have matched the wrong artwork, and two long names sharing
  their first 200 characters would have violated the type's primary key — an unhandled `SqlException`
  inside a `[JSInvokable]`, so a dead circuit.
- **The report shows the file's path, not just the derived name**, and links each matched artwork.
  Duplicate names are now included in the pre-check query, grouped by `DatabaseCollationComparer`
  rather than filtered out, because the duplicate case is exactly the one rule 6 wants a link for.
- `FileDropZoneFrame` drop handler checked the loose-file input's disabled state and then took the
  directory path; it now picks the input first and guards that one.
- HEIC files get `ImageProcessor.UnsupportedHeicMessage` in the pre-check rather than being lumped
  into "not an image format we accept". `ImageUploadValidation.IsHeic` is shared with `FindProblem`.
- `ExecuteAddAsync`'s `SqlTransaction` is no longer nullable: since `AddAsync` delegates to
  `AddManyAsync`, every caller passes one.
- `CollectedFile.Path`'s comment said the path was relative to the dropped folder. It includes that
  folder's own name, which is what makes `MaximumDirectoryDepth = 2` correct.

### What was built on 2026-09-18


*Steps 6 and 7 — the browser side and the page.*
- `FileDropZoneFrame` takes an optional `DirectoryButtonLabel` and renders a second button. **One
  drop zone handles both folders and loose files**: `webkitGetAsEntry()` returns either kind, so a
  drop needs no toggle. Two buttons exist only because a `webkitdirectory` input's dialog cannot
  pick loose files. The element decides which drop path to use by looking for a folder input among
  its own children — a zone that can pick a folder can take one from a drop — and dispatches a
  bubbling `entriesdropped` carrying the entries it grabbed before the handler returned.
- `Components/Pages/Admin/Catalog/ArtworkImages/`: `UploadArtworkImages.razor` (the page, `?type=`
  preselects), `BulkImageUpload.razor` (the island), `BulkImageUpload.razor.js` (walk, metadata
  stream, upload driver), `CollectedFile`, `BulkImageFile`, `BulkImageOutcome`, `BulkImageReport`.
  The dashboard links to it per work type.
- The page has no form, so it renders `<AntiforgeryToken />` for the uploader script to find.
- The artwork type is a plain `<select>` in the island, not `SelectField`, which needs an
  `EditContext`. Changing it re-runs the pre-check against what was already dropped.
- Cap of **5,000 files**, passed to JS from the island so it is written once; the stream's byte
  allowance is derived from it and is only a backstop. Over the cap, nothing is sent and the page
  says so.
- Driver: four at a time, one bar for the whole run (bytes for files in flight, whole file once its
  answer arrives), progress throttled to four updates a second, "Processing the last images…" at
  100% with answers pending. 429/503/504 back off up to four attempts honouring `Retry-After` with
  jitter; anything else fails the file, showing the server's message only when it is short
  `text/plain`. Stop aborts and empties the queue, leaving the remaining files as "will be added" so
  Upload picks up where it left off.

**Gotcha that cost real time:** `DotNet.createJSStreamReference` is for JS **calling into** .NET and
passing a stream as an argument. When .NET calls JS and asks for an `IJSStreamReference`, it wraps
the returned value itself, so the JS function must **return the Blob directly**. Wrapping it first
fails with "Supplied value is not a typed array or blob", surfacing as a `JSException` inside the
`[JSInvokable]` and appearing only in the browser console.

### Not built on the bulk image page

- **The browser's backoff is verified, by faking the 503.** A real one needs the limiter's slot queue
  full, and on a 20-core box that is `ProcessorCount - 1` = 19 in flight plus
  `slots × (ProxyTimeout / EstimatedTimePerImage)` = 114 waiting, while the page sends four at a
  time — unreachable. So on 2026-09-18 `UploadAndAttachByNameAsync` temporarily answered 503 with
  `Retry-After: 2` to every third upload, and 150 files were run through it. **That code is removed.**
  One file ended in "Didn't upload (503)", which only happens after four attempts, so the wait, the
  retry and the attempt cap all ran; the rest retried invisibly and were added. The bar stayed smooth
  because a retry only drops that one file's bytes, under a percent of the run.
- The add-artwork form still shows a bare "Upload failed (503)." (parked since 2026-09-17). The bulk
  page now has retry and backoff worth sharing with it.
- A second image per artwork goes on through the edit artwork page (built 2026-09-19).

### Test data (2026-09-18)

`tools/generate-test-artworks/generate.sh` builds a folder of ImageMagick images and a CSV that
matches them, salted with the cases the upload page reports: a name in two series folders, a
`thumbnails` folder one level too deep, a `notes.txt`, an image with no CSV row, a series name with a
comma and one with an apostrophe. It prints the counts the page should then show, and `--clean`
removes what it made. It writes to `test-upload-files/` at the repo root, which `.gitignore` covers,
and clears that folder first. `--size 8000x6000 --format jpg` makes 48-megapixel files, to give NetVips real
work; PNG at that size is well past the 25 MB upload limit.

**(From Claude, 2026-09-19)** `--size` applies to every image, which makes generating the folder
slow. `--big N` instead leaves the run quick and gives N of the artworks a `--big-size`
(8000x6000 by default) JPEG, always JPEG whatever `--format` says. They are spread across
different series folders on purpose: the upload walks folder by folder, so big images bunched in
one folder would all arrive together instead of stretching the run out.

The existing `test-upload-files/` was not regenerated — the catalog imported from it is still
valid — so **30 of its 151 artworks, five per series, had their PNG replaced in place with a
48-megapixel JPEG** (~14 MB each). The folder is now 681 MB. `Lantern Path` was left alone so the
two-series-folder duplicate stays a small file, and the counts the page should show are unchanged:
153 images found, 3 too deep, 1 not an image.

Megapixels are what the server pays for, not file count: `ImageProcessor` decodes and then writes
three widths in two formats plus the blur. So raising `--big` is what lengthens a run; raising
`--count` adds 1200x900 files that finish almost immediately. 12000x8000 (96 megapixels) was tried
and dropped: it is inside the 100-megapixel cap, but generating two of them did not finish within
400 seconds, so building the test data costs more than the test is worth.

### Don't pass `--nologo` to `dotnet test` (From Claude, 2026-09-19)

It is forwarded to the test app, which in Microsoft Testing Platform mode then runs **zero tests**
and exits 5 without a word on stdout or stderr. It looks exactly like a broken test project.
`-v q` and `--no-build` are fine; it is `--nologo` alone.

### Enum switches are exhaustive by the compiler (2026-09-18)

`_ => throw new ArgumentOutOfRangeException(...)` was removed from all seven enum switch expressions.
It only existed to silence **CS8524**, which complains that integers with no name in the enum aren't
handled — an arm for those is dead code. Silencing it that way also silences **CS8509**, which names
an enum member no arm handles, and that one is worth having. The csproj now sets `NoWarn` for CS8524
and `WarningsAsErrors` for CS8509, so a forgotten case fails the build naming the member. Verified by
deleting a case. A value that genuinely can't occur still throws, now as `SwitchExpressionException`.

### Database tests run in sequence (2026-09-18)

`SeriesRepositoryTests.GetAllListsSeriesInTheArtistsOrder` failed about one run in four. It reads
every series, reverses the list and reorders it, and `dbo.ReorderSeries` throws 50008 when the list
isn't exactly the series that exist — so a sibling class inserting a series in between broke it. The
three new series inserts in `ArtworkRepositoryTests` made a pre-existing race likely enough to see.

**This is not the production concern it looks like.** In the app the stale list comes from an artist
pausing between loading the page and dragging, which is human time and can't be held under a lock —
that is exactly what 50008 is for, and `SeriesOrderList.razor` already catches it, sets
`_changedElsewhere` and refreshes. The guard works; only the test's assumption was wrong.

Fixed with `Database/DatabaseCollection.cs` and `[Collection(DatabaseCollection.Name)]` on the nine
classes that take `TestDatabaseFixture`, so they run in sequence with each other while the tests that
touch no database stay parallel. Six consecutive green runs; 210 tests, ~2.3s.

### Series are created by the CSV import (2026-09-18)

Requiring the artist to create every series by hand before importing was too harsh, so **an unknown
series name in the CSV is now created rather than rejecting the file**. The typo defence moved into
the review:

- **Row counts per new series.** A typo has one row where the real series has twelve. This is the
  signal that actually catches it; a flat list of names does not get read.
- **A shared slug is an error.** `ArtistShopSlug.FromName` lowercases and strips everything that is
  not a letter or digit, so "Seascapes, 1990s" and "Seascapes 1990s" both become `seascapes-1990s`.
  `Unique_Series_Slug` would refuse the second, and `SeriesRepository.AddAsync` already treats a
  slug clash as "name already in use", so the import agrees with it. This doubles as the
  near-duplicate check and is not a heuristic: it is the rule the database enforces.
- **A small edit distance is a warning** and does not block the import. `Imports/SeriesNameSimilarity`
  holds both checks plus a two-row Levenshtein written by hand rather than taking a dependency — the
  distance is twenty lines and the part that needs tuning is the threshold, which is ours either way.
  Currently distance 1 or 2 with the longer name at least 8 characters ("Blue" and "Blur" are two
  words, not one misspelt).
- A name matching an existing series is not new at all. Two rows spelling one name with different
  capitals are one series, first spelling wins. A name with no letters or digits is an error.
- `ArtworkCatalogAddition` gained `NewSeriesNames`; `ArtworkRepository.AddAsync` now delegates to
  `AddManyAsync`, so one path handles both and a duplicated try/catch went away.
- `SeriesRepository.AddManyAsync(connection, transaction, names)` is static and runs on the caller's
  transaction, so the series and the artworks are saved together or not at all. Procedure
  `dbo.AddManySeries` over `dbo.SeriesNameAndSlugList` (script `0006`, applied automatically on the
  next start). **`SortOrder` is `UNIQUE` with no gap filling**, so a batch cannot have every row read
  the same `MAX` and ask for `MAX + 1`: it uses `MAX(SortOrder) + ROW_NUMBER()` under the same
  `UPDLOCK, HOLDLOCK` that `AddSeries` uses. A name taken between review and confirm comes back as
  `ChangedSincePageLoadException`, which the page already answers by re-reviewing.

**A concern I raised here that turned out to be unfounded:** a row listing the same series twice
(`Mines; Mines`) is fine. `ArtworkImportRowReader.List` already ends with
`.Distinct(ArtworkImportNames.Comparer)`, so duplicates never reach the planner. Same for
vocabulary terms.

### Image upload, as of 2026-09-13

Steps 0-7 are done and verified against the database. You can drop or pick multiple images on
`/admin/catalog/artworks/add`, watch each upload with a real progress bar, reorder by dragging,
star one as primary, and it all persists in order with the right primary.

Step 8 is done:
- **Orphan sweep:** `OrphanedImageSweepService` (a `BackgroundService` on a `PeriodicTimer`) runs
  `OrphanedImageSweeper` at startup and then every interval. It deletes unreferenced originals
  older than the grace period. The settings are `OrphanedImageSweep:GracePeriod` and
  `Interval`: 7 days / 1 day, and 5 minutes / 1 minute in Development.
- **Submit check:** the add-artwork submit rejects images whose original the sweep already deleted.
- **Storage split:** `ImageStorage` owns the file layout (paths, save original, delete variants
  then original). `ImageUploadStore` is the upload itself, shared by the endpoint and the tests,
  and is what bulk import should call.
- **Tests:** `tests/ArtistShop.Web.Tests` (xUnit v3) runs against a separate `ArtistShopTests`
  database with `FakeTimeProvider`. Run `source env.sh && dotnet test` from the repo root with SQL
  Server up. `global.json` switches `dotnet test` to Microsoft Testing Platform mode, which
  xUnit 4 requires on the .NET 10 SDK.

**Layout.** Uploads live at `<repo>/content/images/{originals,variants}` — deliberately outside
the project directory, because `dotnet watch` treats files under the project as project changes
and refreshes the browser, which kills the SignalR circuit mid-upload. Components are under
`src/ArtistShop.Web/Components/Forms/FileUpload/`: `FileDropZone.razor` (+ its `.razor.js`) is
file-type agnostic and is what the future CSV upload should compose; `Images/` holds
`ImagesField`, `ImageUploadRow` and `SelectedImage`.

**If something looks flaky, check these before theorising.** Both bugs this session were
invisible by default:
- `appsettings.json` pins `Microsoft.AspNetCore` to `Warning`, which hides SignalR and circuit
  diagnostics. Raise `Microsoft.AspNetCore.SignalR`, `.Http.Connections` and `.Components.Server`
  to Debug in Development first.
- A `dotnet watch ⌚ Files added:` line naming your own runtime output means the watcher is
  fighting you.
- Exceeding SignalR's 32KB `MaximumReceiveMessageSize` closes the circuit with **no exception**,
  so an empty server log does not mean nothing went wrong.

## Decided: shop items don't share storage keys

`Unique_ArtworkImages_StorageKey` makes a second row for the same file a loud error. It came up
because paintings 6 and 7 once shared keys by accident (the form didn't reset after a save, fixed
with `@key="AddedSlug"`). If postcards later need to reuse a painting's photo, dropping the
constraint and adding reference counting is a deliberate schema change.

## 0. Spike the boundary — DONE

- [x] Minimal `InteractiveServer` island inside the EditForm, rendering one hardcoded hidden input
- [x] Submit; confirm it model-binds into `PaintingCatalogAdditionForm`
- [x] Island state survives a failed-validation re-render, so the image list can live in the circuit

## 1. Storage and serving — DONE

- [x] `ImageStorage:RootPath` in config, resolved against `ContentRootPath`
- [x] Split into `originals/` (never served) and `variants/` (served at `/media`)
- [x] `UseStaticFiles` + `PhysicalFileProvider` on `variants/` only
- [x] Gitignore `src/ArtistShop.Web/content/`

Decided: files never move after upload. Key is a v7 GUID, orphans get swept later.

## 2. Upload endpoint — DONE

- [x] Minimal API `POST /admin/uploads`, admin-only, one file per request
- [x] Antiforgery — ended up as a **form field**, not a header; the server checks that first
- [x] Size and content-type filter (cheap; the real check is whether libvips can decode it)
- [x] Returns `{ storageKey, width, height, blurDataUri }`

## 3. Image processing — DONE

- [x] NetVips + `NetVips.Native.linux-x64` (MIT, AVIF in the box, streams so memory stays low)
- [x] Original master saved untouched, no extension
- [x] Variants at 400 / 800 / 1600, AVIF + WebP, never upscaled
- [x] Blur data URI — 20px WebP, base64, inlined
- [x] Width/height read after `.Autorot()` so EXIF orientation can't transpose them
- [x] Failed processing deletes both the original and any partial variant directory

Widths are placeholders until the gallery and detail pages exist. Changing them later
means reprocessing `originals/`.

## 4. JS interop — DONE

- [x] Drop zone: `dragover` + `drop`, `preventDefault` or the browser navigates away
- [x] Click-to-pick via hidden `<input type="file" multiple>`
- [x] `File` objects stay in a JS `Map`; only metadata crosses the circuit
- [x] XHR per file, `request.upload` progress (fetch still has no upload progress)
- [x] C# starts each upload after its row exists, so progress can't arrive first
- [x] Listeners and the `DotNetObjectReference` cleaned up on dispose
- [x] `jsconfig.json` + JSDoc for type checking without a Node toolchain

## 5. The island component — DONE

- [x] Row per file: name, progress bar, status, real thumbnail once processed
- [x] Distinct "Processing…" state between 100% and the server finishing
- [x] Per-file error message from the server's own text
- [x] Retry a failed upload (the `File` is still held in the JS Map for this)
- [x] Remove a file
- [x] Temporary upload scaffold deleted
- [x] Split into `FileUpload/` (shared) and `FileUpload/Images/` (image-specific)
- [x] `FileDropZone<TResult>` extracted — drop zone, XHR driver, progress plumbing,
      generic only at its public API so the JSInvokable stays non-generic
- [x] `.catch` on every JS→.NET call, after two silent-rejection debugging sessions

Dropped: `IBrowserFile.RequestImageFileAsync` preview. Unnecessary — the served
variant arrives fast enough that a client-side preview earns nothing.

## 6. Reorder and primary — DONE

- [x] BlazorBlueprint.Primitives `BbSortable` (SortableJS) — touch works, unlike raw HTML5 DnD
- [x] `BbProgress` for real `role="progressbar"` semantics
- [x] Native radio group for primary — grouping is by `name`, so nesting in sortable rows is fine;
      `Filter="input, button"` stops the controls initiating a drag
- [x] ↑/↓ buttons as the keyboard path (SortableJS has no keyboard reorder)
- Primitives only: 247 bytes of CSS, so no second Tailwind build. Components' 129KB sheet not taken.

## 7. Wire into the form — VERIFIED WORKING

- [x] Indexed hidden inputs `Input.Images[n].*`; the index carries the order
- [x] Original filename captured and persisted (the bulk-CSV join key)
- [x] `ToCatalogAddition()` builds real `ShopItemImage` records
- [x] `NonEmptyAttribute` — property-level so it doesn't hit the `IValidatableObject`
      short-circuit; `[MinLength]` alone was useless because every DataAnnotations
      validator except `[Required]` treats null as valid
- [x] `ValidationMessage For="() => Input.Images"` — no field component, so no home otherwise
- [x] Confirmed in the database: SortOrder 0,1,2 with the star on the right row

### Resolved from this step

- [x] Form reset after save — `@key="AddedSlug"` on `ImagesField`. Blazor rebuilds the component
      when the key changes, which also disposes the uploader and frees the JS `File` map.
      `forceLoad` was the heavier alternative and wasn't needed.
- [x] Frozen upload bars / circuit reconnects — two causes, both fixed: the content directory sat
      inside the project so `dotnet watch` refreshed the browser mid-upload, and the blur data URI
      carried the source EXIF, blowing SignalR's 32KB receive limit.
- [x] Metadata stripped: variants keep only ICC, the blur keeps nothing and is converted to sRGB.
      Fixes a real privacy leak — public variants were carrying camera GPS.
- [x] Error bodies only rendered when `text/plain` and short, so an exception page can't paint
      itself into the UI
- [x] Test catalog and orphaned files cleared

## 8. Cleanup — DONE

- [x] Orphan sweep with a grace period, run by a background service; 4 tests
- [x] Deletion stays in the sweep, not on the ✕ button (✕ only unlinks)
- [x] Shared storage keys decided (forbidden, see above)
- [x] Expired uploads caught at submit
- [x] `MSSQL_PID=Express`. `SERVERPROPERTY('Edition')` confirmed it applied to the existing volume
- [x] Portrait variants: libvips `thumbnail` fits a `width`×`width` square unless given a height,
      so `800.webp` of a portrait photo came out 800 *tall*. Fixed with `height: source.Height`.
      Worth a test: upload `Image.Black(1000, 2000, bands: 3)` and assert `800.webp` is 800 wide
- [x] `BlurDataUri` size guard dropped. The same square rule bounds the blur at 20×20, and its
      metadata is stripped, so it stays far below the `nvarchar(1000)` column
- [x] Abort in-flight XHRs when the island is disposed, and when a row is removed mid-upload
- [x] Leftover variant folders cleared. Their originals were deleted before the delete order was
      fixed, and the sweep lists only `originals/`, so it couldn't see them
- [x] `Xmp` dropped from variants: XMP can carry its own copy of the GPS fields. Variants keep
      `Icc` only. To verify on a real photo, `grep -c GPSLatitude` on a variant should print 0

## 9. Bulk import — in progress (CSV import started 2026-09-16)

**Bulk image matching — decided 2026-09-13, revised 2026-09-17.** The CSV creates the artworks
(and their series). The artist then uploads a folder of images for **one artwork type**, and each
image's file name (without its extension) is compared to `Artworks.Name` **within that type**. A
sculpture and a painting may share a title without being ambiguous.

**Matching rules:**
1. **One image per artwork.** More images go through the edit artwork page (step 10).
2. **Names compare case-insensitively**, not as slugs. A title with a character file names can't
   hold (`/`) never matches and is reported as unmatched.
3. **Artworks that already have any image are skipped and reported.** Re-uploading the same folder
   changes nothing.
4. **Always primary**, since only artworks with no image get one.
5. **Several artworks of the type share the name:** nothing is attached; reported as ambiguous.
6. **The same name appears more than once in the upload** (two series folders, or `Sunset.jpg` and
   `sunset.png`): none of them is attached; reported with a pointer to that artwork's edit page.
7. **Unmatched files are reported.**
8. **The report is built in the page** as files finish. Lost if the tab closes.
9. **Two uploads racing for one artwork:** `AttachPrimaryImageToImagelessArtworkByName` takes `UPDLOCK` on the
   matched artworks, so the second waits and then reports "already has images". No 2601 from it.
   That holds only while this procedure is the one path adding a primary image. When the edit page
   (step 10) can add one, it takes the same lock **and** the attach repository catches 2601 and
   reports the file as skipped (Mike, 2026-09-17: do both — the lock keeps it from happening, the
   catch keeps it from being a 500 if some later path forgets the lock).

**Folders (2026-09-17).** The artist drops (or picks) one top-level folder. Files directly inside it
and inside its immediate subfolders (their series folders) count; anything deeper (thumbnails) is
ignored. Series are **not** read from folder names, because the CSV already assigned them.

**Flow, per step:**
1. **Drop.** The drop handler calls `webkitGetAsEntry()` on every `dataTransfer.items` entry before it
   returns (the list is emptied afterwards). Pickers: one for files, one with `webkitdirectory` for a
   folder (a directory picker can't pick single files); those `File`s carry `webkitRelativePath`.
2. **Walk.** Call `readEntries()` until it returns an empty batch (Chrome stops at 100 per call);
   `entry.file()` gives the `File`, `entry.fullPath` the path. Asynchronous, so the page doesn't
   freeze. `File`s stay in the JS `Map`.
3. **Metadata to the server** (path, size) as an `IJSStreamReference`: .NET calls a JS function
   returning `DotNet.createJSStreamReference(bytes of the JSON)` and reads it with
   `OpenReadStreamAsync(maxAllowedSize: …)`. The default is 512 KB (about 3,000 files), so set it.
   Not a single interop call: SignalR caps browser-to-server messages at 32 KB (about 200 files).
4. **The island (server) filters and pre-checks.** Depth rule, duplicate names (by
   `DatabaseCollationComparer`, over names from `ArtworkName.FromFileName`), then one database
   call for the type and all names, returning each name's outcome. **Only files that will attach
   get uploaded**; everything else goes straight into the report. A huge folder of mostly old work
   uploads almost nothing.
5. **Driver** uploads 3–4 at a time and starts the next when a response arrives. Courtesy only;
   the server doesn't trust it.
6. **Kestrel** limits (30 MB body, minimum data rate) apply as they already do.
7. **Per-user rate limit:** `AddRateLimiter` with a token bucket per user
   (`RateLimitPartition.GetTokenBucketLimiter`, keyed by the `NameIdentifier` claim; e.g. burst 100,
   10 per second), `UseRateLimiter()` after `UseAuthorization()`, `RequireRateLimiting` on the
   endpoints. Rejects with 429.
8. **The form is read.** One file per request. ASP.NET Core keeps files over 64 KB in a temp file
   (`ASPNETCORE_TEMP` or the system temp folder) and deletes it when the request ends.
9. **Validation** (name length, empty, size, content type), shared with `/admin/uploads`.
10. **Header only.** NetVips reads width, height and bands without decoding. Over a megapixel cap:
    400. Otherwise estimate MB = width × height × bands × a safety factor. libvips picks the decoder
    from the bytes, so a lying header can't make the decode bigger than the estimate.
11. **Processing limiter** (`Images/ImageProcessingLimiter.cs`), called from `ImageUploadStore.SaveAsync`
    so both endpoints get it, around only the NetVips work (not the whole request, so slow
    connections don't hold anything). Two `System.Threading.RateLimiting.ConcurrencyLimiter`s: a
    processor slot per image (the only queue with a limit, counted in images, `OldestFirst`), then
    megabytes of estimated memory. An image holds its slot while waiting for memory, so at most
    `ProcessorSlots` images wait there. Queue full: `ImageProcessingBusyException` → 503 with
    `Retry-After`. A closed tab cancels its wait and hands back its slot. **Global, not per tenant**:
    the point is keeping the machine up.
12. **Process.** `NetVips.Concurrency = 1` (one thread per image; the slots set how many run).
    Check whether AVIF encoding obeys it.
13. **Lease released** by `using`, even when processing throws.
14. **The bulk endpoint attaches** (artwork type id posted as a form field) through the attach
    procedure. If nothing was attached, it **deletes the stored original and variants right away**
    instead of leaving them to the sweep. It returns 200 with an outcome value for skips (not 400).
15. **Browser:** 200 feeds the report through the island. 429/503/504: exponential backoff with
    jitter, honouring `Retry-After`, capped, then reported as failed. 400: show the message.
16. **Report** fills in as files finish: attached, already had an image, ambiguous, duplicate
    names, unmatched, failed.

**Progress.** One bar for the whole run, not a row per file. A file counts in full once its response
arrives; files in flight count by bytes sent. Once every byte is sent and responses are pending,
show a "Processing…" spinner. `BbProgress` is fine, but JS **throttles** updates to the island
(e.g. 4 per second, one overall number). Today `FileDropZone.razor.js` sends every progress event.

**Host sizing, computed once at startup** (`ImageProcessingCapacity.FromHost`, printed in the startup
log): slots = cores − 1 (at least 1), leaving a core for pages; memory budget =
`MemoryBudgetMegabytes`, set per deployment (Mike, 2026-09-17, replacing a fraction of
`GC.GetGCMemoryInfo().TotalAvailableMemoryBytes`); startup refuses a budget at or above that total;
queued images = slots × (proxy timeout ÷ time per
image). Estimate = width × height × bands × bytes per sample (from the header's band format) ×
`MemoryEstimateMultiplier`. The megapixel check runs first, and an image whose estimate exceeds the
whole budget is also rejected as too large. All numbers live in `appsettings.json` `ImageProcessing`
and are **placeholders until measured**: 100 megapixels (Mike, 2026-09-17), a 1,000 MB budget, 60 s proxy
timeout, retry after 10 s. **Measured 2026-09-17** (our `ImageProcessor`, production libvips settings,
peak memory above baseline ÷ decoded size, on a fast desktop):

| Image | Multiplier | Time |
|---|---|---|
| 48 MP JPEG / WebP / PNG / 8-bit TIFF | 0.77–0.89 | 5–7 s |
| 100 MP JPEG | 0.64–0.66 | 7 s |
| 48 MP JPEG with an EXIF rotation tag (every portrait phone photo) | **1.39–1.44** | 9 s |
| 48 MP 16-bit TIFF | 0.37–0.49 | 8 s |

So the multiplier is 2 (margin over 1.44) and the time per image 10 s (a VPS core is slower than
the desktop). Re-measure on the VPS with `tools/measure-image-memory/measure.cs` (its header shows how, with or
without the SDK installed); a later run of the rotated photo gave 1.58, still under 2.

**Open:**
- The megapixel cap, the safety factor, the proxy timeout and seconds per image: measure first.
- Whether `/admin/uploads` gets the per-user token bucket too (probably yes).
- Multi-tenancy: see "Multi-tenancy notes" at the end of this file.

- [ ] CSV of the artist's spreadsheet creates shop items with no images. Decided 2026-09-13:
      one CSV per item type; fixed header names the artist must use (no column mapping); a header
      must be a known field (`title`, `price`, …), `series`, or the name of an existing vocabulary
      that applies to the item type; an unknown term name rejects the file, while an unknown
      **series** name is created (revised 2026-09-18, see the top of this file); any bad cell
      rejects the whole file with row numbers. A title already in `ShopItems` is skipped and listed, not rejected, so a CSV can
      be re-imported to add only its new rows; same-named items are added by hand. Sample
      export is `paintings.csv` at the repo root.
      - One row per painting; several series or terms in one cell, split on a
        configurable list delimiter, `;` by default (series names already contain commas)
      - Dimensions stored in cm; the import is told the source unit and converts. Columns become
        `decimal(8, 4)` so a 2-decimal inch value converts exactly
      - `DatePainted` nullable, and stores how precise it is (a year alone is "exact date unknown").
        Only `yyyy`, `yyyy-MM` and `yyyy-MM-dd` accepted; a year of `0` means blank
      - Unknown header columns reject the file
      - `Price` nullable: no price means viewable but not purchasable
      - Blank rows skipped, every cell trimmed, file read as UTF-8
      - Photographs and sculptures are their own item types, so their rows leave `paintings.csv`
      - `catalogueNumber` is unused

      **Decided 2026-09-16, after profiling `paintings.csv`:**
      - **One row per artwork, never merged.** The export is an old shape: one row per series
        membership (39 titles appear twice, differing only in series). The artist puts all series in
        one `;`-separated cell. Two rows with the same title in one file are both skipped and listed,
        like a title already in the catalog.
      - **Sample files:** `paintings.csv` stays as it is, as the invalid sample (old shape,
        `catalogueNumber`, sculpture rows). Mike makes a valid copy, and sculptures go in their own
        spreadsheet.
      - **Products: the page asks for a product type and a "one of a kind" checkbox.** The switch is on
        edition size, not on the type, the same rule the products island uses:
        - ticked: edition size 1, and `sold` decides stock (0 sold, 1 not); `editionSize`/`stock`
          columns are errors
        - unticked: `editionSize` (blank = open) and `stock` required; `sold` is an error
        - no price and not sold: no product, so viewable but not buyable. A sold row may have no price
          (sold long ago, price unknown)
      - **No auto-created terms or series.** Typos would make near-duplicate terms, and guessing which
        columns are vocabularies would be guessing. Unknown names are errors with row numbers; name
        comparison already ignores capitals.
      - **Check, then confirm.** The upload shows a review (rows to add, skipped rows, errors and
        warnings) and changes nothing. The review page carries the CSV text in a hidden field;
        confirming posts it back and the server checks it again before importing, so an edited field
        is harmless and nothing is stored or swept. If the result differs from the review, show the
        new review instead. Needs a file size limit. The whole import is one transaction.
      - **Deferred: what happened to an artwork** (lost, not for sale, gifted, in a collection). That's
        an artwork status, not a product, and is additive later; artworks with no products are the ones
        to review then. Decide at that point whether "sold" moves there too, or the product's stock 0
        and the status would both say it.
      - **Parser: Sylvan.Data.Csv, chosen 2026-09-16 and hidden behind `Imports/CsvTable`** (the only
        file that references it; `CsvTableTests` pin the behaviour a replacement must keep: spreadsheet
        row numbers, quoted commas and line breaks, blank rows skipped, short rows padded, comma-only
        delimiter, `MalformedCsvException` with a row number). Headers are dynamic (vocabulary names), so
        class mapping doesn't help. Sylvan.Data.Csv: MIT, 1.4.4 April 2026, `DbDataReader` API. Rejected:
        CsvHelper, the most used, but no commit since June 2025; Sep (MIT, active), pre-1.0, span-based,
        and leaves quotes in values unless `Unescape = true`.

      **Build order (started 2026-09-16):**
      - [x] `Imports/CsvTable` over Sylvan
      - [x] Planner (2026-09-16, never touches the database): `ArtworkImportPlanner.Plan(csvText, settings,
            snapshot)` returns an `ArtworkImportPlan` (additions with row numbers, skipped rows, errors,
            `Fingerprint()`). `ArtworkImportColumns` matches the header row, `ArtworkImportRowReader`
            reads cells, `ArtworkImportCatalogSnapshot.LoadAsync` reads the catalog (new
            `GetArtworkNames`). Header names are in `ArtworkImportHeaders`: title, description,
            dateCreated, height, width, depth, duration (h:mm:ss or m:ss), series, price, sold,
            editionSize, stock; anything else must be a vocabulary for the type. Header problems stop
            the plan before rows are read. Inches allow 2 decimal places, centimetres 4, prices 2;
            `sold` is TRUE/FALSE/blank. Skipped rows aren't checked. `RejectsTheOldPaintingsExport`
            reads `paintings.csv`
      - [x] `ArtworkRepository.AddManyAsync` (2026-09-16): one connection and transaction, `AddArtwork`
            per addition (its own BEGIN/COMMIT nest); a stale choice in a later row rolls back the
            earlier ones (tested). Shares `ExecuteAddAsync` with `AddAsync`
      - [x] Split the drop zone's look from its behaviour (2026-09-16, image upload checked in the browser):
            `FileDropZoneFrame` is the look, and its `<file-drop-zone>` custom element (loaded in `App.razor`,
            like `<partial-date-field>`) opens the picker, highlights on drag, and turns a drop into the
            input's `change` event. `FileDropZone` keeps only the uploading and listens to `change`.
            `FileDropField` is the static version: a named file input inside the frame (the form needs
            `enctype="multipart/form-data"`), with the chosen file's name shown under the button
      - [x] Import page under `/admin/catalog/artworks/import?type={id}` (BUILT 2026-09-16, working in the browser; `Pages/Admin/Catalog/ArtworkImport/`, dashboard link per type). Notes: both forms are multipart,
            so the 4 MB form value limit applies to the unencoded text rather than URL-encoded text; `CsvTable.Parse`
            turns every line break into `\n`, because a browser posts hidden fields back with `\r\n` and the
            fingerprint would never match; `Utf8Text` decodes strictly and the page says "save as CSV UTF-8";
            both forms post to `?type=` alone so a stale `imported=` doesn't show; the unit is only asked for
            when the type has height and width. The review opens in `Components/Dialogs/StaticModalDialog`, a
            native `<dialog>` opened by a `<static-modal-dialog>` custom element, not a BbDialog island: island
            parameters reach the server in one SignalR message (32KB limit) and the review carries the whole CSV.
            So both import forms post without Enhance. Later the same day every dialog moved onto one
            `Components/Dialogs/ModalDialog` (native `<dialog>`, `<modal-dialog>` custom element, look in
            `.artist-shop-modal`); Blueprint's dialogs and the three `BbPortalHost` islands are gone, and the
            reconnect modal uses the same class. Checked in the browser: rename and confirm dialogs, Escape
            blocked while saving (tested with a temporary delay), and no greying out between catalog pages
            without the portal hosts. `@oncancel:preventDefault` compiled to a literal attribute name and broke
            the circuit, so `ModalDialog` renders `data-keep-open` and its script cancels Escape. Original spec:: file, unit, product type, one of a
            kind, list separator; the review (counts, skipped rows, errors by row and column) carries the
            CSV text and the plan's fingerprint in hidden fields; confirm re-plans with a fresh snapshot
            and shows the new review if the fingerprint differs. File size limit. Dashboard link per type.
            Decode the upload with a `StreamReader`, which drops the byte order mark (`Trim()` doesn't, so
            the first header wouldn't match). Reject `"` and whitespace as the list separator
- [x] Schema edits (2026-09-13): nullable `Price`, `DatePainted` + `DatePaintedPrecision` with
      `PartialDate` owning date validation, `decimal(8, 4)` dimensions, and a Year/Month/Day
      `PartialDateField` whose script disables impossible days
- [x] **Catalog vocabulary admin (built 2026-09-14/15).** Mediums and supports became
      artist-defined vocabularies of terms (the Drupal/WordPress taxonomy pattern, "controlled
      vocabulary" in museum cataloguing).

      **Render model: static pages with prerendered islands.** The first build used
      `InteractiveServer` pages with prerendering off, and every link click flickered. The page
      went blank, then showed "Loading…", and the shell (inside the page) rebuilt its tabs. The rules now:
      - Pages are static and load all the data. Islands (`@rendermode="InteractiveServer"`,
        prerendered) never load their own data. A prerendered island that fetched its own data
        would render "not loaded" when the circuit attaches, which is the flicker again.
      - Island parameters travel as JSON. `IReadOnlySet` and dictionaries keyed by a record don't
        deserialize (checked), so domain records use lists. Callbacks can't cross into an island.
      - After writing, an island calls `NavigationManager.Refresh()`, so the static page
        re-renders and passes new parameters in, or `NavigateTo` when the page changes.
        `@key="Id"` gives a different entity a fresh island.
      - `CatalogLayout` is a static layout holding the tabs (`SectionNavLink`, which stays active
        on a vocabulary's edit and terms pages) and `<BbPortalHost @rendermode="InteractiveServer" />`.
        That host island also keeps the circuit open between catalog pages.
      - Rejected: interactive routing for all of `/admin`. `AddSinglePainting` would have to opt
        out, and moving between static and interactive routing is a full page load.
      - Fixed 2026-09-15: island controls used to look ready and do nothing until the circuit attached.
        `Components/Interop/DisabledUntilInteractive` wraps each island's controls in a
        `<fieldset class="contents" disabled>` while `RendererInfo.IsInteractive` is false (verified in
        the prerendered HTML). `ImagesField` wraps only its controls, never its hidden inputs: a
        disabled input posts nothing.
      - Known and accepted: field ids change once when an island attaches.

      **What exists.**
      - `/admin/catalog/vocabularies/new` and `/{id}/edit`: `VocabularyEditor` (static) plus the
        `VocabularyEditorForm` island. It holds the name, the shop item type checkboxes, a confirm
        dialog when unselecting a type that has terms on items, and delete. `VocabularyForm` owns
        its `EditContext`, the last-saved values (for `HasChanges` and `WasUnselected`), and the
        store for database errors. Creating a vocabulary opens its terms page, and deleting one
        opens `/new`.
      - `/admin/catalog/vocabularies/{id}`: `VocabularyTermList` (static). `AddVocabularyTermForm` is
        a static form post that redirects back to the page after adding, and the
        `VocabularyTermTable` island holds the rename `BbDialog` and the delete confirm.
      - `Components/Dialogs/ConfirmDialog` wraps `BbAlertDialog`, with plain buttons so it stays open
        and disabled while saving.
      - Procedures live in `Database/Procedures/Vocabularies/` and `VocabularyTerms/`. Duplicate names
        are matched on the constraint name by `SqlErrors.IsUniqueConstraintViolation` and become
        `NameAlreadyInUseException`, then a field message. No `ShopItemType` C# enum was needed.

      **Stale rows, done 2026-09-15.** Deleted-elsewhere errors all become `ChangedSincePageLoadException`
      now: 50002 (vocabulary, `UpdateVocabulary`) refreshes the editor page, which then says the
      vocabulary doesn't exist; 50003 (term, `RenameVocabularyTerm`) closes the dialog and refreshes
      the table. See the add-artwork form for 50001/50004.

      **Open.** Nothing links to `/admin/catalog` from the dashboard.

- [x] **Series admin — BUILT 2026-09-15.** Series stays its own entity rather than a vocabulary: it will grow
      a description, has a public page, and orders its artworks. Same render model as the
      vocabulary pages; add a Series tab to `CatalogLayout` (`SectionNavLink` with
      `ActivePath="/admin/catalog/series"`).

      **Decided 2026-09-15:**
      - **Any shop item type in any series, mixed freely** (a painting and a photograph can share
        one). No type checklist like vocabularies. Schema, edited in place in `0001`:
        `PaintingSeries` → `Series`, `PaintingAndSeriesJunction` → `ShopItemAndSeriesJunction
        (ShopItemId, SeriesId, SortOrder, IsCover)` referencing `ShopItems (Id)`. In C#,
        `PaintingSeries` → `Series`, and the series list moves from `Painting` up to `ShopItem`.
        `AddPainting` and `GetPaintingBySlug` follow the rename. (`ShopItem` itself may be renamed.)
      - **Created** by an add box on the list page, a static form post like `AddVocabularyTermForm`.
      - **Slugs:** series slugs are never numbered; a name whose slug another series has is
        rejected. Shop items keep `dbo.ResolveShopItemSlug`, which every `AddPainting`/`AddSculpture`
        calls, since all shop items share `dbo.ShopItems`. (A caller-passes-slugs `dbo.ResolveSlug`
        was built and removed the same day, once series stopped needing it.) When shop item editing
        arrives, the function takes the item's own id to leave out, or renaming "Sunset" to
        "Sunset!" yields `sunset-2`. One slug max length (`ArtistShopLimits.SlugMaximumLength`) and one
        `ArtistShopSlug.FromName` for shop items and series.
      - **Cover:** the artist stars one artwork on the series page, and its primary image is the
        cover, like `IsPrimary` on images. `IsCover` on the junction with a filtered unique index
        `WHERE IsCover = 1`, so the cover is always a member and leaves with it. The star is
        disabled on artworks with no images. Cover = the starred artwork's primary image, else the
        first artwork in order with an image, else a "no images yet" placeholder (a series can
        have no images at all).
      - **Custom order** is the same drag list as `ImagesField`. Series upload nothing (their images
        are the artworks' own), so the island only saves order and star, immediately: one set-based
        `UPDATE` for order (`UNIQUE (SeriesId, SortOrder)` forces that), one for the star.
      - **Extract a generic sortable list with a star** from `ImagesField`/`ImageUploadRow`. It owns
        `BbSortable`, move/nudge and ↑/↓, the radio star with explicit/fallback colours, and the
        "first item that `CanStar`" fallback. Each user supplies the row's middle and its extra
        actions as `RenderFragment<TItem>`s (images: progress, Retry, ✕; series: checkbox for bulk
        remove). Not an island itself, so callbacks and fragments work. Suggested: it reports
        moves rather than reordering `Items`, so the series island can save before showing the new
        order, then `Refresh()`; `ImagesField` applies moves immediately.
      - **Queries:** separate procedures for series with and without covers, not a flag parameter.

      **Build order:** 1) schema — DONE 2026-09-15 (`Series`, `ShopItemAndSeriesJunction` with
      `IsCover` + filtered unique index, series slug `nvarchar(200)`; `AddPainting` and
      `GetPaintingBySlug` updated); 2) slugs — DONE 2026-09-15 (`ArtistShopSlug.FromName` shared,
      `ArtistShopLimits.SlugMaximumLength`, test `NumbersTheSlugWhenItIsTaken`). Parallel tests
      exposed deadlocks in `AddPainting` under `SERIALIZABLE`: fixed by `ResolveShopItemSlug`
      reading once, over one prefix range, `WITH (UPDLOCK)`; 20 clean runs; 3) series procedures, repository, tests —
      DONE 2026-09-15 (`Procedures/Series/`, `SeriesRepository`, 13 tests in
      `SeriesRepositoryTests`; `0003` adds `dbo.OrderedIdList` for reorder; `PaintingSeries` →
      `Series` record, series list moved up to `ShopItem`). Errors 50004 (renamed series gone),
      50005 (reorder list no longer matches the series), 50006/50007 (cover not in series / has no
      image) reach the caller as raw `SqlException`s; the island decides what to show in step 5.
      The plain `GetAllSeries` (no covers) waits for the add-artwork series picker. **Changed
      2026-09-15:** series slugs are not numbered. A name whose slug another series has is
      rejected (`Unique_Series_Slug`), on add and rename, as `NameAlreadyInUseException`; the page
      must explain it in words the artist knows (see the wording under step 5); 4) extract the sortable list with
      a star — BUILT 2026-09-15 and working in the browser (`Components/Lists/StarredSortableList`;
      a line above the list appears only while a star is chosen by hand and offers going back to the
      automatic star, one control for the whole list rather than click-again; series side is
      `ClearSeriesCover` + `SeriesRepository.ClearCoverAsync`;
      `ImagesField` uses it, `ImageUploadRow` is now only the thumbnail, name, status and progress;
      star identity is the storage key, and Retry now sits after ↑/↓ beside ✕); 5) list page with add form, then the series page island — BUILT 2026-09-15, builds, not yet
      checked in the browser. Folder `Pages/Admin/Catalog/SeriesAdmin` (a folder named `Series` would
      make a namespace hiding the `Series` record). Series have their own `SeriesLayout`, not a tab in
      the vocabulary `CatalogLayout` (Mike: keep them separate). `SeriesList` (static, add form),
      `SeriesEditor` (static) with two islands: `SeriesActions` (rename dialog, delete confirm) and
      `SeriesShopItemList` (order, star, clear star, select and remove). The repository turns the
      "page is stale" errors 50004-50007 into `ChangedSincePageLoadException`; the list island shows
      "changed somewhere else" and refreshes. Nothing in the UI adds artworks to a series yet. Name-taken wording, following
      the artwork form's "web address": "Another series already has this name, or one that only
      differs in punctuation, accents or capital letters." A name with no letters or digits needs
      the artwork form's "no letters or numbers to build a web address from" check.

      **Pages:**
      - `/admin/catalog/series` lists series (with cover and artwork counts), linking to
        `/admin/catalog/series/{id}`: rename (the slug follows, breaking old links, accepted),
        reorder and star its artworks, tick artworks and remove them after one confirm, and
        delete the series after a confirm. Each row shows the artwork's primary image. Adding
        existing artworks there: not now.
      - Reorder, star, tick and remove belong in one island over the artwork list.
      - Already done: `SortOrder` on the junction with `UNIQUE (SeriesId, SortOrder)`. `AddPainting`
        appends with `MAX + 1` under `UPDLOCK`.
      - Same rules as vocabularies: names trimmed, empty rejected, delete procedures remove junction
        rows then the row in one transaction, dialog counts labelled "(count as of page load)".
      - Public series ordering, later: the customer picks the sort, the artist sets the default and
        can drag a custom order.
      - **Later, noted 2026-09-15, not designed yet:**
        - **Artist's order of the series themselves — BUILT 2026-09-15**, not yet checked in the
          browser. `Series.SortOrder` (`UNIQUE`; new series go last), `dbo.ReorderSeries` (50008 when
          the set changed), `SeriesOrderList` island on `/admin/catalog/series`, and the list component
          split into `SortableList` with `StarredSortableList` built on it (Mike chose the split over
          an optional star, keeping every parameter required). Visitors' own sort orders: much later.
        - **"Chronological" is undefined**, deferred until visitors can sort. A series has no date of its own. It could come from its
          artworks (earliest, latest, median), but dates are `Paintings.DatePainted` only, so other
          types would need their own date or a shared one on `ShopItems`. Decide before sculptures
          and photographs get their tables.
        - **Browse series by artwork type**: Artworks → Paintings → the series containing paintings,
          exclusively, mostly, or at least one. No schema change: count the junction rows per
          `ShopItems.ShopItemTypeId`. The open questions are what "mostly" means (a share? the most
          common type?) and whether the artist can override it. Deferred.
- [ ] Term and series pickers on the add-artwork form — BUILT 2026-09-15. A `CheckboxGroupField` per
      vocabulary that applies to paintings, and one for series (in the artist's order); existing terms
      and series only. The groups are the `TermAndSeriesPickers` island, which reloads its options when
      the tab becomes visible again, so a term or series created in another tab appears without losing
      the form. The `CatalogSetupNotice` island at the top of the form lists what isn't set up yet
      (no vocabulary, a vocabulary applying to no artwork type, a vocabulary without terms, no series),
      with links opening in a new tab, and refreshes the same way (Mike: tell them before they fill
      the form in). Both islands use `Components/Interop/TabReturnWatcher`, which renders nothing and
      raises `OnTabReturn` on `visibilitychange`. Checked boxes post repeated
      `Input.VocabularyTermIds` / `Input.SeriesIds` values, and form binding reads those into
      `List<int>` (verified with a real POST); with nothing checked it sets the list to null, so the
      form's lists turn null into empty. `ShopItemTypeId.Painting` mirrors the
      SQL `1`. **Stale picks, done 2026-09-15:** `AddPainting` checks series ids too (50009, so a
      deleted series isn't a foreign key error), the repository turns 50001/50009 into
      `ChangedSincePageLoadException`, and the page keeps the form and its uploads while saying the choices
      were updated. The pickers island adopts the options it's passed on every render (the page reads
      them fresh each request), so a failed submit drops what no longer exists; ticks stay island state,
      seeded once.
- [ ] Someday: export the catalog to CSV plus images in folders by series, for moving the shop
      elsewhere. The CSV import only creates items, so the site becomes the source of truth once
      the artist edits there; a CSV can't update existing paintings or add them to a series
- [ ] **Edit painting — PAUSED 2026-09-16 for step 10, where it becomes "Edit artwork".** Decisions
      from 2026-09-15 below; step 10 revises the slug rule and the form class.
      - **The slug follows the name**, like a series. Mike: leaving the old name in the web address is
        weird, and editing usually happens a few times early in a painting's life and then never. Warn
        in the form that old links will stop working, in case any were shared. `ResolveShopItemSlug`
        needs the artwork's own id left out, or renaming "Sunset" to "Sunset!" numbers it `sunset-2`
        (the function reads `dbo.ShopItems` itself, so pass an id to exclude, `NULL` when adding).
      - **Split in two.** First: name, price, date, description, dimensions, terms and series, reusing
        the add form's fields and the `TermAndSeriesPickers` island, plus an `UpdatePainting` procedure
        (replace the term junction rows; for series, delete the rows that went and append new ones with
        `MAX + 1` so existing order survives). Second, separately: editing images, which means teaching
        `ImagesField` about images the artwork already has (show, reorder, restar, remove, add more) —
        today it only knows files being uploaded right now. "Remove" keeps unlinking only; the sweep
        deletes files later.
      - Form reuse: `PaintingCatalogAdditionForm` becomes the shared form class, seeded from an existing
        painting for edit. Watch the sibling `@key` rule and that a `[SupplyParameterFromForm]` model
        needs exactly one public constructor.
- [x] Split the drop zone's look from its behaviour so the static CSV form reuses it (see the CSV build order)
- [ ] Bulk image matching, build order (design above, 2026-09-17):
      1. DONE 2026-09-17: `NetVips.NetVips.BlockUntrusted = true;` in `Program.cs`. Not yet checked:
         upload a JPEG, PNG, WebP, AVIF and TIFF to confirm they still work
      2. DONE 2026-09-17: `dbo.AttachPrimaryImageToImagelessArtworkByName` (`Procedures/Artworks/AttachPrimaryImageToImagelessByName.sql`),
         `ArtworkImageRepository.AttachPrimaryToImagelessArtworkByNameAsync` returning the match (type +
         every matching artwork id), 50010 → `ChangedSincePageLoadException`. `0001` gained
         `Index_Artworks_TypeAndName` so the `UPDLOCK` lookup doesn't lock the whole table; the dev
         database must be dropped. 7 tests in `ArtworkImageRepositoryTests`, 148 pass.
         Collation pinned the same day: `DatabaseInitializer.Collation` (`Latin1_General_100_CI_AS_SC`) on
         `CREATE DATABASE`, and `VerifyCollationAsync` at startup (shop database only) and in the test fixture
      3. DONE 2026-09-17: `dbo.GetArtworkNameMatches` (`Procedures/Artworks/GetNameMatches.sql`) over a new
         `dbo.ArtworkNameList` (`0005`, primary key on Name, so case-only duplicates are an error: the
         page reports those before calling). `ArtworkImageRepository.GetArtworkNameMatchesAsync` returns a
         dictionary keyed by the names as sent. Both procedures now return the shared
         `ArtworkNameMatchType` in an `ArtworkNameMatch` (type + artwork ids); from the attach procedure,
         `OneImagelessArtwork` means attached. 151 tests pass
      4. DONE 2026-09-17: `Images/ImageUploadValidation` (validation moved out of the endpoint),
         `ImageProcessor.ReadHeader`, `ImageTooLargeException`, `ImageProcessingLimiter` + settings +
         capacity, and `ImageUploadStore` deleting the original on **any** failure (including busy
         and cancelled). `/admin/uploads` returns 503 + `Retry-After` when busy. 11 new tests
         (`ImageProcessingLimiterTests`, `ImageUploadStoreTests`), 162 pass. Not checked in a browser.
         Follow-ups the same day: `Utilities/Units` (`BytesPerMebibyte`, `PixelsPerMegapixel`; the CSV
         limit is now 1 MiB), `Utilities/ValidatedSettings.Read<T>` (bind a section by property name,
         `[Range]` attributes catch missing values, misspelled keys are errors) for both
         `ImageProcessingSettings` and `OrphanedImageSweepSettings` (now its own `OrphanedImageSweep`
         section), and a per-endpoint `RequestSizeLimitAttribute` on `/admin/uploads` (file limit + 1 MiB;
         not exercised by a test). 167 pass.
         **HEIC is not supported (Mike, 2026-09-17):** NetVips.Native's libheif has no HEVC decoder
         (patent-encumbered), and switching to the system libvips wasn't worth it. `ReadHeader` rejects
         `heif-compression = hevc` with `UnsupportedImageFormatException` (AVIF reports `av1`), and
         validation rejects `image/heic`/`image/heif` with the same "export as JPEG" message. Test fixture
         `tests/.../Images/Fixtures/sample.heic` (made with ImageMagick). 168 pass. The image picker's `accept` is now
         `ImageUploadValidation.FileInputAccept`, joined from the same set the server checks (not tried on an iPhone)
      5. DONE 2026-09-17: bulk endpoint (upload, process, attach, delete on skip, outcome in a 200)
         and the per-user token bucket, both described in "Done 2026-09-17" above. The JavaScript in
         step 6 posts `file` and a form field named exactly `artworkTypeId`, and the antiforgery
         token the way `FileDropZone.razor.js` already does
      6. DONE 2026-09-18: JS folder walk, both pickers, metadata stream, 4-at-a-time driver,
         backoff, throttled progress — see "What was built on 2026-09-18" at the top of this file
      7. DONE 2026-09-18: the page at `/admin/catalog/artworks/images`, island, depth rule,
         duplicate names through `DatabaseCollationComparer`, pre-check, bar + spinner, report.
         Still to do: the CSV import page's success state should link to it
- [ ] **Browse artworks — next.** Nothing lists them yet, so the only way to see what the CSV import
      and the image upload produced is the database
- [ ] Edit artwork page (step 10), the only way to add a second image

## 10. Artwork restructure — in progress (CSV import in step 9 was taken first, 2026-09-16)

**Why.** A painting is barely different from a sculpture or a photograph: the only real difference
is which vocabularies apply. The `Paintings` subtype table and the `ShopItem` → `Painting`
inheritance don't pay for themselves, and the artist needs types we can't foresee (fibre arts, a
dance performance). So `ShopItem` becomes `Artwork`, and its type becomes a row the artist manages.
"Work type" is the cataloguing standards' name for this (CCO, VRA Core); the table is `ArtworkTypes`.

**Decided:**
- Work types are artist-defined, seeded with Painting, Photograph and Sculpture. No code refers to a
  particular type: `ShopItemTypeId.Painting` and the computed `Paintings.ShopItemTypeId AS 1` go away.
- An artwork's type can't change after creation. The artist deletes it and adds it again.
- A type that artworks still use can't be deleted.
- Public address is `/artworks/{slug}`. Browse pages will filter and sort by type.
- **Fields per type work like vocabularies per type.** We predefine the fields, trying to cover
  every kind of artwork up front; a client who needs another asks us. The artist switches fields on
  per type. A field switched off in another tab while a form is open is handled the same way as
  un-ticking a vocabulary's type.
- Pre-release: edit `0001`/`0002` and drop the database, no migration.

**Schema.**
- Renames: `ShopItems` → `Artworks`, `ShopItemTypes` → `ArtworkTypes`, `ShopItemImages` →
  `ArtworkImages`, `ShopItemImageList` → `ArtworkImageList`, and the junctions and constraint names
  to match (`ArtworkAndVocabularyTermsJunction`, `VocabularyAndArtworkTypesJunction`,
  `ArtworkAndSeriesJunction`). `ArtworkTypes.Id` becomes `IDENTITY`.
- `Paintings` is dropped. Its columns move onto `Artworks`, renamed for any type: `DateCreated` +
  `DateCreatedPrecision`, `Description`, `WidthCm`, `HeightCm`, and a new `DepthCm`.
- The composite foreign keys that check a term's vocabulary applies to the artwork's type stay as
  they are, with the columns renamed.
- `ArtworkFields` (seeded by us, mirrored by a C# enum with explicit values) and
  `ArtworkTypeAndArtworkFieldsJunction`. A `CHECK` can't read another table, so `AddArtwork` (and
  later `UpdateArtwork`) `THROW`s when a value is given for a field its type doesn't have; that
  error becomes `ChangedSincePageLoadException` like 50001-50009.
- Fields (decided 2026-09-16): 1 Date created, 2 Height and width, 3 Depth, 4 Duration. Description
  is always on. Height and width are one switch because neither is any use alone. Depth requires
  height and width: `ArtworkFields.RequiresArtworkFieldId` (a field with no requirement names
  itself), copied into the junction under a foreign key, plus a foreign key from the junction to
  its own (type, required field) row. The database refuses depth without height and width, and
  refuses removing height and width while depth is on (547). The column is `NOT NULL` on purpose:
  a foreign key with a NULL column isn't checked, which let a NULL copy skip the rule. The type
  admin should show Depth nested under Height and width. Measurements are ordered height x width x
  depth everywhere, as galleries list them.

**Decided 2026-09-16, selling and navigation:**
- **Product types, not kinds** (renamed 2026-09-16): `ProductTypes` matches `ArtworkTypes`, and "product
  type" is the usual shop term; the `Product`/`Artwork` prefix already tells the two apart.
- **`Price`/`Stock` leave `Artworks`.** `ProductTypes` and `Products` (see the section below) are
  built in this step, not with the cart, so the add form doesn't lose its price.
- **The add and edit forms have a products section with any number of rows**: an island, like
  `ImagesField`, posting through hidden inputs. A plus button adds a row; each row has a product
  type, label, price and stock. Most of the time the artist adds the original and its price
  together with the artwork, so products aren't a separate form; the edit page is where the rest
  get set up.
- **Sold originals.** An edition-size-1 product with stock 0 must stay 0 when the edit form is
  saved. In the products island, a row with edition size 1 shows stock as for sale / sold instead
  of a number; otherwise stock is a number no higher than the edition size.
- **Edition size replaces one-of-a-kind** (decided 2026-09-16, replacing an `IsOneOfAKind` flag on
  the product type and an earlier "built-in Original"). `Products.EditionSize` is how many were ever made:
  `NULL` = open, restock freely (postcards, open prints); `N` = limited, never more than N (casts,
  limited prints); `1` = one of a kind. Product types are plain names, seeded Original, Print and
  Postcard, and an unused one can be deleted. The public page shows "Sold" for edition size 1 at stock 0,
  "Sold out" for others at 0, and "Edition of 10, 3 left". The CSV import page asks which product type
  "sold" rows become; they get edition size 1 and stock 0.
- **Dropped: at most one original per artwork.** Nothing marks a product as "the original" now,
  and `WHERE EditionSize = 1` would forbid a painting plus a one-off monoprint.
- **Refilling stock is allowed** (decided 2026-09-16). The only rule is `Stock <= EditionSize`.
  The artist edits stock by hand for private and gallery sales, and could always add a new
  product with full stock anyway, so nothing checks stock against past sales.
- **A sold product may have no price** (decided 2026-09-16):
  `CHECK (Price IS NOT NULL OR Stock = 0)`. Putting it back up for sale then requires a price.
- **Ways to the edit page:** an "Edit" link on the public page for logged-in admins, and "Edit it"
  next to "View it" after adding. An admin list of all artworks with filters comes later.

**Open:**
- **A product with no artwork** (a postcard of the artist in the studio). Deferred. Mike's idea: a
  product links to either an artwork or a non-artwork record (say `Merchandise`: name, slug,
  description, its own images), never both. Two nullable foreign keys plus a `CHECK` that exactly
  one is set keeps real foreign keys. Adding it later is additive. Watch that a unique index treats
  NULLs as equal in SQL Server, so the one-original index must also filter `ArtworkId IS NOT NULL`.
- **The admin artworks list** is deferred. Before building it, decide which filters (type, series,
  term, name, has images, for sale?), sorting, and paging.

**Build order.**
- [x] Schema (2026-09-16): `0001` rewritten, `0002` renamed to `ArtworkImageList`, new `0004`
      `ProductList`. Checked on a scratch database. **Databases not dropped yet**; drop them once the
      C# compiles. Fields seeded: 1 date created, 2 dimensions (depth optional inside it),
      3 duration; description is always on. Painting, Photograph and Sculpture start with 1 and 2
- [x] Procedures (2026-09-16): `ShopItems/` → `Artworks/`, everything renamed, checked on a
      scratch database. `AddArtwork` takes `@ArtworkTypeId`, the new columns and `@Products`, and
      throws 50010 (type gone), 50011 (a field switched off), 50012 (product type gone).
      `GetArtworkBySlug` `EXEC`s `GetArtworkById`, which returns artwork (with type id and name),
      images, series, terms, products. `UpdateArtwork` and the work type admin procedures come
      with their own steps
- [x] C# (2026-09-16): `ShopItem*` → `Artwork*` everywhere; `Painting` removed, `Artwork` is concrete
      with its type and products; `ArtworkRepository` (`AddAsync`, `GetByIdAsync`, `GetBySlugAsync`)
      maps 50001/50009-50012 to `ChangedSincePageLoadException`; `ProductTypeRepository` + `GetProductTypes`;
      `ArtworkField` enum; `Product`/`ProductAddition` in `Domain/Commerce`. 77 tests pass.
      Interim state until the later steps:
      - `/admin/catalog/artworks/add?type={id}` (dashboard links one per type) renders only the
        type's fields (`GetArtworkTypeFields`) and clears posted values for fields switched off
        meanwhile. It has no duration input yet and sends no products, so there is no price entry
        until the products island. 80 tests pass
      - public page is `/artworks/{slug}`; the public nav's "Paintings" page is still the placeholder
- [x] Work type admin under `/admin/catalog/types` (2026-09-16): add, rename, delete when unused, and
      tick the type's fields. Vocabularies keep ticking their types on the vocabulary page
- [ ] Products island: rows with product type, label, price, edition size and stock; with edition size 1,
      stock is for sale (1) or sold (0).
      The page passes the product types as a record-wrapped list, per the island parameter rule.
      **The add form starts with one row of the default product type** (decided 2026-09-19), since
      adding an artwork by hand usually means adding its original too; the artist can remove the row.
      Nothing creates a product today except the CSV import, so an artwork added by hand can be
      viewed and not bought.
      **(From Claude, 2026-09-19)** When it lands, move `AddArtwork`'s product-type check (50012)
      into `dbo.CheckArtworkChoicesAreCurrent` and give `UpdateArtwork` a `@Products` parameter, so
      the two forms can't drift on which stale choices they refuse. It sits in `AddArtwork` today
      only because the CSV import is the one caller that sends products
- [ ] Add artwork: the artist picks the type first, then `/admin/catalog/artworks/add?type={id}`.
      The static page renders only that type's fields and vocabularies, so no island has to react
      to a type dropdown. `TermAndSeriesPickers` takes the type id instead of assuming paintings.
      The products island's rows are inserted into `Products` in the same transaction. After adding,
      the message links to both the public page and the edit page
- [ ] Public page `/artworks/{slug}` replaces `/paintings/{slug}`, with an "Edit" link for admins;
      dashboard nav updated
- [x] Edit artwork (2026-09-19: fields, terms, series, images and delete; products wait on the
      products island). Built as described below, with one addition: a duration input, because the
      CSV import can set a duration and a form that didn't carry it would erase it on save:
      - Route by id, `/admin/catalog/artworks/{id:int}/edit`, since the slug changes on rename
      - Slug rule: keep the current slug if it equals the new name's slug or that slug plus
        `-<number>`; otherwise call `ResolveArtworkSlug`. This keeps an unchanged save from moving
        `sunset-2` to `sunset-4`, and the function needs no id to exclude
      - `UpdateArtwork`: stale-choice checks as in `AddArtwork`, a new error for an artwork deleted
        while the form was open; replace all term rows; for series, delete the unchecked rows and
        append new ones at `MAX + 1`. Removing an artwork from a series whose cover it was removes
        the cover too, since `IsCover` lives on that junction row
      - Form class: a base form with the fields, terms and series; the add form inherits it and
        adds `Images`/`PrimaryImageKey` (the edit form has no images in phase one). Seed it with a
        static `FromArtwork(Artwork)`, since a `[SupplyParameterFromForm]` model needs exactly one
        public constructor. Keep the property named `Input`: the pickers hardcode
        `Input.VocabularyTermIds` and `Input.SeriesIds`
      - Editing images: done in the same session, see below
- [ ] CSV import (step 9) is per work type: the artist picks the type on the import page, and the
      known headers are that type's fields and vocabularies, plus series, price and sold
- [ ] Next: admin artworks list under `/admin/catalog/artworks`, with filters, linking to each edit page.
      Once it exists, the artwork type page's "Used by N artworks, so it can't be deleted" should link
      to it filtered by that type (`ArtworkTypeEditorForm.razor`)

### Products and orders — tables built in this step; cart and orders come later

Discussed 2026-09-16. A postcard or print isn't an artwork but is made from one.
- **`Products`**: `Id`, `ArtworkId` → `Artworks`, `ProductTypeId`, `Label` ("A4", "A3"), `Price`,
  `EditionSize`, `Stock`. Cart lines reference `ProductId`. An artwork with no products can be
  viewed but not bought. Rejected: a cart line pointing at "an artwork, print or postcard" by product type
  + id, because that id can't have a foreign key. Edition size rules are in "Decided" above.
- Tracking which number of an edition sold (3/10) comes later.
- **Order lines are snapshots**: artwork name, product type name, label, unit price and quantity are
  copied, plus a nullable `ProductId` with `ON DELETE SET NULL`. Renames, price changes and
  deletions then can't rewrite history. The order copies the shipping address and totals too.

## Multi-tenancy notes (design started 2026-09-24)

**Decided with Mike, 2026-09-24:**
- **Words:** the tenant is a **site** in code (`SiteId`, `CurrentSite`), "website" in text people
  read: an artist's portfolio and blog, and later their shop. "Shop" is only the selling part.
- One Postgres cluster, plus the shared identity database. Accounts are platform-wide, for
  customers and admins alike. ~~A database per site~~: replaced 2026-09-25 by **a schema per site
  in one database, with one shared connection pool**; see "Schema per site" below.
- A site has one **owner** (can delete the site and add admins) and any number of **admins** (edit
  content only). An account may administer several sites. Identity roles are global, so site rights
  are a membership `(user, site, role)`, not `RoleNames.Admin`. If the owner deletes their account,
  their site goes too.
- **Which site:** an exact host name → site lookup, so platform subdomains
  (`shop1.mikesilverman.net`) and custom domains (`alicepaints.com`, `shop.bobart.com`) work the same.
  Unknown host → 404. Cookies stay host-only: a customer signs in on each site separately.
- **Sign-up for now:** a code Mike hands out, which works only if it was issued, and is deleted
  once used. It expires, and can carry settings such as the storage quota. A random code, stored
  hashed in the platform database, made on a platform-operator dashboard in the production app
  (no signing). Real sign-up with billing and a free tier comes later, with abuse of the free tier
  as its own design.
- **Hosts:** a new site gets `name.artistshop.com` (reserved names such as `www`, `admin`, `api`,
  `mail` are never handed out). A custom domain is set up by the artist at their DNS provider (or
  by Mike, by separate agreement), and for now Mike adds it to the site's hosts by hand. One host is
  a site's main one; the others redirect to it. A host belongs to exactly one site, since it's how a
  request finds its site. So once artists add their own domains, a DNS TXT check is needed: without
  it, someone could list a domain whose owner has pointed it here but not yet added it, or one left
  pointing here after its site closed ("subdomain takeover").
- **Platform host** (`artistshop.com` itself, not a site): what the product is, sign-up, "my sites".
- **Platform database**, a third one, holding sites, hosts, memberships and sign-up codes. Dapper and
  plain SQL, not EF.
- **Owner:** exactly one per site, and ownership can be handed to one of its admins. Deleting a site
  or an owner's account has a 30-day grace period before anything is erased, tested with
  `FakeTimeProvider` (already a test dependency).
- **Image processing between sites:** sites take turns when several are uploading at once; one
  site alone gets every slot. Not needed for test sites.
- **Payments (far future):** Stripe or similar, paid straight to the artist, with no fee taken by the
  platform. Artists pay a subscription. An artist sees the email and shipping address on their own
  site's orders only.
- **Storage:** image files get a per-site prefix before a second site database exists (see the
  sweeper note below), and each site has a storage quota.
- **Connections:** ~~a small pool per site~~: replaced 2026-09-25 by one pool shared by every site
  (see "Schema per site" below).
- Deferred: per-site running of backups, sweeps and export (schema updates can run for every site at
  startup while there are only test sites); fair sharing of image processing between sites; anything
  across all sites, such as a shared gallery.

- Production's current content doesn't carry over: it can be deleted, and the spreadsheet import
  brings it back.

**Schema per site (decided with Mike, 2026-09-25; built the same day, see "Where this stands" at the top for what changed from this draft):**
- **Why:** a Postgres connection is bound to one database, so a database per site means a
  connection pool per site: each site up to 5 connections, and Postgres allows 100 in all, so about
  20 busy sites could take every one, each connection a server process of several MB. PgBouncer
  pools per database too. In one database, one pool (say 20) serves every site. Also: each
  database carries its own copy of Postgres's system catalogs, about 9 MB of dev's 9.4 MB site
  database, so 500 sites would be about 4.7 GB mostly of that; one backup instead of one per site;
  queries across sites become possible (a shared gallery). Measured 2026-09-25: a site is 16
  tables, 68 tables-plus-indexes and 63 functions, so 500 schemas are about 34,000 relations and
  31,500 functions, which Postgres handles; trouble is reported around ten times that.
- **Not chosen:** a site id on every row. It only pays off in the thousands of tenants, would change
  all 16 tables, 63 functions and 72 repository calls, and brings back the risk a forgotten filter
  leaks one artist's data into another's.
- **Tradeoffs accepted:** one busy site can take the whole shared pool, where today each site is
  capped at 5. A per-site cap in the app (as `ImageProcessingLimiter` does for images) is the fix,
  built when measuring shows it's needed. A connection that has served many sites keeps their
  catalog entries in memory, so connections get bigger as they get fewer. Isolation is unchanged:
  choosing the right schema per request, as the right database is chosen today.
- **The 2 GB VPS is a testing ground** (Mike): a bigger one comes once a few users pay. Size for 2 GB
  now, but don't choose designs only to squeeze into it.
- **Shape (draft, from Claude; settle details before building):**
  - Site schemas `site_<id>` live in one database, probably the platform database, so everything
    but Identity is one database. Open: whether Identity joins it as a schema too.
  - Migrations and functions run once per schema, as they run per database today: DbUp with the
    schema first in `Search Path`, its own journal table in that schema. After the functions are
    made, each is pinned to its schema (`ALTER FUNCTION … SET search_path = site_<id>`) in one loop,
    so none of the 63 files changes.
  - The app calls a site's functions through the shared data source, either by qualifying each
    call (`site_12.get_posts()`) or by setting `search_path` as a connection is taken from the pool.
    Which one is open; either way, a call that forgets the schema finds no function in `public`
    and fails loudly rather than reading another site.
  - `SiteDatabases` becomes the shared data source plus the site's schema name.
    `SiteProvisioner.Prepare` makes a schema rather than a database. Deleting a site becomes
    `DROP SCHEMA … CASCADE`. The tests' site databases become schemas; `tools/add-site` and the
    blog seeder follow. Nothing is deployed and dev data can go, so nothing is migrated: dev drops
    its `artist_shop_site_*` databases.
  - **Sign-up makes the schema before it uses the code** (Mike, 2026-09-25). Today
    `SiteSignUp` adds the site (using the code) and then runs `Prepare`, so if `Prepare` fails the
    code is gone, the host isn't loaded until a restart, and trying again says the name is taken.
    New order: check the code and name without writing (so the usual mistakes don't make and drop a
    schema); take the id with `nextval` on the sites sequence; make and migrate `site_<id>`; then
    `add_site_with_sign_up_code` with that id uses the code and adds the row in one transaction.
    If making the schema fails, or the code or name was taken in the meantime, drop the schema:
    nothing is used up and the person can try again. A crash between the schema and the row leaves
    an orphan schema, harmless since ids aren't reused; a startup sweep can drop them. A site row
    then only exists for a ready site, so the startup loop just runs new migrations.

**Build order (draft, from Claude):**
1. **Done 2026-09-24 (`a26f072`):** the app logs in as `artist_shop_app` (`LOGIN CREATEDB`, not a
   superuser), made by `postgres-init/create-app-role.sh`, which the postgres image runs only on an
   empty volume. New secret `POSTGRES_APP_PASSWORD` (dev `.env`, the VPS's
   `.artist-site-postgres-env`); `POSTGRES_PASSWORD` is now for psql by hand only. Dev was reset
   (`down -v`, `content/images` emptied): re-import the spreadsheet, and rerun
   `tools/seed-blog-posts/seed.cs` if the filler posts are wanted. Checked: fresh volume makes the
   role, 432 tests pass, the app creates both databases and owns every table. **VPS still to do:**
   copy `postgres-init/` beside the compose file, add the app password to both env files as the
   compose header says, and start from a fresh volume (its content can go).
2. **Done 2026-09-24 (`a26f072`):** each site's images are in `content/images/sites/<SiteId>/`
   (`ImageStorage.ForSite`; `ImageStorage` is one site's storage, scoped from the new
   `CurrentSite`). `/media/<key>/<file>` is now `VariantEndpoints`, serving the current site's
   folder, since the static files middleware serves one folder for everyone; same week-long
   caching, 304s work, and only `<width>.avif|webp` names are served. The sweeper takes one site's
   storage and repositories, and `OrphanedImageSweepService` loops over the sites, each in its own
   try. Until step 3, `SingleSite.Id` (1) is the only site: step 3 replaces its uses (the
   `CurrentSite` registration, the sweep's list and the startup `CreateFolders`). Checked in the
   running app: an upload lands in `sites/1/`, `/media` serves it (200, 304, HEAD), bad names 404.
   441 tests. Also fixed: `Blog.razor` was missing the `@using` for `LinkPreview`, so `/posts` had
   no link preview (the build's only warning, RZ10012).
3. **Done 2026-09-24 (`9c56bea`); Mike's browser check passed** (a series made on site 2 wasn't
   on site 1, blogs separate, uploads on both shown separately on the home pages and in the admin): the platform database
   `artist_shop_platform` (`sites`, `site_hosts`; scripts in `Database/Platform/`, run by
   `SchemaMigrator.Platform`) and one database per site, `artist_shop_site_<id>`, named from the id
   by `SiteDatabases`, which also caches a data source per site (pool settings in `SiteDatabases`
   in appsettings.json). `ConnectionStrings__ArtistShop` became `ConnectionStrings__ArtistShopPlatform`.
   `UseSiteHosts` answers a host no site has with a plain 404, first in the pipeline;
   `SiteHostDirectory` holds every host in memory (reloaded at startup, and by the app after it
   changes hosts). `CurrentSite.From` reads the request's host; `SiteCircuitStart`, a circuit
   handler, makes a circuit's `CurrentSite` as the circuit starts, while its request is still there.
   Every site repository is registered on the current site's data source (`AddSiteRepository` in
   Program.cs). Startup creates site 1 from `FirstSite:Hosts` when there are no sites (until step 5
   removes it), then brings every site's database up to date (`SiteProvisioner.Prepare`).
   `AllowedHosts` is gone from compose. Tools: `tools/add-site/add-site.cs -- --host …` (restart the
   app after), and the blog seeder takes `--site`. Tests make their site databases as
   `artist_shop_tests_site_*`, dropped at the start of each run. 459 tests.
   **Checked with curl, two sites in dev** (`site1.localhost` and `localhost` are site 1,
   `site2.localhost` is site 2): each lists only its own posts, a site 2 post is 404 on site 1,
   unknown hosts and bare IP addresses are 404, a login on one site isn't one on the other, an
   upload on site 2 lands in `sites/2/` and only site 2 serves it.
   **Mike's browser check** (the part curl can't do: live connections): run `dev.sh`, log in on
   `http://site2.localhost:5176`, add a series (or a vocabulary term) in the admin, then check it
   isn't on `http://site1.localhost:5176`'s admin. Both pages are interactive, so this proves a
   circuit finds its site. Then an upload and a save on each.
   **VPS:** the web env file's `ConnectionStrings__ArtistShop` line becomes
   `ConnectionStrings__ArtistShopPlatform=…Database=artist_shop_platform…`, and compose's
   `FirstSite__Hosts__0` replaces `AllowedHosts` (both in docker-compose.production.yml).
   Later: procedures are re-created in every site's database at every startup, fine for a few sites
   but slow for hundreds.
4. **Done 2026-09-25 (`1b6a222`):** `site_members (site_id, user_id, role)` in the platform
   database (`Platform/Scripts/0002`), Identity's user id with no foreign key, `SiteRole` Owner 1 /
   Admin 2, at most one owner per site (partial unique index). `add_site` takes the owner and
   inserts them in the same function, so no site is without one. `SitePolicies.Admin`
   (`Sites/SiteAdminAuthorization.cs`) passes for either role on the current site, asking
   `SiteRepository.GetMemberRoleAsync` on every check (no caching); every admin page, the Edit
   links, both admin endpoints, `SinglePost`'s drafts and the nav's Admin link use it. `RoleNames`
   and the Admin role are gone (removed from the dev identity database too). A signed-in
   non-member gets Identity's `/Account/AccessDenied` page: the pages are static SSR, so the
   authorization middleware turns them away before `Routes.razor`'s `NotAuthorized` ever renders.
   `IdentitySeeder` became `FirstSiteOwner`, called only while there are no sites; its settings are
   `FirstSite:OwnerEmail` / `FirstSite:OwnerPassword` (dev `.env`'s `DEV_OWNER_PASSWORD`).
   `add-site` needs `--owner <email>`. The platform operator role moved to step 5, with the
   dashboard that needs it. Not yet: an Owner-only policy (step 6 is its first use).
   **VPS:** the web env file's `Admin__Email` / `Admin__Password` become `FirstSite__OwnerEmail` /
   `FirstSite__OwnerPassword` (docker-compose.production.yml's header); start from a fresh volume.
5. The platform host, in four parts (agreed 2026-09-25), each checked in the browser before the next:
   - **5a. Done 2026-09-25 (`721591c`):** one app routing by host, as Rails' host constraints do,
     rather than a second app for the platform (a second .NET process costs 100-200 MB on the 2 GB
     VPS). `Platform:Host` (dev `localhost`, production `artshop.mikesilverman.net`; dev site 1 is
     now only `site1.localhost`). `HostDirectory` (was `SiteHostDirectory`) finds a request's
     `CurrentHost`, `.Platform` or `.Site(id)`, and refuses to load if a site has the platform's
     host; `CurrentSite` comes from it and throws on the platform. `[ServedOn(HostTypes...)]`: pages
     without it are a site's, other endpoints serve every host unless marked (`/media`, uploads and
     video links are site-only). `UseKnownHosts` (bare 404 for an unknown host) runs first;
     `UseServedOnHosts` 404s a page on the wrong type of host, after the status code pages so it
     shows Not Found. `UseAuthentication`/`UseAuthorization` are now called explicitly after both,
     since ASP.NET otherwise adds them at the start of the pipeline, before the host is known. `/`
     is `Home`, showing `SiteHome` or `PlatformHome`; `NotFound`, `Error` and the Account pages
     serve both; `MainLayout` shows `PlatformNavMenu` on the platform. `SiteAdminHandler` takes
     `CurrentHost`, not `CurrentSite`: Blazor makes every authorization handler whenever a page
     asks for `IAuthorizationService` (`AuthorizeRouteView` does, on every page), so a
     `CurrentSite` in its constructor threw on the platform. Checked with curl on all hosts and by
     Mike in the browser.
   - **5b. Done 2026-09-25 (`82d22da`), checked by Mike in the browser:** `PlatformOperator`, an
     Identity role; `PlatformOperator.SyncAsync` at every startup gives it to the account
     `Platform:OperatorEmail` names (made with `Platform:OperatorPassword` if missing; both env
     secrets) and takes it from anyone else. Account making is shared as `SeededAccounts`.
     `PlatformPolicies.Operator` requires the role. `SignUpCode` (16 random bytes as hex in groups
     of four; reading ignores hyphens, spaces and case; SHA-256 of the bytes is stored),
     `sign_up_codes` (platform migration 0003: hash, required note saying who it's for, expiry),
     `SignUpCodeRepository`. `/operator` (`Pages/Platform/Operator/`), platform only: a static form
     (note, 1-90 days, default 14) shows a new code once, after a redirect that carries it in an
     encrypted cookie (`MadeSignUpCodeCookie`, added in the review); an interactive table of unused codes with Revoke behind a `ConfirmDialog`. The
     platform nav shows Operator to the operator.
   - **5c. Built 2026-09-25 (uncommitted), reworked the same day:** the first version asked
     `/signup` for an email and password, making or reusing the account, which told anyone with a
     code whether an email had an account (a wrong password or a weak one returned before the code
     was used, so one code allowed any number of guesses). Now:
     - **Email** (brought forward from after step 5): `Email/`. `Mailer`, with `SmtpMailer`
       (MailKit, one connection per email) in the app and a capturing fake in `TestApp`.
       `EmailSettings` from `Email:*` (Host, Port, Security, Username/Password together or not at
       all, FromAddress, FromName); the app won't start without them. Dev and the rehearsal send to
       Mailpit (docker-compose.yml, `http://localhost:8025`; the rehearsal's at 8026). The
       provider is undecided: production's compose header lists the env lines, and the domain will
       need its SPF and DKIM records. `AccountEmails` replaces `IdentityNoOpEmailSender`.
     - **Register** at `/Account/Register`, platform only (`Pages/Platform/Register/`), linked
       from the platform's log in page. `AccountRegistration` (`Identity/`) answers the same for
       any email: Identity's password rules first, for every email; a new email gets an
       unconfirmed account and a confirmation link; an email with an account gets "You already
       have an account" with sign-in and reset links, and its password is hashed and thrown away
       so both take about as long. Either way the page redirects to "Check your email". Identity's
       own leak stays: the lockout page only appears for an account that exists.
     - **`/signup`** needs a signed-in account (`[Authorize]`), which becomes the owner, and asks
       only for the code and the website name. `SiteName` (`Domain/Sites`): 3-30 of a-z, digits and
       hyphens, lowercased as typed, no hyphen first or last, not `--` as the third and fourth
       characters (DNS's `xn--` names), and a reserved list (www, admin, api, mail and the like).
       The site's one host is `name.<Platform:Host>`. `SiteSignUp`: `add_site_with_sign_up_code`
       deletes the unexpired code and calls `add_site` in one transaction (`SH016` when the code
       can't be used; a taken host rolls back and keeps the code), then `SiteProvisioner.Prepare`
       and `HostDirectory.ReloadAsync`. If `Prepare` fails the person sees the error page and the
       site is finished at the next startup, but is a 404 until then. Success redirects to the new
       site's `/Account/Login?ReturnUrl=/admin` (cookies are per host). Boundary: the function
       lives with the sites (`SiteRepository.AddWithSignUpCodeAsync`) but deletes a sign-up code,
       since using the code is what adds the site.
     - **`FirstSite:*` is gone**: startup makes no site, `FirstSiteOwner` is deleted, and `TestApp`
       makes its site itself. `TextField` passes other attributes (`type`, `autocomplete`) to its
       input. `ConfirmEmail` links to sign in once confirmed.
     - Tests: `SiteNameTests`, the new repository function, `App/SignUpTests` and
       `App/RegisterTests`, posting the real forms (`TestApp.PostFormAsync`, `SignedInClientAsync`).
     - **VPS:** drop the web env file's `FirstSite__*` lines and add the `Email__*` ones; the first
       site is made by registering, then signing up with a code from `/operator`.
   - 5d. **Built 2026-09-25:** "My websites" at `/sites`, the sites an account owns or administers, linking to each main host (see the top of this file).
   **Production subdomains** need a wildcard DNS record at Namecheap and a wildcard certificate,
   which Let's Encrypt only issues through DNS-01. Namecheap's API only opens for accounts past a
   threshold (about $50 balance or spend, or 20 domains) and whitelisted IPs; otherwise a CNAME for
   `_acme-challenge.artshop.mikesilverman.net` to an acme-dns service avoids it. nginx then needs a
   `*.artshop.mikesilverman.net` server name. Decide at deploy.
6. Owner features: inviting admins (needs real email), handing over ownership, deletion with its
   grace period.
7. Custom domains and their certificates. (From Claude, 2026-09-25) Changing a site's main host
   must clear the old `is_main` and set the new one in one transaction: the partial unique index
   forces clearing first, and `get_member_sites` joins on `is_main`, so a site left with no main
   host silently drops out of "My websites".

**Follow-ups from the 2026-09-25 review (From Claude):**
- **Taking the operator role away isn't immediate.** Identity keeps no sessions on the server: the
  sign-in cookie is the session (the account's claims, roles included, encrypted with Data
  Protection), so there's nothing to delete. Every 30 minutes by default
  (`SecurityStampValidatorOptions.ValidationInterval`) the cookie's security stamp is compared with
  the account's: a different stamp signs the person out, the same one rebuilds the claims, which is
  when a changed role shows up. Circuits recheck on the same interval
  (`IdentityRevalidatingAuthenticationStateProvider`). `RemoveFromRoleAsync` doesn't change the
  stamp, so after `PlatformOperator.SyncAsync` takes the role away, the old account keeps it for up
  to 30 minutes. `UpdateSecurityStampAsync` on that account would sign it out at the next check.
  Mike wants to learn how Identity works before deciding.
- **Expired sign-up codes are never deleted.** 5c's "use a code" function refuses expired codes;
  clearing old rows (on the operator page, or a periodic job) comes later.
- **Site subdomains are "same-site" with the platform.** `name.artshop.mikesilverman.net` and
  `artshop.mikesilverman.net` share the registrable domain `mikesilverman.net`, so browsers treat
  them as one site: `SameSite` cookies are sent on requests from a site's page to the platform, and
  a script on a site's page could set cookies for `.artshop.mikesilverman.net` that the platform
  would then receive ("cookie tossing"). Today artists can't put script on a page: Blazor
  HTML-encodes every value, nothing renders raw HTML (no `MarkupString`), video embeds are built
  from a parsed YouTube/Vimeo id, and uploads are re-encoded to AVIF/WebP. The risk arrives with
  raw HTML, SVG uploads, custom CSS/JS or themes, or an encoding bug. The usual fix is what
  GitHub does (`github.com` vs `github.io`): sites under a different registrable domain from the
  platform, or the sites' parent domain added to the Public Suffix List. Decide at deploy, with
  the wildcard DNS record, before real artists have subdomains; a Content-Security-Policy header is
  worth adding either way.

### Earlier notes (2026-09-17, written for SQL Server; the Postgres port has happened since)

The app is single-tenant today; the goal is many artists' sites (say 250) on one small VPS.
- **Shape:** one process with a `TenantId` column, one process with a database per tenant, or a
  process per tenant (simplest code, most memory: a Blazor Server process idles at roughly
  100–200 MB). Not chosen.
- **Image processing limits:** `ImageProcessingLimiter` is per process. With one process it is the
  machine's cap; with several, each needs its share, or the cap has to be shared between them.
- **Leaning (Mike, 2026-09-17, not final):** one process, a database per shop. A forgotten
  `WHERE TenantId = …` then can't leak one artist's catalog into another's, which is the risk a
  shared-table design never fully loses. What it would cost: `SqlConnectionFactory` resolved per
  request instead of a singleton (and a `Max Pool Size` cap, since ADO.NET pools per connection
  string), DbUp run per tenant at startup or on first use, one small shared database holding the
  host-name-to-shop registry, and identity staying single with a tenant claim. Express's 50 GB cap
  is per database, which helps; its ~1.4 GB buffer pool is per instance, which doesn't.
- **Image storage must move behind an abstraction first.** `ImageStorage` writes straight to local
  folders and `OrphanedImageSweeper` deletes every file in `originals/` the database doesn't
  reference — so pointing the sweep at one shop's database while the folder holds every shop's
  files would delete the others' originals, silently and for good. The seam wanted is a storage
  interface (save, open, delete, list) whose local implementation prefixes a tenant, so an offsite
  image service can take its place later. Decide it before a second database exists.
- **Memory budget:** `ImageProcessing:MemoryBudgetMegabytes`, set per deployment, with the .NET heap
  pinned by `DOTNET_GCHeapHardLimitPercent` so the two fit inside the container. Plan: the app in its
  own container with a memory limit (`mem_limit` in compose), SQL Server in another, so SQL's memory
  is outside the app's. In dev the app runs on the host and shares the whole machine.
- **(From Claude, 2026-09-22) A Postgres role for the app instead of the superuser.** The app
  connects as `postgres`. If a SQL injection ever got through, a superuser could read files off the
  server and run programs (`COPY ... PROGRAM`). None of the tenancy shapes needs a superuser. A
  database per tenant needs `CREATEDB` (the app creates databases at runtime, as `EnsureDatabase`
  already does), a schema per tenant needs `CREATE` on the database, and a `TenantId` column needs
  nothing extra. A role with `CREATEDB` that owns its databases covers all three, so this can be
  done before or with the tenancy work.
- **(From Claude, 2026-09-22) Custom domains replace `AllowedHosts`.** Production allows only
  `artshop.mikesilverman.net` (set in `docker-compose.production.yml`). With tenants on their own
  domains, no fixed list works: remove the setting when the host-to-shop registry exists, and have
  that lookup answer 404 for a host it doesn't know.
- **Future:** move image processing into its own worker container (as imgproxy, Thumbor and
  Mastodon's Sidekiq do), so a memory spike or a crash in libvips can't take the website down.
  Needs `MALLOC_ARENA_MAX=2` in that container too.

## Deployment (work in progress, 2026-09-17)

`Dockerfile`, `.dockerignore` and `docker-compose.production.yml` are the plan for shipping; the open
items are listed at the top of the compose file. Checked locally the same day: the image builds
(Tailwind is downloaded in the build), the stack starts, migrations run, libvips loads, and the home
page returns 200. The app used about 56 MB idle and SQL Server about 680 MB, each under a 2 GB limit.
- **Inside a container .NET reports 75% of the memory limit** as `TotalAvailableMemoryBytes` (its GC
  heap limit): 2 GB gave 1,536 MB, so the image budget came out at 384 MB. At the current settings
  that rejected a 48 MP phone photo (about 412 MB estimated). Fixed the same day by replacing the
  fraction with an explicit `MemoryBudgetMegabytes`.
- **No CPU limit yet**, so the container saw all 20 host cores (19 slots). Add `cpus:` in compose;
  `Environment.ProcessorCount` follows it.
- `Failed to determine the https port for redirect` is expected until the reverse proxy exists.
- Trying it leaves the `artist-shop-production_*` volumes behind; `down -v` removes them.

## Image URLs (2026-09-17)

`Images/ImageVariants` owns the widths, formats, file names and "largest variant that exists up to
the wanted width"; `Images/ImageUrls.Variant(key, imageWidth, wantedWidth, format)` is the only place
that knows variants are served from `/media`, so a CDN or image service changes one file. The four
hand-built URLs (upload row, public artwork page, both series lists) use it, which also fixed the
public page asking for `800.webp` on images narrower than 800. Not yet used: `srcset`/`sizes` and a
`<picture>` with AVIF and WebP sources, for the gallery pages.

