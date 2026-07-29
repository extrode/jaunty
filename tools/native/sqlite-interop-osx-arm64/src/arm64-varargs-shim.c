/*
 * arm64 variadic-ABI shim for System.Data.SQLite.
 *
 * sqlite3_config() and sqlite3_db_config() are variadic. System.Data.SQLite
 * P/Invokes them through several *fixed*-signature declarations instead
 * (sqlite3_config_log, sqlite3_db_config_int_refint, ...), because .NET has no
 * portable way to express a C vararg call.
 *
 * On x86-64 that mismatch is harmless: a variadic callee reads its arguments
 * from the same registers a fixed-signature caller puts them in. On Apple arm64
 * it is fatal. There, a variadic callee reads variadic arguments off the stack
 * while a fixed-signature caller passes them in x1..x7, so the callee sees
 * whatever happened to be on the stack.
 *
 * The observable symptom is not an error at the config call - that appears to
 * succeed. SQLite stores a garbage function pointer for SQLITE_CONFIG_LOG and
 * jumps through it the first time it logs anything, which in this test suite is
 * the SQLITE_WARNING_AUTOINDEX that constructAutomaticIndex() emits on a
 * multi-table join. The process dies with SIGBUS somewhere in sqlite3_prepare,
 * far from the actual mistake.
 *
 * These wrappers take fixed arguments - so the calling convention matches the
 * managed caller - and forward to the real variadic entry points with the
 * argument shape each configuration verb actually expects. The linker aliases
 * the mangled export names onto them; see build.sh.
 *
 * The verb sets below are closed. System.Data.SQLite raises a managed exception
 * for any option outside them before reaching native code:
 *   sqlite3_config    - SQLite3.cs StaticIsInitialized, StaticSetMemoryStatus, SetLogCallback
 *   sqlite3_db_config - SQLite3.cs SetConfigurationOption
 *
 * Other variadic sqlite3 entry points that System.Data.SQLite imports need no
 * wrapper: sqlite3_log() passes only the two arguments that are fixed in its own
 * declaration, and sqlite3_mprintf() is declared with __arglist, which the
 * runtime marshals using the platform's real vararg convention.
 */

#include <stdint.h>

extern int sqlite3_config(int, ...);
extern int sqlite3_db_config(void *, int, ...);

#define SQLITE_CONFIG_NONE          0    /* System.Data.SQLite's "is it initialized yet" probe */
#define SQLITE_CONFIG_MEMSTATUS     9    /* int                */
#define SQLITE_CONFIG_LOG           16   /* callback, void*    */

#define SQLITE_DBCONFIG_MAINDBNAME  1000 /* const char*        */
#define SQLITE_DBCONFIG_LOOKASIDE   1001 /* void*, int, int    */
                                         /* 1002+  the SQLITE_DBCONFIG_ENABLE_* family: int, int* */

typedef void (*jaunty_sqlite_log_callback)(void *, int, const char *);

int jaunty_shim_sqlite3_config(int op, uintptr_t a1, uintptr_t a2)
{
    switch (op)
    {
        case SQLITE_CONFIG_LOG:
            return sqlite3_config(op, (jaunty_sqlite_log_callback)a1, (void *)a2);

        case SQLITE_CONFIG_MEMSTATUS:
            return sqlite3_config(op, (int)a1);

        default: /* SQLITE_CONFIG_NONE, and any other verb taking no argument */
            return sqlite3_config(op);
    }
}

int jaunty_shim_sqlite3_db_config(void *db, int op, uintptr_t a1, uintptr_t a2, uintptr_t a3)
{
    switch (op)
    {
        case SQLITE_DBCONFIG_MAINDBNAME:
            return sqlite3_db_config(db, op, (const char *)a1);

        case SQLITE_DBCONFIG_LOOKASIDE:
            return sqlite3_db_config(db, op, (void *)a1, (int)a2, (int)a3);

        default:
            return sqlite3_db_config(db, op, (int)a1, (int *)a2);
    }
}
