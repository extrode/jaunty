namespace Jaunty.Internals.Write;

/// <summary>
/// Validates entity collections passed to Bulk* write methods before any parameter binding
/// begins, serving the same purpose as the <c>ArgumentNullException.ThrowIfNull(entity)</c> guard
/// single-entity Insert/Update/Delete/Upsert already apply - without this, a null item reaches
/// the compiled/reflection-based parameter binder unchecked and throws an opaque
/// NullReferenceException instead of identifying which item in the batch was null. Deliberately
/// throws <see cref="ArgumentException"/> rather than <see cref="ArgumentNullException"/> so the
/// message can embed the offending batch index - this is not the same exception type as the
/// single-entity guard.
/// </summary>
internal static class BulkEntityValidator
{
    internal static void ThrowIfAnyNull<T>(IList<T> entityList, string paramName)
    {
        for (int i = 0; i < entityList.Count; i++)
        {
            if (entityList[i] is null)
                throw new ArgumentException($"Entity at index {i} is null.", paramName);
        }
    }
}
