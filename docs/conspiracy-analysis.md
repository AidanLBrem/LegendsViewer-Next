# Conspiracy Analysis

Load a standard Legends XML export (with its matching Legends Plus export alongside it), then open **Conspiracy Analysis** in the sidebar. Search objectives, people, roles or strategies; filter by civilization and recorded hold status. Select a plot to inspect participants, targets, agreements, immortality promises, strategy entities and exported fields. Graph nodes and text links open existing figure/entity/artifact pages or related plots.

## Data rules

- Intrigue records are parsed under historical figures by the existing streaming XML pipeline. The change extends the existing models rather than creating a second XML reader.
- A plot's key is `(owner historical figure ID, local plot ID)`. Actor IDs resolve within that owner's intrigue actor list. ID zero is valid; missing IDs default to -1.
- Legends Plus merges intrigue records by local ID and plot participants by actor ID. Distinct records are retained; matching scalar fields follow the existing Plus-precedence convention.
- `type` is presented as the objective. The top-level actor reference is labeled a target only for the explicitly recognized actor-directed objectives. An acquired artifact is labeled a target artifact; entity references are neutrally labeled associated entities.
- Plot roles and relationship roles are shown separately. Not every referenced actor is a conspirator: a target, enemy or suspected criminal is not necessarily participating willingly.
- `on_hold` supports an empty presence flag and explicit false/zero values. Absence is displayed as **Not marked on hold**, not a claim of activity, success or failure. No completion date is invented.
- Civilization filters include the plot's entity, participants' entities/strategy entities, and recorded entity relationships of the owner and participants, mapped through the current civilization hierarchy. Former affiliations and other relationships may appear; this is context, not proof of political sponsorship or affiliation at the time of the plot.
- All intrigue fields, including unknown nested fields, remain inspectable. Missing records show IDs without broken links. Handler and agreement IDs remain textual where no dedicated record route exists.
- Parent/delegated plot references are linked only when both owner and local plot IDs resolve.

## API

`GET /api/conspiracies?civilizationId=284&search=assassinate&status=not-on-hold&page=1&pageSize=25`

Filters are optional. Status accepts `on-hold` or `not-on-hold`; page size is 1–100. The response includes filtered/total counts and civilization choices. `GET /api/conspiracies/{figureId}/{localId}` returns a plot or 404. Results are computed from the currently loaded world, avoiding a stale cross-world cache. The graph is scoped to one plot to keep large worlds usable.

## Validation

Build with .NET 10 and Node/npm using the repository's existing build commands. Run backend tests with `dotnet test LegendsViewer.Backend.Tests/LegendsViewer.Backend.Tests.csproj`; run frontend tests with `npm run test:run` in the frontend directory.

The optional real-export backend test uses `LEGENDS_SMOKE_XML` and `LEGENDS_SMOKE_PLUS` environment variables. It is skipped when the standard XML path is unset. Large user exports are not committed.

Validated against the matched `autosave 1-00201-02-01` exports: 22,415 plots and 17,829 intrigue actors. A full world load and browser preview also exercised search, civilization filtering, plot drill-down and the network. Calculation took 379 ms on the first full-world measurement; observed subsequent HTTP responses were approximately 60–160 ms. These are local observations, not a performance guarantee.

New tests cover owner-scoped IDs, missing IDs, target roles, status flags, unknown fields, merging standard/Plus XML, API filtering/pagination, graph endpoints, unresolved graph links, and empty optional route parameters.

The broader repository suite has unrelated failures; see the accompanying validation report. Two existing warfare test compilation errors were repaired (DTO property names and an omitted constructor type) so the backend tests could run. No application warfare behavior was changed.
