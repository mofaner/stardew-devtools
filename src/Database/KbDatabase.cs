using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace SdvKb.Database
{
    public sealed class ApiRecord
    {
        public string Assembly { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string FullTypeName { get; set; } = string.Empty;
        public string MemberName { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Accessibility { get; set; } = string.Empty;
        public int MetadataToken { get; set; }
    }

    public sealed class SchemaRecord
    {
        public string AssetPath { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string FullTypeName { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string FieldsJson { get; set; } = string.Empty;
        public string FieldsText { get; set; } = string.Empty;
    }

    public sealed class VanillaDataRecord
    {
        public string AssetName { get; set; } = string.Empty;
        public string EntryKey { get; set; } = string.Empty;
        public string EnName { get; set; } = string.Empty;
        public string ZhName { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string RawJson { get; set; } = string.Empty;
    }

    public sealed class DocRecord
    {
        public string Source { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string UrlOrPath { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    public sealed class KbDatabase : IDisposable
    {
        private readonly SqliteConnection _conn;
        public string DbPath { get; }

        public KbDatabase(string dbPath)
        {
            DbPath = Path.GetFullPath(dbPath);
            string? dir = Path.GetDirectoryName(DbPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = DbPath,
                Mode = SqliteOpenMode.ReadWriteCreate
            };
            _conn = new SqliteConnection(builder.ToString());
            _conn.Open();

            ExecuteNonQuery("PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;");
            EnsureSchema();
        }

        public SqliteConnection Connection => _conn;

        public void CheckpointForPortableBundle()
        {
            ExecuteNonQuery("PRAGMA wal_checkpoint(TRUNCATE); PRAGMA optimize;");
        }

        public void EnsureSchema()
        {
            const string sql = @"
CREATE TABLE IF NOT EXISTS meta (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS api_items (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    assembly TEXT NOT NULL,
    kind TEXT NOT NULL,
    namespace TEXT NOT NULL,
    type_name TEXT NOT NULL,
    full_type_name TEXT NOT NULL,
    member_name TEXT NOT NULL,
    signature TEXT NOT NULL,
    summary TEXT NOT NULL,
    accessibility TEXT NOT NULL,
    metadata_token INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS idx_api_type ON api_items(type_name);
CREATE INDEX IF NOT EXISTS idx_api_member ON api_items(member_name);
CREATE INDEX IF NOT EXISTS idx_api_fulltype ON api_items(full_type_name);

CREATE VIRTUAL TABLE IF NOT EXISTS api_fts USING fts5(
    type_name,
    member_name,
    full_type_name,
    signature,
    summary,
    search_tokens,
    content='',
    tokenize='unicode61'
);

CREATE TABLE IF NOT EXISTS game_schemas (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    asset_path TEXT NOT NULL,
    type_name TEXT NOT NULL,
    full_type_name TEXT NOT NULL,
    kind TEXT NOT NULL,
    summary TEXT NOT NULL,
    fields_json TEXT NOT NULL,
    fields_text TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_schema_type ON game_schemas(type_name);
CREATE INDEX IF NOT EXISTS idx_schema_asset ON game_schemas(asset_path);

CREATE VIRTUAL TABLE IF NOT EXISTS schema_fts USING fts5(
    asset_path,
    type_name,
    full_type_name,
    summary,
    fields_text,
    search_tokens,
    content='',
    tokenize='unicode61'
);

CREATE TABLE IF NOT EXISTS vanilla_data (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    asset_name TEXT NOT NULL,
    entry_key TEXT NOT NULL,
    en_name TEXT NOT NULL,
    zh_name TEXT NOT NULL,
    summary TEXT NOT NULL,
    raw_json TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_data_asset ON vanilla_data(asset_name);
CREATE INDEX IF NOT EXISTS idx_data_key ON vanilla_data(entry_key);
CREATE INDEX IF NOT EXISTS idx_data_zh ON vanilla_data(zh_name);
CREATE INDEX IF NOT EXISTS idx_data_en ON vanilla_data(en_name);

CREATE VIRTUAL TABLE IF NOT EXISTS data_fts USING fts5(
    asset_name,
    entry_key,
    en_name,
    zh_name,
    summary,
    search_tokens,
    content='',
    tokenize='unicode61'
);

CREATE TABLE IF NOT EXISTS docs (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    source TEXT NOT NULL,
    category TEXT NOT NULL,
    title TEXT NOT NULL,
    url_or_path TEXT NOT NULL,
    content TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_docs_cat ON docs(category);
CREATE INDEX IF NOT EXISTS idx_docs_src ON docs(source);

CREATE VIRTUAL TABLE IF NOT EXISTS docs_fts USING fts5(
    category,
    title,
    content,
    search_tokens,
    content='',
    tokenize='unicode61'
);
";
            ExecuteNonQuery(sql);
            try
            {
                ExecuteNonQuery("ALTER TABLE api_items ADD COLUMN metadata_token INTEGER NOT NULL DEFAULT 0;");
            }
            catch
            {
                // Column already exists
            }
        }

        public void SetMeta(string key, string value)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT INTO meta(key, value) VALUES($k, $v) ON CONFLICT(key) DO UPDATE SET value = excluded.value;";
            cmd.Parameters.AddWithValue("$k", key);
            cmd.Parameters.AddWithValue("$v", value);
            cmd.ExecuteNonQuery();
        }

        public Dictionary<string, string> GetAllMeta()
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT key, value FROM meta ORDER BY key;";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                dict[r.GetString(0)] = r.GetString(1);
            }
            return dict;
        }

        public (long ApiCount, long SchemaCount, long DataCount, long DocCount) GetCounts()
        {
            return (
                ExecuteScalarLong("SELECT COUNT(*) FROM api_items;"),
                ExecuteScalarLong("SELECT COUNT(*) FROM game_schemas;"),
                ExecuteScalarLong("SELECT COUNT(*) FROM vanilla_data;"),
                ExecuteScalarLong("SELECT COUNT(*) FROM docs;")
            );
        }

        public void ReplaceApiAndSchemas(IEnumerable<ApiRecord> apis, IEnumerable<SchemaRecord> schemas)
        {
            using var tx = _conn.BeginTransaction();

            ExecuteNonQuery("DELETE FROM api_items; DROP TABLE IF EXISTS api_fts;", tx);
            ExecuteNonQuery(@"CREATE VIRTUAL TABLE api_fts USING fts5(
                type_name, member_name, full_type_name, signature, summary, search_tokens,
                content='', tokenize='unicode61');", tx);

            using (var cmdItem = _conn.CreateCommand())
            using (var cmdFts = _conn.CreateCommand())
            {
                cmdItem.Transaction = tx;
                cmdItem.CommandText = @"INSERT INTO api_items(assembly, kind, namespace, type_name, full_type_name, member_name, signature, summary, accessibility, metadata_token)
                    VALUES($asm, $kind, $ns, $tn, $ftn, $mn, $sig, $sum, $acc, $tokid);
                    SELECT last_insert_rowid();";
                var pAsm = cmdItem.Parameters.Add("$asm", SqliteType.Text);
                var pKind = cmdItem.Parameters.Add("$kind", SqliteType.Text);
                var pNs = cmdItem.Parameters.Add("$ns", SqliteType.Text);
                var pTn = cmdItem.Parameters.Add("$tn", SqliteType.Text);
                var pFtn = cmdItem.Parameters.Add("$ftn", SqliteType.Text);
                var pMn = cmdItem.Parameters.Add("$mn", SqliteType.Text);
                var pSig = cmdItem.Parameters.Add("$sig", SqliteType.Text);
                var pSum = cmdItem.Parameters.Add("$sum", SqliteType.Text);
                var pAcc = cmdItem.Parameters.Add("$acc", SqliteType.Text);
                var pTokId = cmdItem.Parameters.Add("$tokid", SqliteType.Integer);

                cmdFts.Transaction = tx;
                cmdFts.CommandText = @"INSERT INTO api_fts(rowid, type_name, member_name, full_type_name, signature, summary, search_tokens)
                    VALUES($id, $tn, $mn, $ftn, $sig, $sum, $tok);";
                var fId = cmdFts.Parameters.Add("$id", SqliteType.Integer);
                var fTn = cmdFts.Parameters.Add("$tn", SqliteType.Text);
                var fMn = cmdFts.Parameters.Add("$mn", SqliteType.Text);
                var fFtn = cmdFts.Parameters.Add("$ftn", SqliteType.Text);
                var fSig = cmdFts.Parameters.Add("$sig", SqliteType.Text);
                var fSum = cmdFts.Parameters.Add("$sum", SqliteType.Text);
                var fTok = cmdFts.Parameters.Add("$tok", SqliteType.Text);

                foreach (var a in apis)
                {
                    pAsm.Value = a.Assembly;
                    pKind.Value = a.Kind;
                    pNs.Value = a.Namespace;
                    pTn.Value = a.TypeName;
                    pFtn.Value = a.FullTypeName;
                    pMn.Value = a.MemberName;
                    pSig.Value = a.Signature;
                    pSum.Value = a.Summary;
                    pAcc.Value = a.Accessibility;
                    pTokId.Value = a.MetadataToken;
                    long rowId = (long)cmdItem.ExecuteScalar()!;

                    fId.Value = rowId;
                    fTn.Value = a.TypeName;
                    fMn.Value = a.MemberName;
                    fFtn.Value = a.FullTypeName;
                    fSig.Value = a.Signature;
                    fSum.Value = a.Summary;
                    fTok.Value = BuildSearchTokens(a.TypeName, a.MemberName, a.Signature, a.Summary);
                    cmdFts.ExecuteNonQuery();
                }
            }

            ExecuteNonQuery("DELETE FROM game_schemas; DROP TABLE IF EXISTS schema_fts;", tx);
            ExecuteNonQuery(@"CREATE VIRTUAL TABLE schema_fts USING fts5(
                asset_path, type_name, full_type_name, summary, fields_text, search_tokens,
                content='', tokenize='unicode61');", tx);

            using (var cmdSchema = _conn.CreateCommand())
            using (var cmdSchemaFts = _conn.CreateCommand())
            {
                cmdSchema.Transaction = tx;
                cmdSchema.CommandText = @"INSERT INTO game_schemas(asset_path, type_name, full_type_name, kind, summary, fields_json, fields_text)
                    VALUES($ap, $tn, $ftn, $kind, $sum, $fj, $ft);
                    SELECT last_insert_rowid();";
                var pAp = cmdSchema.Parameters.Add("$ap", SqliteType.Text);
                var pTn = cmdSchema.Parameters.Add("$tn", SqliteType.Text);
                var pFtn = cmdSchema.Parameters.Add("$ftn", SqliteType.Text);
                var pKind = cmdSchema.Parameters.Add("$kind", SqliteType.Text);
                var pSum = cmdSchema.Parameters.Add("$sum", SqliteType.Text);
                var pFj = cmdSchema.Parameters.Add("$fj", SqliteType.Text);
                var pFt = cmdSchema.Parameters.Add("$ft", SqliteType.Text);

                cmdSchemaFts.Transaction = tx;
                cmdSchemaFts.CommandText = @"INSERT INTO schema_fts(rowid, asset_path, type_name, full_type_name, summary, fields_text, search_tokens)
                    VALUES($id, $ap, $tn, $ftn, $sum, $ft, $tok);";
                var fId = cmdSchemaFts.Parameters.Add("$id", SqliteType.Integer);
                var fAp = cmdSchemaFts.Parameters.Add("$ap", SqliteType.Text);
                var fTn = cmdSchemaFts.Parameters.Add("$tn", SqliteType.Text);
                var fFtn = cmdSchemaFts.Parameters.Add("$ftn", SqliteType.Text);
                var fSum = cmdSchemaFts.Parameters.Add("$sum", SqliteType.Text);
                var fFt = cmdSchemaFts.Parameters.Add("$ft", SqliteType.Text);
                var fTok = cmdSchemaFts.Parameters.Add("$tok", SqliteType.Text);

                foreach (var s in schemas)
                {
                    pAp.Value = s.AssetPath;
                    pTn.Value = s.TypeName;
                    pFtn.Value = s.FullTypeName;
                    pKind.Value = s.Kind;
                    pSum.Value = s.Summary;
                    pFj.Value = s.FieldsJson;
                    pFt.Value = s.FieldsText;
                    long rowId = (long)cmdSchema.ExecuteScalar()!;

                    fId.Value = rowId;
                    fAp.Value = s.AssetPath;
                    fTn.Value = s.TypeName;
                    fFtn.Value = s.FullTypeName;
                    fSum.Value = s.Summary;
                    fFt.Value = s.FieldsText;
                    fTok.Value = BuildSearchTokens(s.AssetPath, s.TypeName, s.Summary, s.FieldsText);
                    cmdSchemaFts.ExecuteNonQuery();
                }
            }

            tx.Commit();
        }

        public void ReplaceVanillaData(IEnumerable<VanillaDataRecord> records)
        {
            using var tx = _conn.BeginTransaction();
            ExecuteNonQuery("DELETE FROM vanilla_data; DROP TABLE IF EXISTS data_fts;", tx);
            ExecuteNonQuery(@"CREATE VIRTUAL TABLE data_fts USING fts5(
                asset_name, entry_key, en_name, zh_name, summary, search_tokens,
                content='', tokenize='unicode61');", tx);

            using var cmdData = _conn.CreateCommand();
            cmdData.Transaction = tx;
            cmdData.CommandText = @"INSERT INTO vanilla_data(asset_name, entry_key, en_name, zh_name, summary, raw_json)
                VALUES($an, $ek, $en, $zh, $sum, $raw);
                SELECT last_insert_rowid();";
            var pAn = cmdData.Parameters.Add("$an", SqliteType.Text);
            var pEk = cmdData.Parameters.Add("$ek", SqliteType.Text);
            var pEn = cmdData.Parameters.Add("$en", SqliteType.Text);
            var pZh = cmdData.Parameters.Add("$zh", SqliteType.Text);
            var pSum = cmdData.Parameters.Add("$sum", SqliteType.Text);
            var pRaw = cmdData.Parameters.Add("$raw", SqliteType.Text);

            using var cmdFts = _conn.CreateCommand();
            cmdFts.Transaction = tx;
            cmdFts.CommandText = @"INSERT INTO data_fts(rowid, asset_name, entry_key, en_name, zh_name, summary, search_tokens)
                VALUES($id, $an, $ek, $en, $zh, $sum, $tok);";
            var fId = cmdFts.Parameters.Add("$id", SqliteType.Integer);
            var fAn = cmdFts.Parameters.Add("$an", SqliteType.Text);
            var fEk = cmdFts.Parameters.Add("$ek", SqliteType.Text);
            var fEn = cmdFts.Parameters.Add("$en", SqliteType.Text);
            var fZh = cmdFts.Parameters.Add("$zh", SqliteType.Text);
            var fSum = cmdFts.Parameters.Add("$sum", SqliteType.Text);
            var fTok = cmdFts.Parameters.Add("$tok", SqliteType.Text);

            foreach (var r in records)
            {
                pAn.Value = r.AssetName;
                pEk.Value = r.EntryKey;
                pEn.Value = r.EnName;
                pZh.Value = r.ZhName;
                pSum.Value = r.Summary;
                pRaw.Value = r.RawJson;
                long rowId = (long)cmdData.ExecuteScalar()!;

                fId.Value = rowId;
                fAn.Value = r.AssetName;
                fEk.Value = r.EntryKey;
                fEn.Value = r.EnName;
                fZh.Value = r.ZhName;
                fSum.Value = r.Summary;
                fTok.Value = BuildSearchTokens(r.AssetName, r.EntryKey, r.EnName, r.ZhName, r.Summary);
                cmdFts.ExecuteNonQuery();
            }

            tx.Commit();
        }

        public void UpsertDocsBySources(IEnumerable<string> sourcesToReplace, IEnumerable<DocRecord> docs)
        {
            using var tx = _conn.BeginTransaction();

            foreach (var src in sourcesToReplace)
            {
                using var del = _conn.CreateCommand();
                del.Transaction = tx;
                del.CommandText = "DELETE FROM docs WHERE source = $s;";
                del.Parameters.AddWithValue("$s", src);
                del.ExecuteNonQuery();
            }

            using (var cmdIns = _conn.CreateCommand())
            {
                cmdIns.Transaction = tx;
                cmdIns.CommandText = @"INSERT INTO docs(source, category, title, url_or_path, content)
                    VALUES($src, $cat, $title, $url, $cnt);";
                var pSrc = cmdIns.Parameters.Add("$src", SqliteType.Text);
                var pCat = cmdIns.Parameters.Add("$cat", SqliteType.Text);
                var pTitle = cmdIns.Parameters.Add("$title", SqliteType.Text);
                var pUrl = cmdIns.Parameters.Add("$url", SqliteType.Text);
                var pCnt = cmdIns.Parameters.Add("$cnt", SqliteType.Text);

                foreach (var d in docs)
                {
                    pSrc.Value = d.Source;
                    pCat.Value = d.Category;
                    pTitle.Value = d.Title;
                    pUrl.Value = d.UrlOrPath;
                    pCnt.Value = d.Content;
                    cmdIns.ExecuteNonQuery();
                }
            }

            // Rebuild docs_fts cleanly from docs table
            ExecuteNonQuery("DROP TABLE IF EXISTS docs_fts;", tx);
            ExecuteNonQuery(@"CREATE VIRTUAL TABLE docs_fts USING fts5(
                category, title, content, search_tokens,
                content='', tokenize='unicode61');", tx);

            using (var sel = _conn.CreateCommand())
            using (var fts = _conn.CreateCommand())
            {
                sel.Transaction = tx;
                sel.CommandText = "SELECT id, category, title, content FROM docs;";

                fts.Transaction = tx;
                fts.CommandText = "INSERT INTO docs_fts(rowid, category, title, content, search_tokens) VALUES($id, $cat, $title, $cnt, $tok);";
                var fId = fts.Parameters.Add("$id", SqliteType.Integer);
                var fCat = fts.Parameters.Add("$cat", SqliteType.Text);
                var fTitle = fts.Parameters.Add("$title", SqliteType.Text);
                var fCnt = fts.Parameters.Add("$cnt", SqliteType.Text);
                var fTok = fts.Parameters.Add("$tok", SqliteType.Text);

                using var r = sel.ExecuteReader();
                while (r.Read())
                {
                    long id = r.GetInt64(0);
                    string cat = r.GetString(1);
                    string title = r.GetString(2);
                    string cnt = r.GetString(3);

                    fId.Value = id;
                    fCat.Value = cat;
                    fTitle.Value = title;
                    fCnt.Value = cnt;
                    fTok.Value = BuildSearchTokens(cat, title, cnt);
                    fts.ExecuteNonQuery();
                }
            }

            tx.Commit();
        }

        public List<ApiRecord> SearchApi(string query, string? kindFilter, string? assemblyFilter, int limit)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<ApiRecord>();

            string trimmed = query.Trim();
            string andQuery = BuildFtsQuery(trimmed, useOr: false);
            if (string.IsNullOrWhiteSpace(andQuery))
                return new List<ApiRecord>();

            int candidatePoolSize = Math.Max(limit * 25, 350);
            var candidates = QueryApiCandidates(andQuery, kindFilter, assemblyFilter, candidatePoolSize);

            // Automatic OR fallback if strict AND returns empty (or very sparse on multi-term synonym queries)
            string orQuery = BuildFtsQuery(trimmed, useOr: true);
            if (candidates.Count == 0 && !string.IsNullOrWhiteSpace(orQuery) && orQuery != andQuery)
            {
                candidates = QueryApiCandidates(orQuery, kindFilter, assemblyFilter, candidatePoolSize);
            }

            if (candidates.Count == 0)
                return new List<ApiRecord>();

            return RankAndFilterApiCandidates(candidates, trimmed, limit);
        }

        private List<(ApiRecord Record, double Bm25)> QueryApiCandidates(
            string ftsQuery,
            string? kindFilter,
            string? assemblyFilter,
            int poolLimit)
        {
            var list = new List<(ApiRecord, double)>();
            using var cmd = _conn.CreateCommand();
            var sb = new StringBuilder(@"
SELECT a.assembly, a.kind, a.namespace, a.type_name, a.full_type_name, a.member_name, a.signature, a.summary, a.accessibility, a.metadata_token,
       bm25(api_fts, 2.0, 9.0, 1.5, 3.5, 2.5, 7.5) AS bm
FROM api_fts
JOIN api_items a ON a.id = api_fts.rowid
WHERE api_fts MATCH $q");

            cmd.Parameters.AddWithValue("$q", ftsQuery);

            if (!string.IsNullOrWhiteSpace(kindFilter))
            {
                sb.Append(" AND lower(a.kind) = lower($kind)");
                cmd.Parameters.AddWithValue("$kind", kindFilter!);
            }
            if (!string.IsNullOrWhiteSpace(assemblyFilter))
            {
                sb.Append(" AND lower(a.assembly) LIKE '%' || lower($asm) || '%'");
                cmd.Parameters.AddWithValue("$asm", assemblyFilter!);
            }

            sb.Append(" ORDER BY bm ASC LIMIT $lim;");
            cmd.Parameters.AddWithValue("$lim", poolLimit);
            cmd.CommandText = sb.ToString();

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add((new ApiRecord
                {
                    Assembly = r.GetString(0),
                    Kind = r.GetString(1),
                    Namespace = r.GetString(2),
                    TypeName = r.GetString(3),
                    FullTypeName = r.GetString(4),
                    MemberName = r.GetString(5),
                    Signature = r.GetString(6),
                    Summary = r.GetString(7),
                    Accessibility = r.GetString(8),
                    MetadataToken = r.GetInt32(9)
                }, r.GetDouble(10)));
            }
            return list;
        }

        /// <summary>
        /// Sub-millisecond B-tree index lookup for exact Type or Type.Member MetadataTokens used by `decompile`.
        /// </summary>
        public List<ApiRecord> LookupDecompileTargets(string rawQuery)
        {
            var results = new List<ApiRecord>();
            if (string.IsNullOrWhiteSpace(rawQuery))
                return results;

            string q = rawQuery.Trim();
            string normQ = q.Replace('+', '.');

            // 1. Check exact Type match (member_name = '')
            using (var cmdType = _conn.CreateCommand())
            {
                cmdType.CommandText = @"
SELECT assembly, kind, namespace, type_name, full_type_name, member_name, signature, summary, accessibility, metadata_token
FROM api_items
WHERE member_name = '' AND metadata_token != 0
  AND (lower(type_name) = lower($q) OR lower(replace(full_type_name, '+', '.')) = lower($nq))
ORDER BY CASE WHEN assembly = 'Stardew Valley' THEN 0 ELSE 1 END, length(full_type_name) ASC
LIMIT 5;";
                cmdType.Parameters.AddWithValue("$q", q);
                cmdType.Parameters.AddWithValue("$nq", normQ);
                using var r = cmdType.ExecuteReader();
                while (r.Read())
                    results.Add(ReadApiRecordWithToken(r));
            }
            if (results.Count > 0)
                return results;

            // 2. Check Type.Member match
            int lastDot = normQ.LastIndexOf('.');
            if (lastDot > 0 && lastDot < normQ.Length - 1)
            {
                string typePart = normQ.Substring(0, lastDot);
                string memberPart = normQ.Substring(lastDot + 1);

                using var cmdMem = _conn.CreateCommand();
                cmdMem.CommandText = @"
SELECT assembly, kind, namespace, type_name, full_type_name, member_name, signature, summary, accessibility, metadata_token
FROM api_items
WHERE metadata_token != 0
  AND (lower(member_name) = lower($mem) OR ($mem = '.ctor' AND kind = 'constructor'))
  AND (lower(type_name) = lower($tp)
       OR lower(replace(full_type_name, '+', '.')) = lower($tp)
       OR lower(replace(full_type_name, '+', '.')) LIKE '%.' || lower($tp))
ORDER BY CASE WHEN assembly = 'Stardew Valley' THEN 0 ELSE 1 END, length(full_type_name) ASC;";
                cmdMem.Parameters.AddWithValue("$mem", memberPart);
                cmdMem.Parameters.AddWithValue("$tp", typePart);
                using var r = cmdMem.ExecuteReader();
                while (r.Read())
                    results.Add(ReadApiRecordWithToken(r));

                if (results.Count > 0)
                {
                    // Keep only matches belonging to the best-matching declaring FullTypeName
                    string bestType = results[0].FullTypeName;
                    return results.Where(x => x.FullTypeName == bestType).ToList();
                }
            }

            // 3. Fallback: bare unambiguous method name
            using (var cmdBare = _conn.CreateCommand())
            {
                cmdBare.CommandText = @"
SELECT assembly, kind, namespace, type_name, full_type_name, member_name, signature, summary, accessibility, metadata_token
FROM api_items
WHERE lower(member_name) = lower($q) AND kind = 'method' AND metadata_token != 0
LIMIT 10;";
                cmdBare.Parameters.AddWithValue("$q", q);
                using var r = cmdBare.ExecuteReader();
                while (r.Read())
                    results.Add(ReadApiRecordWithToken(r));
            }
            return results;
        }

        private static ApiRecord ReadApiRecordWithToken(SqliteDataReader r) => new ApiRecord
        {
            Assembly = r.GetString(0),
            Kind = r.GetString(1),
            Namespace = r.GetString(2),
            TypeName = r.GetString(3),
            FullTypeName = r.GetString(4),
            MemberName = r.GetString(5),
            Signature = r.GetString(6),
            Summary = r.GetString(7),
            Accessibility = r.GetString(8),
            MetadataToken = r.GetInt32(9)
        };

        private static List<ApiRecord> RankAndFilterApiCandidates(
            List<(ApiRecord Record, double Bm25)> candidates,
            string rawQuery,
            int limit)
        {
            var queryWords = ExtractQueryWords(rawQuery);
            string compactRaw = Regex.Replace(rawQuery, @"[\s_]+", "");

            string? dotType = null;
            string? dotMember = null;
            int lastDot = rawQuery.LastIndexOf('.');
            if (lastDot > 0 && lastDot < rawQuery.Length - 1 && !rawQuery.Contains(' '))
            {
                dotType = rawQuery.Substring(0, lastDot).Trim();
                dotMember = rawQuery.Substring(lastDot + 1).Trim();
            }

            var scored = new List<(ApiRecord Record, double Score, int MemberHits)>(candidates.Count);

            foreach (var (a, bm25) in candidates)
            {
                // SQLite bm25 is negative (more negative = better). Cap base contribution so structural match dominates.
                double score = Math.Min(35.0, Math.Max(0.0, -bm25));
                bool isTypeRow = string.IsNullOrEmpty(a.MemberName);

                // 1. Exact & contiguous identifier matches
                if (!isTypeRow && a.MemberName.Equals(compactRaw, StringComparison.OrdinalIgnoreCase))
                    score += 260.0;
                else if (isTypeRow && (a.TypeName.Equals(compactRaw, StringComparison.OrdinalIgnoreCase) || a.FullTypeName.Equals(rawQuery, StringComparison.OrdinalIgnoreCase)))
                    score += 240.0;
                else if (!isTypeRow && compactRaw.Length >= 4 && a.MemberName.IndexOf(compactRaw, StringComparison.OrdinalIgnoreCase) >= 0)
                    score += 95.0;
                else if (isTypeRow && compactRaw.Length >= 4 && a.TypeName.IndexOf(compactRaw, StringComparison.OrdinalIgnoreCase) >= 0)
                    score += 70.0;

                // Explicit Type.Member lookup boost
                if (dotType != null && dotMember != null)
                {
                    bool typeMatches = a.TypeName.Equals(dotType, StringComparison.OrdinalIgnoreCase)
                                    || a.FullTypeName.EndsWith("." + dotType, StringComparison.OrdinalIgnoreCase)
                                    || a.FullTypeName.Equals(dotType, StringComparison.OrdinalIgnoreCase);
                    if (typeMatches && !isTypeRow)
                    {
                        if (a.MemberName.Equals(dotMember, StringComparison.OrdinalIgnoreCase))
                            score += 360.0;
                        else if (a.MemberName.StartsWith(dotMember, StringComparison.OrdinalIgnoreCase))
                            score += 240.0;
                    }
                }

                // 2. CamelCase word-level matching across MemberName vs TypeName vs Signature/Summary
                var memberWords = SplitIdentifierWords(a.MemberName);
                var typeWords = SplitIdentifierWords(a.TypeName);
                string sigLower = a.Signature.ToLowerInvariant();
                string sumLower = a.Summary.ToLowerInvariant();

                int memberExactHits = 0;
                int memberPrefixHits = 0;
                int typeExactHits = 0;
                int typePrefixHits = 0;
                int sigOrDocOnlyHits = 0;
                int coveredWords = 0;

                foreach (var w in queryWords)
                {
                    bool mExact = memberWords.Any(mw => mw == w);
                    bool mPrefix = !mExact && memberWords.Any(mw => mw.StartsWith(w, StringComparison.Ordinal) || (w.Length >= 4 && mw.Contains(w)));
                    bool tExact = typeWords.Any(tw => tw == w);
                    bool tPrefix = !tExact && typeWords.Any(tw => tw.StartsWith(w, StringComparison.Ordinal));
                    bool inSigOrDoc = sigLower.Contains(w) || sumLower.Contains(w);

                    if (mExact)
                    {
                        score += 48.0;
                        memberExactHits++;
                    }
                    else if (mPrefix)
                    {
                        score += 30.0;
                        memberPrefixHits++;
                    }

                    if (tExact)
                    {
                        score += isTypeRow ? 42.0 : 16.0;
                        typeExactHits++;
                    }
                    else if (tPrefix)
                    {
                        score += isTypeRow ? 24.0 : 7.0;
                        typePrefixHits++;
                    }

                    if (!mExact && !mPrefix && !tExact && !tPrefix && inSigOrDoc)
                    {
                        score += 14.0;
                        sigOrDocOnlyHits++;
                    }

                    if (mExact || mPrefix || tExact || tPrefix || inSigOrDoc)
                        coveredWords++;
                }

                int memberHits = memberExactHits + memberPrefixHits;
                int typeHits = typeExactHits + typePrefixHits;
                bool isCtor = a.Kind == "constructor";

                // 3. Multi-word member synergy: reward methods/events whose own name matches multiple query words
                if (!isCtor && memberHits >= 2)
                {
                    score += memberHits * 36.0;
                }

                // 4. Complementary Type + Member synergy: only when MemberName matches at least one query word NOT already in TypeName!
                if (!isCtor && queryWords.Count >= 2 && memberHits >= 1 && typeHits >= 1)
                {
                    bool hasDistinctMemberWord = queryWords.Any(w =>
                        memberWords.Any(mw => mw == w || mw.StartsWith(w, StringComparison.Ordinal)) &&
                        !typeWords.Any(tw => tw == w || tw.StartsWith(w, StringComparison.Ordinal)));
                    if (hasDistinctMemberWord)
                    {
                        score += 65.0;
                    }
                }

                if (queryWords.Count >= 2 && coveredWords == queryWords.Count)
                {
                    score += 28.0;
                }

                // 5. Anti-class-flooding: penalize member rows whose own member_name matched 0 query words (or ctor repeating class name)
                if ((!isTypeRow && memberHits == 0 && dotType == null) || (isCtor && dotType == null))
                {
                    score -= isCtor ? 25.0 : (sigOrDocOnlyHits > 0 ? 20.0 : 60.0);
                }

                // 6. Kind preference (no penalty for private methods — Harmony patches private methods heavily!)
                score += a.Kind switch
                {
                    "method" => 6.0,
                    "event" => 6.0,
                    "property" => 4.0,
                    "class" or "interface" or "struct" or "enum" => 3.0,
                    "constructor" => -5.0,
                    _ => 0.0
                };

                scored.Add((a, score, memberHits));
            }

            scored.Sort((x, y) => y.Score.CompareTo(x.Score));

            // Cap zero-member-hit rows and constructor overloads per declaring type so they never flood top N
            var finalResults = new List<ApiRecord>(limit);
            var zeroMemberTypeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var ctorTypeCounts = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var item in scored)
            {
                if (item.Record.Kind == "constructor" && dotType == null)
                {
                    ctorTypeCounts.TryGetValue(item.Record.FullTypeName, out int cCnt);
                    if (cCnt >= 1)
                        continue;
                    ctorTypeCounts[item.Record.FullTypeName] = cCnt + 1;
                }
                else if (!string.IsNullOrEmpty(item.Record.MemberName) && item.MemberHits == 0 && dotType == null)
                {
                    zeroMemberTypeCounts.TryGetValue(item.Record.FullTypeName, out int cnt);
                    if (cnt >= 2)
                        continue;
                    zeroMemberTypeCounts[item.Record.FullTypeName] = cnt + 1;
                }

                finalResults.Add(item.Record);
                if (finalResults.Count >= limit)
                    break;
            }

            return finalResults;
        }

        public List<SchemaRecord> SearchSchemas(string query, int limit)
        {
            var results = new List<SchemaRecord>();
            if (string.IsNullOrWhiteSpace(query))
            {
                using var listCmd = _conn.CreateCommand();
                listCmd.CommandText = @"SELECT asset_path, type_name, full_type_name, kind, summary, fields_json, fields_text
                    FROM game_schemas
                    ORDER BY CASE WHEN asset_path != '' THEN 0 ELSE 1 END, asset_path, type_name
                    LIMIT $lim;";
                listCmd.Parameters.AddWithValue("$lim", limit);
                using var lr = listCmd.ExecuteReader();
                while (lr.Read())
                    results.Add(ReadSchema(lr));
                return results;
            }

            string ftsQuery = BuildFtsQuery(query, useOr: false);
            results = ExecuteSchemaFts(ftsQuery, query, limit);
            if (results.Count == 0)
            {
                string orQuery = BuildFtsQuery(query, useOr: true);
                if (!string.IsNullOrWhiteSpace(orQuery) && orQuery != ftsQuery)
                    results = ExecuteSchemaFts(orQuery, query, limit);
            }
            return results;
        }

        private List<SchemaRecord> ExecuteSchemaFts(string ftsQuery, string rawQuery, int limit)
        {
            var results = new List<SchemaRecord>();
            if (string.IsNullOrWhiteSpace(ftsQuery))
                return results;

            using var cmd = _conn.CreateCommand();
            cmd.CommandText = @"
SELECT s.asset_path, s.type_name, s.full_type_name, s.kind, s.summary, s.fields_json, s.fields_text,
       bm25(schema_fts, 12.0, 12.0, 6.0, 3.0, 2.0, 4.0)
       + CASE WHEN lower(s.type_name) = lower($raw) OR lower(s.asset_path) = lower($raw) OR lower(s.asset_path) = 'data/' || lower($raw) THEN -80.0 ELSE 0.0 END AS score
FROM schema_fts
JOIN game_schemas s ON s.id = schema_fts.rowid
WHERE schema_fts MATCH $q
ORDER BY score ASC
LIMIT $lim;";
            cmd.Parameters.AddWithValue("$q", ftsQuery);
            cmd.Parameters.AddWithValue("$raw", rawQuery.Trim().Replace('\\', '/'));
            cmd.Parameters.AddWithValue("$lim", limit);

            using var r = cmd.ExecuteReader();
            while (r.Read())
                results.Add(ReadSchema(r));
            return results;
        }

        private static SchemaRecord ReadSchema(SqliteDataReader r) => new SchemaRecord
        {
            AssetPath = r.GetString(0),
            TypeName = r.GetString(1),
            FullTypeName = r.GetString(2),
            Kind = r.GetString(3),
            Summary = r.GetString(4),
            FieldsJson = r.GetString(5),
            FieldsText = r.GetString(6)
        };

        public List<VanillaDataRecord> SearchVanillaData(string query, string? assetFilter, int limit)
        {
            var results = new List<VanillaDataRecord>();

            // Fast exact match check when querying by exact ID + assetFilter or exact zh/en name
            if (string.IsNullOrWhiteSpace(query) && !string.IsNullOrWhiteSpace(assetFilter))
            {
                using var listCmd = _conn.CreateCommand();
                listCmd.CommandText = @"SELECT asset_name, entry_key, en_name, zh_name, summary, raw_json
                    FROM vanilla_data
                    WHERE lower(asset_name) = lower($af) OR lower(asset_name) = 'data/' || lower($af) OR lower(asset_name) LIKE '%' || lower($af) || '%'
                    LIMIT $lim;";
                listCmd.Parameters.AddWithValue("$af", assetFilter!.Replace('\\', '/'));
                listCmd.Parameters.AddWithValue("$lim", limit);
                using var lr = listCmd.ExecuteReader();
                while (lr.Read())
                    results.Add(ReadData(lr));
                return results;
            }

            string trimmed = query.Trim();
            string ftsQuery = BuildFtsQuery(trimmed, useOr: false);
            if (string.IsNullOrWhiteSpace(ftsQuery))
                return results;

            results = ExecuteDataFts(ftsQuery, trimmed, assetFilter, limit);
            if (results.Count == 0)
            {
                string orQuery = BuildFtsQuery(trimmed, useOr: true);
                if (!string.IsNullOrWhiteSpace(orQuery) && orQuery != ftsQuery)
                    results = ExecuteDataFts(orQuery, trimmed, assetFilter, limit);
            }
            return results;
        }

        private List<VanillaDataRecord> ExecuteDataFts(string ftsQuery, string trimmed, string? assetFilter, int limit)
        {
            var results = new List<VanillaDataRecord>();
            using var cmd = _conn.CreateCommand();
            var sb = new StringBuilder(@"
SELECT d.asset_name, d.entry_key, d.en_name, d.zh_name, d.summary, d.raw_json,
       bm25(data_fts, 4.0, 12.0, 10.0, 12.0, 3.0, 4.0)
       + CASE WHEN lower(d.entry_key) = lower($raw) OR lower(d.zh_name) = lower($raw) OR lower(d.en_name) = lower($raw) THEN -80.0 ELSE 0.0 END AS score
FROM data_fts
JOIN vanilla_data d ON d.id = data_fts.rowid
WHERE data_fts MATCH $q");

            cmd.Parameters.AddWithValue("$q", ftsQuery);
            cmd.Parameters.AddWithValue("$raw", trimmed);

            if (!string.IsNullOrWhiteSpace(assetFilter))
            {
                sb.Append(" AND (lower(d.asset_name) = lower($af) OR lower(d.asset_name) = 'data/' || lower($af) OR lower(d.asset_name) LIKE '%' || lower($af) || '%')");
                cmd.Parameters.AddWithValue("$af", assetFilter!.Replace('\\', '/'));
            }

            sb.Append(" ORDER BY score ASC LIMIT $lim;");
            cmd.Parameters.AddWithValue("$lim", limit);
            cmd.CommandText = sb.ToString();

            using var r = cmd.ExecuteReader();
            while (r.Read())
                results.Add(ReadData(r));
            return results;
        }

        public List<string> ListDataAssets()
        {
            var list = new List<string>();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT asset_name, COUNT(*) FROM vanilla_data GROUP BY asset_name ORDER BY asset_name;";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add($"{r.GetString(0)} ({r.GetInt64(1)})");
            return list;
        }

        private static VanillaDataRecord ReadData(SqliteDataReader r) => new VanillaDataRecord
        {
            AssetName = r.GetString(0),
            EntryKey = r.GetString(1),
            EnName = r.GetString(2),
            ZhName = r.GetString(3),
            Summary = r.GetString(4),
            RawJson = r.GetString(5)
        };

        public List<DocRecord> SearchDocs(string query, string? categoryFilter, int limit)
        {
            var results = new List<DocRecord>();
            if (string.IsNullOrWhiteSpace(query))
            {
                using var listCmd = _conn.CreateCommand();
                var lsb = new StringBuilder("SELECT source, category, title, url_or_path, content FROM docs");
                if (!string.IsNullOrWhiteSpace(categoryFilter))
                {
                    lsb.Append(" WHERE lower(category) LIKE '%' || lower($cat) || '%'");
                    listCmd.Parameters.AddWithValue("$cat", categoryFilter!);
                }
                lsb.Append(" ORDER BY category, title LIMIT $lim;");
                listCmd.Parameters.AddWithValue("$lim", limit);
                listCmd.CommandText = lsb.ToString();
                using var lr = listCmd.ExecuteReader();
                while (lr.Read())
                    results.Add(ReadDoc(lr));
                return results;
            }

            string ftsQuery = BuildFtsQuery(query, useOr: false);
            results = ExecuteDocsFts(ftsQuery, query, categoryFilter, limit);
            if (results.Count == 0)
            {
                string orQuery = BuildFtsQuery(query, useOr: true);
                if (!string.IsNullOrWhiteSpace(orQuery) && orQuery != ftsQuery)
                    results = ExecuteDocsFts(orQuery, query, categoryFilter, limit);
            }
            return results;
        }

        private List<DocRecord> ExecuteDocsFts(string ftsQuery, string rawQuery, string? categoryFilter, int limit)
        {
            var results = new List<DocRecord>();
            if (string.IsNullOrWhiteSpace(ftsQuery))
                return results;

            var symMatch = Regex.Match(rawQuery, @"[$#%@][a-zA-Z0-9_]+");
            string? specialSym = symMatch.Success ? symMatch.Value : null;

            using var cmd = _conn.CreateCommand();
            var sb = new StringBuilder(@"
SELECT d.source, d.category, d.title, d.url_or_path, d.content,
       bm25(docs_fts, 6.0, 12.0, 2.5, 3.0)
       + CASE WHEN lower(d.title) LIKE '%' || lower($raw) || '%' THEN -35.0 ELSE 0.0 END
       + CASE WHEN instr(lower(d.content), lower($raw)) > 0 THEN -40.0 ELSE 0.0 END
       + CASE WHEN lower(d.title) LIKE '%overview%' OR lower(d.title) LIKE '%data format%' OR lower(d.title) LIKE '% — format%' THEN -8.0 ELSE 0.0 END
       + CASE WHEN $sym IS NOT NULL AND instr(lower(d.content), lower($sym)) > 0 THEN -60.0 ELSE 0.0 END AS score
FROM docs_fts
JOIN docs d ON d.id = docs_fts.rowid
WHERE docs_fts MATCH $q");

            cmd.Parameters.AddWithValue("$q", ftsQuery);
            cmd.Parameters.AddWithValue("$raw", rawQuery.Trim());
            cmd.Parameters.AddWithValue("$sym", (object?)specialSym ?? DBNull.Value);

            if (!string.IsNullOrWhiteSpace(categoryFilter))
            {
                sb.Append(" AND lower(d.category) LIKE '%' || lower($cat) || '%'");
                cmd.Parameters.AddWithValue("$cat", categoryFilter!);
            }

            sb.Append(" ORDER BY score ASC LIMIT $lim;");
            cmd.Parameters.AddWithValue("$lim", limit);
            cmd.CommandText = sb.ToString();

            using var r = cmd.ExecuteReader();
            while (r.Read())
                results.Add(ReadDoc(r));
            return results;
        }

        private static DocRecord ReadDoc(SqliteDataReader r) => new DocRecord
        {
            Source = r.GetString(0),
            Category = r.GetString(1),
            Title = r.GetString(2),
            UrlOrPath = r.GetString(3),
            Content = r.GetString(4)
        };

        /// <summary>
        /// Splits PascalCase/camelCase/snake_case (including acronym prefixes like IModHelper / NPCController)
        /// and expands CJK characters into unigrams + bigrams so SQLite FTS5 unicode61 supports instant lookup.
        /// </summary>
        public static string BuildSearchTokens(params string?[] inputs)
        {
            var sb = new StringBuilder();
            foreach (var input in inputs)
            {
                if (string.IsNullOrWhiteSpace(input))
                    continue;

                string camelSplit = Regex.Replace(input!, @"([A-Z]+)([A-Z][a-z])", "$1 $2");
                camelSplit = Regex.Replace(camelSplit, @"([a-z0-9])([A-Z])", "$1 $2");
                camelSplit = Regex.Replace(camelSplit, @"[_./\\()\[\]<>,:;]+", " ");
                sb.Append(camelSplit).Append(' ');

                foreach (Match m in Regex.Matches(input!, @"[\u4e00-\u9fff]{2,}"))
                {
                    string cjk = m.Value;
                    for (int i = 0; i < cjk.Length; i++)
                    {
                        sb.Append(cjk[i]).Append(' ');
                        if (i + 1 < cjk.Length)
                        {
                            sb.Append(cjk[i]).Append(cjk[i + 1]).Append(' ');
                        }
                    }
                }
            }
            return sb.ToString();
        }

        internal static List<string> SplitIdentifierWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();

            string s = Regex.Replace(text, @"([A-Z]+)([A-Z][a-z])", "$1 $2");
            s = Regex.Replace(s, @"([a-z0-9])([A-Z])", "$1 $2");

            var list = new List<string>();
            foreach (Match m in Regex.Matches(s, @"[a-zA-Z0-9]+"))
            {
                list.Add(m.Value.ToLowerInvariant());
            }
            return list;
        }

        internal static List<string> ExtractQueryWords(string rawQuery)
        {
            var words = new List<string>();
            if (string.IsNullOrWhiteSpace(rawQuery))
                return words;

            string cleaned = Regex.Replace(rawQuery, @"[./:\\()\[\]<>,\-""'`?!;_]+", " ");
            foreach (var part in cleaned.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (Regex.IsMatch(part, @"^[a-zA-Z0-9]+$"))
                {
                    var sub = SplitIdentifierWords(part);
                    if (sub.Count > 1 && sub.Any(w => w.Length >= 2))
                        words.AddRange(sub.Where(w => w.Length >= 2));
                    else
                        words.AddRange(sub);
                }
                else
                {
                    foreach (Match cm in Regex.Matches(part, @"[\u4e00-\u9fff]+|[a-zA-Z0-9]+"))
                    {
                        if (Regex.IsMatch(cm.Value, @"^[a-zA-Z0-9]+$"))
                            words.AddRange(SplitIdentifierWords(cm.Value));
                        else
                            words.Add(cm.Value);
                    }
                }
            }

            return words.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static string BuildFtsQuery(string rawQuery, bool useOr = false)
        {
            if (string.IsNullOrWhiteSpace(rawQuery))
                return string.Empty;

            var terms = new List<string>();

            // Split on whitespace and common separators including underscore
            string cleaned = Regex.Replace(rawQuery, @"[./:\\()\[\]<>,\-""'`?!;_]+", " ");
            foreach (var part in cleaned.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (Regex.IsMatch(part, @"[\u4e00-\u9fff]"))
                {
                    string cjkOnly = Regex.Replace(part, @"[^\u4e00-\u9fff0-9a-zA-Z]", "");
                    foreach (Match cm in Regex.Matches(cjkOnly, @"[\u4e00-\u9fff]+|[a-zA-Z0-9]+"))
                    {
                        string v = cm.Value;
                        if (Regex.IsMatch(v[0].ToString(), @"[\u4e00-\u9fff]"))
                        {
                            if (v.Length == 1)
                                terms.Add($"\"{v}\"");
                            else
                            {
                                for (int i = 0; i + 1 < v.Length; i++)
                                    terms.Add($"\"{v.Substring(i, 2)}\"");
                            }
                        }
                        else
                        {
                            AddSplitAsciiTerms(v, terms);
                        }
                    }
                }
                else
                {
                    string safe = Regex.Replace(part, @"[^a-zA-Z0-9]", "");
                    if (safe.Length > 0)
                    {
                        AddSplitAsciiTerms(safe, terms);
                    }
                }
            }

            if (terms.Count == 0)
                return string.Empty;

            var distinct = terms.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            string joinOp = useOr ? " OR " : " AND ";
            return string.Join(joinOp, distinct);
        }

        private static void AddSplitAsciiTerms(string asciiToken, List<string> terms)
        {
            var words = SplitIdentifierWords(asciiToken);
            if (words.Count > 1 && words.Any(w => w.Length >= 2))
            {
                foreach (var w in words.Where(w => w.Length >= 2))
                    terms.Add($"\"{w}\"*");
            }
            else
            {
                foreach (var w in words)
                    terms.Add($"\"{w}\"*");
            }
        }

        private void ExecuteNonQuery(string sql, SqliteTransaction? tx = null)
        {
            using var cmd = _conn.CreateCommand();
            if (tx != null)
                cmd.Transaction = tx;
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        private long ExecuteScalarLong(string sql)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = sql;
            object? res = cmd.ExecuteScalar();
            return res is long l ? l : Convert.ToInt64(res ?? 0);
        }

        public void Dispose()
        {
            _conn.Dispose();
        }
    }
}
