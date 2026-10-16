using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MediaBrowser.Common.Configuration;
using Microsoft.Data.Sqlite;

namespace Jellyfin.Plugin.Comments.Data;

/// <summary>
/// Owns the plugin's private SQLite database. Kept entirely separate from Jellyfin's
/// own databases so a schema change here can never corrupt library/user data.
/// </summary>
internal sealed class CommentRepository
{
    private readonly string _dbPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommentRepository"/> class.
    /// </summary>
    /// <param name="applicationPaths">The server application paths.</param>
    public CommentRepository(IApplicationPaths applicationPaths)
    {
        var directory = Path.Combine(applicationPaths.DataPath, "comments");
        Directory.CreateDirectory(directory);
        _dbPath = Path.Combine(directory, "comments.db");
        InitializeSchema();
    }

    // ---------------------------------------------------------------- schema

    private void InitializeSchema()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            PRAGMA journal_mode = WAL;

            CREATE TABLE IF NOT EXISTS Comments (
                Id             TEXT    NOT NULL PRIMARY KEY,
                ItemId         TEXT    NOT NULL,
                UserId         TEXT    NOT NULL,
                Username       TEXT    NOT NULL,
                ParentId       TEXT    NULL,
                Text           TEXT    NOT NULL,
                TimestampTicks INTEGER NULL,
                CreatedAt      TEXT    NOT NULL,
                UpdatedAt      TEXT    NULL,
                IsDeleted      INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS IX_Comments_ItemId ON Comments (ItemId, CreatedAt);
            CREATE INDEX IF NOT EXISTS IX_Comments_ParentId ON Comments (ParentId);

            CREATE TABLE IF NOT EXISTS Reactions (
                CommentId TEXT NOT NULL,
                UserId    TEXT NOT NULL,
                Type      TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                PRIMARY KEY (CommentId, UserId, Type)
            );
            CREATE INDEX IF NOT EXISTS IX_Reactions_CommentId ON Reactions (CommentId);
            """;
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA busy_timeout = 5000;";
        pragma.ExecuteNonQuery();

        return connection;
    }

    // ---------------------------------------------------------------- comments

    public List<CommentRow> GetCommentsForItem(Guid itemId)
    {
        var rows = new List<CommentRow>();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, ItemId, UserId, Username, ParentId, Text, TimestampTicks, CreatedAt, UpdatedAt, IsDeleted
            FROM Comments
            WHERE ItemId = $itemId
            ORDER BY CreatedAt ASC;
            """;
        command.Parameters.AddWithValue("$itemId", itemId.ToString("D"));

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(ReadComment(reader));
        }

        return rows;
    }

    public CommentRow? GetComment(Guid commentId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, ItemId, UserId, Username, ParentId, Text, TimestampTicks, CreatedAt, UpdatedAt, IsDeleted
            FROM Comments
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", commentId.ToString("D"));

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadComment(reader) : null;
    }

    public int GetCommentCount(Guid itemId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Comments WHERE ItemId = $itemId AND IsDeleted = 0;";
        command.Parameters.AddWithValue("$itemId", itemId.ToString("D"));
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public CommentRow CreateComment(
        Guid itemId,
        Guid userId,
        string username,
        Guid? parentId,
        string text,
        long? timestampTicks)
    {
        var id = Guid.NewGuid();
        var createdAt = UtcNow();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Comments (Id, ItemId, UserId, Username, ParentId, Text, TimestampTicks, CreatedAt, UpdatedAt, IsDeleted)
            VALUES ($id, $itemId, $userId, $username, $parentId, $text, $timestampTicks, $createdAt, NULL, 0);
            """;
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        command.Parameters.AddWithValue("$itemId", itemId.ToString("D"));
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));
        command.Parameters.AddWithValue("$username", username);
        command.Parameters.AddWithValue("$parentId", DbGuid(parentId));
        command.Parameters.AddWithValue("$text", text);
        command.Parameters.AddWithValue("$timestampTicks", DbLong(timestampTicks));
        command.Parameters.AddWithValue("$createdAt", createdAt);
        command.ExecuteNonQuery();

        return new CommentRow(id, itemId, userId, username, parentId, text, timestampTicks, createdAt, null, false);
    }

    public bool UpdateCommentText(Guid commentId, string text)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Comments SET Text = $text, UpdatedAt = $updatedAt WHERE Id = $id AND IsDeleted = 0;";
        command.Parameters.AddWithValue("$text", text);
        command.Parameters.AddWithValue("$updatedAt", UtcNow());
        command.Parameters.AddWithValue("$id", commentId.ToString("D"));
        return command.ExecuteNonQuery() > 0;
    }

    public bool SoftDeleteComment(Guid commentId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = "UPDATE Comments SET Text = '', UpdatedAt = $updatedAt, IsDeleted = 1 WHERE Id = $id AND IsDeleted = 0;";
            update.Parameters.AddWithValue("$updatedAt", UtcNow());
            update.Parameters.AddWithValue("$id", commentId.ToString("D"));
            if (update.ExecuteNonQuery() == 0)
            {
                transaction.Rollback();
                return false;
            }
        }

        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM Reactions WHERE CommentId = $id;";
            delete.Parameters.AddWithValue("$id", commentId.ToString("D"));
            delete.ExecuteNonQuery();
        }

        transaction.Commit();
        return true;
    }

    public bool HasReplies(Guid commentId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Comments WHERE ParentId = $id;";
        command.Parameters.AddWithValue("$id", commentId.ToString("D"));
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
    }

    // ---------------------------------------------------------------- reactions

    public Dictionary<Guid, List<ReactionCount>> GetReactionCounts(Guid itemId)
    {
        var result = new Dictionary<Guid, List<ReactionCount>>();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT r.CommentId, r.Type, COUNT(*)
            FROM Reactions r
            INNER JOIN Comments c ON c.Id = r.CommentId
            WHERE c.ItemId = $itemId
            GROUP BY r.CommentId, r.Type
            ORDER BY r.Type;
            """;
        command.Parameters.AddWithValue("$itemId", itemId.ToString("D"));

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var commentId = Guid.Parse(reader.GetString(0));
            var type = reader.GetString(1);
            var count = reader.GetInt32(2);

            if (!result.TryGetValue(commentId, out var list))
            {
                list = [];
                result[commentId] = list;
            }

            list.Add(new ReactionCount(type, count));
        }

        return result;
    }

    public Dictionary<Guid, List<string>> GetUserReactions(Guid itemId, Guid userId)
    {
        var result = new Dictionary<Guid, List<string>>();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT r.CommentId, r.Type
            FROM Reactions r
            INNER JOIN Comments c ON c.Id = r.CommentId
            WHERE c.ItemId = $itemId AND r.UserId = $userId
            ORDER BY r.Type;
            """;
        command.Parameters.AddWithValue("$itemId", itemId.ToString("D"));
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var commentId = Guid.Parse(reader.GetString(0));
            var type = reader.GetString(1);

            if (!result.TryGetValue(commentId, out var list))
            {
                list = [];
                result[commentId] = list;
            }

            list.Add(type);
        }

        return result;
    }

    public List<ReactionCount> GetReactionCountsForComment(Guid commentId)
    {
        using var connection = OpenConnection();
        return ReadReactionCounts(connection, commentId);
    }

    public List<string> GetUserReactionsForComment(Guid commentId, Guid userId)
    {
        using var connection = OpenConnection();
        return ReadUserReactions(connection, commentId, userId);
    }

    /// <summary>
    /// Toggles a reaction: adds it if absent, removes it if present. Returns the
    /// aggregated reaction counts for the comment after the change.
    /// </summary>
    public List<ReactionCount> ToggleReaction(Guid commentId, Guid userId, string type)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM Reactions WHERE CommentId = $commentId AND UserId = $userId AND Type = $type;";
            delete.Parameters.AddWithValue("$commentId", commentId.ToString("D"));
            delete.Parameters.AddWithValue("$userId", userId.ToString("D"));
            delete.Parameters.AddWithValue("$type", type);

            if (delete.ExecuteNonQuery() > 0)
            {
                transaction.Commit();
                return ReadReactionCounts(connection, commentId);
            }
        }

        using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO Reactions (CommentId, UserId, Type, CreatedAt) VALUES ($commentId, $userId, $type, $createdAt);";
            insert.Parameters.AddWithValue("$commentId", commentId.ToString("D"));
            insert.Parameters.AddWithValue("$userId", userId.ToString("D"));
            insert.Parameters.AddWithValue("$type", type);
            insert.Parameters.AddWithValue("$createdAt", UtcNow());
            insert.ExecuteNonQuery();
        }

        transaction.Commit();
        return ReadReactionCounts(connection, commentId);
    }

    private static List<ReactionCount> ReadReactionCounts(SqliteConnection connection, Guid commentId)
    {
        var result = new List<ReactionCount>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Type, COUNT(*) FROM Reactions WHERE CommentId = $commentId GROUP BY Type ORDER BY Type;";
        command.Parameters.AddWithValue("$commentId", commentId.ToString("D"));

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new ReactionCount(reader.GetString(0), reader.GetInt32(1)));
        }

        return result;
    }

    private static List<string> ReadUserReactions(SqliteConnection connection, Guid commentId, Guid userId)
    {
        var result = new List<string>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Type FROM Reactions WHERE CommentId = $commentId AND UserId = $userId ORDER BY Type;";
        command.Parameters.AddWithValue("$commentId", commentId.ToString("D"));
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }

    // ---------------------------------------------------------------- helpers

    private static CommentRow ReadComment(SqliteDataReader reader)
    {
        return new CommentRow(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            Guid.Parse(reader.GetString(2)),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : Guid.Parse(reader.GetString(4)),
            reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetInt64(6),
            reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.GetInt64(9) != 0);
    }

    private static string UtcNow() => DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

    private static object DbGuid(Guid? value) => value is Guid guid ? guid.ToString("D") : DBNull.Value;

    private static object DbLong(long? value) => value.HasValue ? (object)value.Value : DBNull.Value;
}
