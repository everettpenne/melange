# Jellyfin Comments Plugin

A self-hosted comment section for your Jellyfin media. Signed-in users can post,
reply, edit, delete and react to comments — including **timestamped comments** tied
to a specific playback position.

The plugin is **API-first**: it exposes a small REST API that any client (the web
UI, Kodi, your own Android app, etc.) can call. Data is stored in the plugin's own
SQLite database, completely separate from Jellyfin's library/user databases.

## Features

- Per-media comments, keyed by the Jellyfin item id (movies, episodes, etc.).
- Threaded replies.
- Edit and delete your own comments; administrators can moderate any comment.
- Reactions (a "like" or any emoji string), toggled per user.
- Timestamped comments — attach a comment to a playback position (in ticks).
- Soft delete: removed comments show as `[deleted]` so replies stay intact.
- Admin configuration page in **Dashboard → Plugins → Comments**.

## Requirements

- Jellyfin server **10.11.x** (targets `net9.0` / ABI `10.11.0.0`).
- To build: the **.NET 9 SDK**.

## Build

```bash
dotnet publish Jellyfin.Plugin.Comments.csproj -c Release -o artifacts
```

or use the helper script:

```bash
./build.sh
```

This produces `artifacts/Jellyfin.Plugin.Comments.dll`.

## Install

1. Stop Jellyfin.
2. Create the plugin directory (it does not exist by default):
   ```bash
   mkdir -p <jellyfin-data>/plugins/Comments
   ```
   (On Linux/Docker this is usually `/config/plugins/Comments`.)
3. Copy `Jellyfin.Plugin.Comments.dll` into that directory.
4. Start Jellyfin. The plugin appears under **Dashboard → Plugins**.

> The comment database is created automatically on first use at
> `<jellyfin-data>/data/comments/comments.db`.

## Configuration

In **Dashboard → Plugins → Comments**:

| Setting | Default | Description |
| --- | --- | --- |
| Maximum comment length | 10000 | Max characters per comment/reply. |
| Allow threaded replies | on | Enables `ParentId` replies. |
| Allow reactions | on | Enables reactions on comments. |
| Allow timestamped comments | on | Enables `TimestampTicks`. |

## API

All endpoints require an authenticated Jellyfin user (the normal
`X-Emby-Authorization` / `Authorization` token Jellyfin clients already send).
Base path is the server root (e.g. `http://your-server:8096`).

### List comments

```
GET /Comments/{itemId}?startIndex=0&limit=50
```

Returns a page of top-level comments, each with nested `Replies` and reaction data.
`startIndex`/`limit` paginate **top-level comments only**; replies are always nested.

### Comment count

```
GET /Comments/{itemId}/Count
```

Returns `{ "ItemId": "...", "TotalComments": 12 }` (includes replies).

### Create a comment

```
POST /Comments
Content-Type: application/json

{
  "ItemId": "0a2b...uuid",
  "Text": "This episode was amazing!",
  "ParentId": null,
  "TimestampTicks": 61150000000
}
```

- `ParentId` (optional) turns the comment into a reply.
- `TimestampTicks` (optional) is the playback position in **ticks**
  (1 second = 10,000,000 ticks). `61150000000` = 1:41:55.

### Edit a comment

```
PUT /Comments/{commentId}
{ "Text": "Updated text" }
```

Author or administrator only.

### Delete a comment

```
DELETE /Comments/{commentId}
```

Author or administrator only. Soft delete (shows as `[deleted]`).

### Toggle a reaction

```
PUT /Comments/{commentId}/Reaction
{ "Type": "like" }
```

`Type` is any short string — `"like"`, `"👍"`, `"❤️"`, etc. Calling it again for
the same user removes the reaction. Returns the comment's updated reaction state.

## Response shape

`CommentDto`:

```jsonc
{
  "Id": "uuid",
  "ItemId": "uuid",
  "UserId": "uuid",           // Guid.Empty when deleted
  "Username": "alice",
  "ParentId": null,           // or the parent comment id
  "Text": "This episode was amazing!",
  "TimestampTicks": 61150000000,
  "TimestampSeconds": 6115,   // convenience, derived from ticks
  "CreatedAt": "2025-10-16T10:30:00.1234567Z",
  "UpdatedAt": null,          // set after an edit
  "IsEdited": false,
  "IsDeleted": false,
  "Reactions": [ { "Type": "like", "Count": 3 } ],
  "MyReactions": [ "like" ],  // reactions the requesting user placed
  "Replies": [ /* nested CommentDto */ ]
}
```

## Notes for your Android client

- Timestamps are in Jellyfin's native **ticks** (100 ns). To convert:
  `seconds = ticks / 10_000_000`, and `ticks = seconds * 10_000_000`. The API also
  returns `TimestampSeconds` directly.
- Use Jellyfin's existing authentication token for requests — no separate login is
  needed, and usernames are attached automatically from the token's user.
- `MyReactions` is per-request, so it reflects the user whose token is being used.

## Project layout

```
Jellyfin.Plugin.Comments/
├── Plugin.cs                    # entry point (BasePlugin + config page)
├── ServiceRegistrator.cs        # DI registration
├── Configuration/               # settings + dashboard config page
├── Api/CommentsController.cs    # REST endpoints
├── Services/                    # validation, permissions, tree building
├── Data/                        # SQLite schema + repository
├── Models/                      # request/response DTOs
├── build.yaml                   # plugin manifest (used by jellyfin-plugin-repo)
└── Jellyfin.Plugin.Comments.csproj
```

## Publishing to the Jellyfin plugin catalog

`build.yaml` is the manifest used by
[`jellyfin/jellyfin-plugin-repo`](https://github.com/jellyfin/jellyfin-plugin-repo).
Submit a pull request there (with a zip containing the built DLL) to make the plugin
installable from Jellyfin's dashboard catalog.
