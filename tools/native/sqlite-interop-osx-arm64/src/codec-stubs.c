/*
 * The public System.Data.SQLite build ships no SEE codec, but its managed
 * assembly still imports sqlite3_key/sqlite3_rekey - the shipped osx-x64 binary
 * exports both. The stock amalgamation only defines them when built with
 * SQLITE_HAS_CODEC, which needs the (commercial) SEE source.
 *
 * These stubs keep the export set aligned with the shipped binaries, so a
 * Password= connection string fails with SQLITE_ERROR the way it does on every
 * other platform rather than with an EntryPointNotFoundException.
 */

struct sqlite3;

int sqlite3_key(struct sqlite3 *db, const void *pKey, int nKey)
{
    (void)db; (void)pKey; (void)nKey;
    return 1; /* SQLITE_ERROR */
}

int sqlite3_rekey(struct sqlite3 *db, const void *pKey, int nKey)
{
    (void)db; (void)pKey; (void)nKey;
    return 1; /* SQLITE_ERROR */
}
