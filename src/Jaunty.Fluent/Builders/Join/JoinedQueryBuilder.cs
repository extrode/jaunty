using System.Data;
using System.Text;
using System.Text.RegularExpressions;

using Jaunty.Dialects;
using Jaunty.Fluent.Internals;
using Jaunty.Internals.Entity;

namespace Jaunty.Fluent;

/// <summary>
/// Query builder for 2-table joins.
/// </summary>
internal sealed partial class JoinedQueryBuilder<TFrom, TJoin> : IJoinedQuery<TFrom, TJoin>
    where TFrom : new()
    where TJoin : new()
{
    private readonly IDbConnection _connection;
    private readonly ISqlDialect _dialect;
    private readonly string _fromTable;
    private readonly string? _fromSchema;
    private readonly string? _fromAlias;
    private readonly List<JoinInfo> _joins = [];
    private readonly List<WhereCondition> _conditions = [];
    private readonly List<OrderByColumn> _orderByColumns = [];
    private readonly ParameterCollection _parameters = new();
    private int _paramSeq;
    private readonly EntityMetadata _fromMetadata;
    private readonly EntityMetadata _joinMetadata;

    internal JoinedQueryBuilder(
        IDbConnection connection,
        ISqlDialect dialect,
        string fromTable,
        string? fromSchema,
        string? fromAlias,
        JoinInfo firstJoin)
    {
        _connection = connection;
        _dialect = dialect;
        _fromTable = fromTable;
        _fromSchema = fromSchema;
        _fromAlias = fromAlias;
        _joins.Add(firstJoin);
        _fromMetadata = FluentMetadataCache.GetMetadata<TFrom>();
        _joinMetadata = FluentMetadataCache.GetMetadata<TJoin>();
    }

    internal string? FromAlias => _fromAlias;
    internal string FromTable => _fromTable;
    internal string? FromSchema => _fromSchema;
    internal ISqlDialect Dialect => _dialect;
    internal IDbConnection Connection => _connection;
    internal List<JoinInfo> Joins => _joins;
    internal List<WhereCondition> Conditions => _conditions;
    internal List<OrderByColumn> GetOrderByColumns() => _orderByColumns;
    internal ParameterCollection GetParameters() => _parameters;

    internal void AddOrderByColumn(string columnName, string direction)
    {
        _orderByColumns.Add(new OrderByColumn(columnName, direction == "DESC"));
    }

    internal string BuildCountSql()
    {
        var sb = new StringBuilder(128);
        sb.Append("SELECT COUNT(*) FROM ");
        sb.Append(_dialect.EscapeTableName(_fromSchema, _fromTable));

        if (_fromAlias is not null)
        {
            sb.Append(' ');
            sb.Append(_fromAlias);
        }

        foreach (JoinInfo join in _joins)
        {
            sb.Append(' ');
            sb.Append(join.JoinKeyword);
            sb.Append(' ');
            sb.Append(_dialect.EscapeTableName(join.SchemaName, join.TableName));

            if (join.Alias is not null)
            {
                sb.Append(' ');
                sb.Append(join.Alias);
            }

            sb.Append(" ON ");
            sb.Append(join.OnCondition);
        }

        if (_conditions.Count > 0)
        {
            sb.Append(" WHERE ");
            sb.Append(BuildWhereExpression(_conditions));
        }

        return sb.ToString();
    }

    internal string BuildSelectPartialSql(string columns)
    {
        var sb = new StringBuilder(256);
        sb.Append("SELECT ");
        sb.Append(columns);
        sb.Append(" FROM ");
        sb.Append(_dialect.EscapeTableName(_fromSchema, _fromTable));

        if (_fromAlias is not null)
        {
            sb.Append(' ');
            sb.Append(_fromAlias);
        }

        foreach (JoinInfo join in _joins)
        {
            sb.Append(' ');
            sb.Append(join.JoinKeyword);
            sb.Append(' ');
            sb.Append(_dialect.EscapeTableName(join.SchemaName, join.TableName));

            if (join.Alias is not null)
            {
                sb.Append(' ');
                sb.Append(join.Alias);
            }

            sb.Append(" ON ");
            sb.Append(join.OnCondition);
        }

        if (_conditions.Count > 0)
        {
            sb.Append(" WHERE ");
            sb.Append(BuildWhereExpression(_conditions));
        }

        if (_orderByColumns.Count > 0)
        {
            sb.Append(" ORDER BY ");

            for (var i = 0; i < _orderByColumns.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");

                OrderByColumn orderBy = _orderByColumns[i];
                sb.Append(orderBy.ColumnName);

                if (orderBy.Descending)
                    sb.Append(" DESC");
            }
        }

        return sb.ToString();
    }

    // FROM/JOIN/WHERE fragment shared with grouped-joined queries (spec 004's state-reuse
    // seam), which append GROUP BY/HAVING instead of the plain column SELECT/ORDER BY that
    // BuildSelectSql/BuildCountSql/BuildSelectPartialSql produce.
    internal string BuildFromJoinWhereSql()
    {
        var sb = new StringBuilder(256);
        sb.Append(_dialect.EscapeTableName(_fromSchema, _fromTable));

        if (_fromAlias is not null)
        {
            sb.Append(' ');
            sb.Append(_fromAlias);
        }

        foreach (JoinInfo join in _joins)
        {
            sb.Append(' ');
            sb.Append(join.JoinKeyword);
            sb.Append(' ');
            sb.Append(_dialect.EscapeTableName(join.SchemaName, join.TableName));

            if (join.Alias is not null)
            {
                sb.Append(' ');
                sb.Append(join.Alias);
            }

            sb.Append(" ON ");
            sb.Append(join.OnCondition);
        }

        if (_conditions.Count > 0)
        {
            sb.Append(" WHERE ");
            sb.Append(BuildWhereExpression(_conditions));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Returns <typeparamref name="T"/>'s columns prefixed with the FROM alias.
    /// </summary>
    /// <remarks>
    /// AUD-R35-182. <typeparamref name="T"/> must be <typeparamref name="TFrom"/>. Nothing in the
    /// signature says so, and nothing enforces it: the columns come from
    /// <typeparamref name="T"/>'s metadata while the alias is always the FROM alias, so passing the
    /// joined entity emits that entity's columns qualified by the wrong table - SQL that is
    /// well-formed and wrong, or an "invalid column name" from the server. Every caller in the
    /// solution passes <typeparamref name="TFrom"/>, so this is a trap for the next caller rather
    /// than a live defect; the type parameter cannot simply be dropped because the
    /// <c>where T : new()</c> constraint is what lets the metadata lookup resolve.
    /// </remarks>
    internal string[] GetSelectColumns<T>() where T : new()
    {
        var metadata = FluentMetadataCache.GetMetadata<T>();
        return GetPrefixedColumns(metadata, _fromAlias);
    }

    public IJoinClause<TFrom, TJoin, T3> InnerJoin<T3>(string? alias = null) where T3 : new()
        => new JoinClause3Builder<TFrom, TJoin, T3>(this, JoinType.Inner, alias);

    public IJoinClause<TFrom, TJoin, T3> LeftJoin<T3>(string? alias = null) where T3 : new()
        => new JoinClause3Builder<TFrom, TJoin, T3>(this, JoinType.Left, alias);

    public IJoinClause<TFrom, TJoin, T3> RightJoin<T3>(string? alias = null) where T3 : new()
        => new JoinClause3Builder<TFrom, TJoin, T3>(this, JoinType.Right, alias);

    internal void AddJoin(JoinInfo join) => _joins.Add(join);

    /// <summary>
    /// Replaces a join this builder already holds, in place.
    /// </summary>
    /// <remarks>
    /// AUD-R35-179. The arity-3 and arity-4 clause builders mutate this shared root rather than
    /// constructing a fresh builder the way the arity-2 one does, so calling <c>On(...)</c> twice on
    /// one held clause builder appended the same join twice - two identical <c>INNER JOIN</c> clauses
    /// in the generated SQL, a silently changed <c>Count</c>, and <c>Joins[1]</c> pointing at the
    /// wrong join for arity-4 alias resolution. A clause builder describes one join, so re-calling
    /// <c>On</c> redefines it instead of adding another. The shared root itself is unchanged: two
    /// wrappers handed out by one clause builder still see one query, and the last <c>On</c> wins for
    /// both. Making each call yield an independent builder means cloning the root's joins,
    /// conditions, parameters and sequence counter, which is a redesign rather than a fix; it is on
    /// the work-list.
    /// </remarks>
    internal void ReplaceJoin(JoinInfo existing, JoinInfo replacement)
    {
        int index = _joins.IndexOf(existing);

        if (index >= 0)
            _joins[index] = replacement;
        else
            _joins.Add(replacement);
    }

    internal void AddWhereCondition(WhereCondition condition) => _conditions.Add(condition);

    /// <summary>
    /// Adds a WHERE condition produced by a join-predicate expression visitor, uniquifying its
    /// parameter names against the ones the rest of the query has already bound. Each Where/And/Or
    /// call uses a fresh visitor that only knows the names it minted itself, so without this two
    /// conditions filtering the same column would collide and one bound value would silently serve
    /// both.
    /// </summary>
    internal void AddWhereExpression(string sql, List<(string Name, object? Value)> parameters, LogicalOperator op)
        => _conditions.Add(WhereCondition.Expression(RegisterExpressionParameters(sql, parameters), op));

    /// <summary>
    /// Binds the values a join-predicate visitor produced and rewrites any name that collides with
    /// one already in the query, returning the rewritten text. Used for both WHERE expressions and
    /// the arity-3/4 <c>On(predicate)</c> conditions, which land in a <see cref="JoinInfo"/> rather
    /// than a <see cref="WhereCondition"/> but are minted by a visitor with the same partial view
    /// of the query and so collide the same way.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This used to renumber every name onto a query-wide <c>jp&lt;n&gt;</c> sequence, which was
    /// sound while the visitors minted nothing else. Since they name a parameter after the column
    /// it filters, renumbering would throw that name away - so a colliding name is suffixed instead
    /// and a name that does not collide, which is nearly all of them, is left exactly as minted.
    /// </para>
    /// <para>
    /// The renames are still applied in a single pass. Doing them one at a time lets a new name
    /// capture a not-yet-rewritten occurrence of itself elsewhere in the same condition, after
    /// which one bound value serves both operands.
    /// </para>
    /// </remarks>
    internal string RegisterExpressionParameters(string sql, List<(string Name, object? Value)> parameters)
    {
        string finalSql = sql;

        if (parameters.Count > 0)
        {
            Dictionary<string, string>? renames = null;

            for (int i = 0; i < parameters.Count; i++)
            {
                (string mintedName, object? value) = parameters[i];
                string finalName = mintedName;

                for (int suffix = 2; IsNameTaken(finalName, parameters, i, renames); suffix++)
                    finalName = mintedName + "_" + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);

                if (!string.Equals(finalName, mintedName, StringComparison.Ordinal))
                {
                    renames ??= new Dictionary<string, string>(StringComparer.Ordinal);
                    renames[mintedName] = finalName;
                }

                _parameters.Add(finalName, value);
                _paramSeq++;
            }

            if (renames is not null)
                finalSql = ApplyRenames(finalSql, renames);
        }

        return finalSql;
    }

    /// <summary>
    /// Whether <paramref name="candidate"/> is already spoken for: bound by the query, minted by
    /// another parameter of this same condition, or handed out by an earlier rename in this batch.
    /// </summary>
    /// <remarks>
    /// The middle case is the one that is easy to miss. A condition can mint both
    /// <c>@p_category_id</c> and <c>@p_category_id_2</c>; suffixing the first on collision has to
    /// step over the second rather than land on it.
    /// </remarks>
    private bool IsNameTaken(
        string candidate,
        List<(string Name, object? Value)> minted,
        int self,
        Dictionary<string, string>? renames)
    {
        if (_parameters.Contains(candidate))
            return true;

        for (int i = 0; i < minted.Count; i++)
        {
            if (i != self && string.Equals(minted[i].Name, candidate, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (renames is not null)
        {
            foreach (KeyValuePair<string, string> rename in renames)
            {
                if (string.Equals(rename.Value, candidate, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Rewrites every renamed parameter in one pass.
    /// </summary>
    /// <remarks>
    /// The pattern is built from the names being renamed rather than cached per dialect prefix, the
    /// way AUD-R30's <c>jp\d+</c> pattern was: names are derived from columns now, so there is no
    /// one shape to match. It costs a regex parse only on the rare condition that collides at all -
    /// the common path exits above without building one.
    /// </remarks>
    private static string ApplyRenames(string sql, Dictionary<string, string> renames)
    {
        // Longest first, so a name that is a prefix of another cannot claim the shorter match. The
        // trailing lookahead is what makes "@p_category_id" leave "@p_category_id_2" alone.
        var names = new List<string>(renames.Keys);
        names.Sort(static (left, right) => right.Length.CompareTo(left.Length));

        var pattern = new StringBuilder("(?:");
        for (var i = 0; i < names.Count; i++)
        {
            if (i > 0)
                pattern.Append('|');

            pattern.Append(Regex.Escape(names[i]));
        }

        pattern.Append(")(?!\\w)");

        return Regex.Replace(sql, pattern.ToString(),
            m => renames.TryGetValue(m.Value, out string? renamed) ? renamed : m.Value);
    }

    /// <summary>
    /// Registers the value parameters bound by a two-table <c>On(predicate)</c> join expression.
    /// Nothing else in the query has bound a name yet at this point - the ON is what creates the
    /// joined builder - so the visitor's own names are taken as minted, and the query-wide sequence
    /// is advanced past them so a later <see cref="AddWhereExpression"/> sees them as taken.
    /// </summary>
    internal void AddOnParameters(List<(string Name, object? Value)> parameters)
    {
        for (int i = 0; i < parameters.Count; i++)
        {
            _parameters.Add(parameters[i].Name, parameters[i].Value);
            _paramSeq++;
        }
    }

    /// <summary>
    /// Folds WHERE conditions left-to-right, wrapping each step in parentheses so the
    /// generated SQL evaluates in the same order the fluent Where/And/Or chain was built,
    /// instead of relying on SQL's AND-before-OR operator precedence.
    /// </summary>
    private static string BuildWhereExpression(List<WhereCondition> conditions)
    {
        var expr = conditions[0].Sql;

        for (var i = 1; i < conditions.Count; i++)
        {
            var condition = conditions[i];
            var op = condition.Operator == LogicalOperator.Or ? "OR" : "AND";
            expr = $"({expr} {op} {condition.Sql})";
        }

        return expr;
    }

    internal void AddParameter<TValue>(string name, TValue value) =>
        _parameters.Add(name, value);

    /// <summary>
    /// Binds the operands a grouped-join HAVING predicate produced, renaming them against this
    /// query's parameter collection, and returns the rewritten HAVING text.
    /// </summary>
    /// <remarks>
    /// AUD-R35-016, the joined half. <c>JoinedGroupByExpressionVisitor</c> mints
    /// <c>"{prefix}jhp0".."jhpN"</c> from a per-visitor counter, and the three grouped-join
    /// builders pushed those names straight into <em>this</em> builder's collection - which every
    /// grouped builder derived from it shares. Two groupings off one join, each with a bound
    /// HAVING operand, therefore added <c>@jhp0</c> twice and the second threw a duplicate-name
    /// error the caller could not have caused. This is the same renumbering
    /// <see cref="RegisterExpressionParameters"/> already applies to the <c>jp</c> sequence, which
    /// collides for exactly the same reason; the <c>jhp</c> sequence never got it.
    /// <para>
    /// Renames are collected first and applied in one pass, for the reason spelled out on
    /// <see cref="RegisterExpressionParameters"/>: a one-at-a-time rewrite lets a new name capture
    /// an old occurrence that has not been rewritten yet.
    /// </para>
    /// </remarks>
    internal string RegisterHavingParameters(string havingSql, List<(string Name, object? Value)> parameters)
    {
        if (parameters.Count == 0)
            return havingSql;

        var renames = new Dictionary<string, string>(parameters.Count, StringComparer.Ordinal);
        for (int i = 0; i < parameters.Count; i++)
        {
            (string oldName, object? value) = parameters[i];
            string newName = _parameters.CreateUniqueName(_dialect.ParameterPrefix, "jhp");
            renames[oldName] = newName;
            _parameters.Add(newName, value);
        }

        // The minted names are "<prefix>jhp<n>"; the replacements are "<prefix>jhp_<n>", which this
        // pattern does not match, so a rewritten name cannot be rewritten again.
        Regex renameRegex = HavingParameterRenameRegexes.Cache.GetOrAdd(_dialect.ParameterPrefix,
            static prefix => new Regex(Regex.Escape(prefix) + @"jhp\d+(?!\w)", RegexOptions.Compiled));

        return renameRegex.Replace(havingSql,
            m => renames.TryGetValue(m.Value, out string? renamed) ? renamed : m.Value);
    }

    internal bool HasParameter(string name) => _parameters.Contains(name);

    /// <summary>
    /// What this builder binds, in the shape interceptors and <c>JauntyConfig.Logger</c>
    /// already understand. Nested grouped builders hold only a parent reference, so they
    /// cannot reach the parameter collection to report it themselves.
    /// </summary>
    internal object DescribeParameters() => _parameters.ToParameterObject();
    /// <summary>
    /// Binds all accumulated parameters directly to the command via raw ADO.NET (bypasses
    /// Jaunty's core Query&lt;T&gt;/ParameterBinder). Used by this builder's own SelectAll/
    /// SelectAllAsync as well as by every GroupedJoinedQueryBuilder{,3,4} (via
    /// _parent(.{_parent}).BindParameters) for HAVING parameter binding.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AUD-R26 (batch 5, medium/bug). This used to route every value through a local
    /// <c>NormalizeForBinding</c> that coerced any <see cref="decimal"/> to <see cref="double"/> -
    /// <c>value is decimal d ? (double)d : value</c> - unconditionally and on every dialect, on the
    /// strength of one provider's behaviour. It now asks the dialect, via
    /// <see cref="DecimalParameterBinding"/>: SQLite still gets the conversion, and SQL Server,
    /// PostgreSQL and MySQL no longer have a <c>DECIMAL(19,4)</c> or <c>NUMERIC</c> comparison
    /// downgraded to binary floating point on another engine's behalf.
    /// </para>
    /// <para>
    /// The conversion is genuinely needed where it now applies, which took two rounds of
    /// measurement to establish. Both SQLite providers bind a <see cref="decimal"/> as TEXT.
    /// Compared against a <em>column</em>, SQLite applies the column's affinity and converts it, so
    /// <c>WHERE price = @p</c> matches - that is the case the audit measured, and why the coercion
    /// looked like a workaround for nothing. Compared against an <em>expression</em> there is no
    /// affinity to apply, and TEXT sorts above every number, so
    /// <c>HAVING SUM(price) &gt; @p</c> matches no group and <c>&lt; @p</c> matches every group,
    /// whatever the values are. Removing it outright turned four <c>GroupBy</c>/<c>Having</c>
    /// integration tests red; that is what caught it.
    /// </para>
    /// <para>
    /// One asymmetry survives, now confined to SQLite, and it runs the other way from what you would
    /// expect - <b>this</b> path is the inexact one. The core binder behind <c>Delete</c>,
    /// <c>Update</c>, <c>Insert</c> and the joined <c>Select()</c> binds the <see cref="decimal"/>
    /// unchanged, which for a plain column comparison is <em>exact</em>, while the conversion here
    /// costs precision past 2^53. Measured against SQLite on a column holding 9007199254740993, with
    /// the same builder and the same <c>Where</c>:
    /// <code>
    /// .Where((i, c) =&gt; i.Amount == 9007199254740993m).Select()      -> 1 row
    /// .Where((i, c) =&gt; i.Amount == 9007199254740993m).SelectBoth()  -> 0 rows
    /// </code>
    /// Changing only the terminal changes the answer. This predates the dialect gate - the old
    /// coercion was unconditional, so it did this on SQLite too - and the gate neither caused nor
    /// cured it. It cannot be cured by converting less here either: this method binds one flat
    /// name/value list that carries both WHERE parameters and, via
    /// <c>GroupedJoinedQueryBuilder</c>, HAVING parameters, and HAVING is the case that needs the
    /// conversion. Fixing it properly means either scoping the conversion to HAVING parameters or
    /// casting in the generated SQL; both are round-27 work, recorded under AUD-R26-050.
    /// </para>
    /// </remarks>
    internal void BindParameters(IDbCommand command)
    {
        foreach ((string name, object? value) in _parameters.GetAll())
        {
            IDbDataParameter param = command.CreateParameter();
            param.ParameterName = name;
            param.Value = DecimalParameterBinding.Normalize(_dialect, value) ?? DBNull.Value;
            command.Parameters.Add(param);
        }
    }

    internal string[] GetPrefixedColumns(EntityMetadata metadata, string? alias)
    {
        IReadOnlyList<ColumnMetadata> columns = metadata.Columns;
        var result = new string[columns.Count];
        string prefix = alias ?? _dialect.EscapeTableName(metadata.SchemaName, metadata.TableName);

        for (int i = 0; i < columns.Count; i++)
        {
            string escaped = _dialect.EscapeColumnName(columns[i].ColumnName);
            result[i] = $"{prefix}.{escaped}";
        }

        return result;
    }

    internal string[] GetPrefixedColumnsWithAlias(EntityMetadata metadata, string? tableAlias, string columnPrefix)
    {
        IReadOnlyList<ColumnMetadata> columns = metadata.Columns;
        var result = new string[columns.Count];
        string prefix = tableAlias ?? _dialect.EscapeTableName(metadata.SchemaName, metadata.TableName);

        for (int i = 0; i < columns.Count; i++)
        {
            string colName = columns[i].ColumnName;
            string escaped = _dialect.EscapeColumnName(colName);
            result[i] = $"{prefix}.{escaped} AS {columnPrefix}{colName}";
        }

        return result;
    }

    internal string BuildSelectSql(string[] columns)
    {
        var sb = new StringBuilder(256);
        sb.Append("SELECT ");

        for (var i = 0; i < columns.Length; i++)
        {
            if (i > 0)
                sb.Append(", ");
            sb.Append(columns[i]);
        }

        sb.Append(" FROM ");
        sb.Append(_dialect.EscapeTableName(_fromSchema, _fromTable));

        if (_fromAlias is not null)
        {
            sb.Append(' ');
            sb.Append(_fromAlias);
        }

        foreach (JoinInfo join in _joins)
        {
            sb.Append(' ');
            sb.Append(join.JoinKeyword);
            sb.Append(' ');
            sb.Append(_dialect.EscapeTableName(join.SchemaName, join.TableName));

            if (join.Alias is not null)
            {
                sb.Append(' ');
                sb.Append(join.Alias);
            }

            sb.Append(" ON ");
            sb.Append(join.OnCondition);
        }

        if (_conditions.Count > 0)
        {
            sb.Append(" WHERE ");
            sb.Append(BuildWhereExpression(_conditions));
        }

        if (_orderByColumns.Count > 0)
        {
            sb.Append(" ORDER BY ");

            for (var i = 0; i < _orderByColumns.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");

                OrderByColumn orderBy = _orderByColumns[i];
                sb.Append(orderBy.ColumnName);

                if (orderBy.Descending)
                    sb.Append(" DESC");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Maps one entity out of a joined row by position, reading <paramref name="offset"/> onwards.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The builder writes the SELECT list itself, entity by entity and in
    /// <see cref="EntityMetadata.Columns"/> order, so each entity owns a contiguous run of ordinals
    /// and its start is the sum of the column counts before it. That is what allowed the
    /// <c>f_</c>/<c>j_</c> and <c>t1_</c>..<c>t4_</c> column aliases to go: they existed only so the
    /// name-based overload below could tell <c>products.category_id</c> from
    /// <c>categories.category_id</c> in one result set, and a position cannot be ambiguous.
    /// </para>
    /// <para>
    /// Unlike the name-based overload, a column cannot be missing here - a short reader is a bug in
    /// the builder rather than a result set that did not carry what was asked for - so there is no
    /// skip-if-absent branch. The DBNull skip stays: a null column leaves the property at its
    /// default, which is what the name-based path does.
    /// </para>
    /// </remarks>
    /// <param name="metadata">The entity's columns, in the order they were emitted.</param>
    /// <param name="reader">The open reader, positioned on a row.</param>
    /// <param name="offset">Ordinal of this entity's first column.</param>
    internal static TEntity MapEntity<TEntity>(EntityMetadata metadata, IDataReader reader, int offset)
        where TEntity : new()
    {
        var entity = new TEntity();
        IReadOnlyList<ColumnMetadata> columns = metadata.Columns;

        for (int i = 0; i < columns.Count; i++)
        {
            int ordinal = offset + i;

            if (reader.IsDBNull(ordinal))
                continue;

            ColumnMetadata col = columns[i];
            object value = reader.GetValue(ordinal);
            Type propertyType = col.PropertyType;
            Type targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            object convertedValue = GroupedJoinedResultMapper.ConvertColumnValue(value, targetType);

            if (col.Setter is { } setter)
                setter(entity!, convertedValue);
            else
                col.Property!.SetValue(entity, convertedValue);
        }

        return entity;
    }

    internal static TEntity MapEntity<TEntity>(EntityMetadata metadata, IDataReader reader, string prefix, Dictionary<string, int> ordinals)
        where TEntity : new()
    {
        var entity = new TEntity();
        IReadOnlyList<ColumnMetadata> columns = metadata.Columns;

        for (int i = 0; i < columns.Count; i++)
        {
            ColumnMetadata col = columns[i];
            string aliasName = $"{prefix}{col.ColumnName}";

            if (!ordinals.TryGetValue(aliasName, out int ordinal))
                continue; // Column not found, skip

            if (reader.IsDBNull(ordinal))
                continue;

            object value = reader.GetValue(ordinal);
            Type propertyType = col.PropertyType;
            Type targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            object convertedValue = GroupedJoinedResultMapper.ConvertColumnValue(value, targetType);

            if (col.Setter is { } setter)
                setter(entity!, convertedValue);
            else
                col.Property!.SetValue(entity, convertedValue);
        }

        return entity;
    }

    /// <summary>
    /// Builds an ordinal lookup for the reader's current column set, avoiding the
    /// exception-driven IndexOutOfRangeException-per-missing-column pattern that
    /// <see cref="IDataRecord.GetOrdinal(string)"/> relies on when a column isn't present.
    /// </summary>
    /// <remarks>
    /// AUD-R12: ordinals are static for the lifetime of a result set, so callers must build this
    /// once per reader before their row loop and reuse it across every
    /// <see cref="MapEntity{TEntity}(EntityMetadata, IDataReader, string, Dictionary{string, int})"/>
    /// call for that result set, instead of rebuilding it on every call (previously: once per
    /// entity per row).
    /// </remarks>
    /// <summary>
    /// Maps result-set column name to ordinal, case-insensitively, resolving a duplicate name to
    /// its <b>first</b> ordinal.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AUD-R26-059 (batch 5, low/consistency). The tie-break was undocumented, and the library holds
    /// three different answers to the same question: this resolves a duplicate to the first ordinal,
    /// the dictionary row-builders (<c>QueryPartialList</c>, <c>SpecialTypeMappers.CreateDictionaryMapper</c>)
    /// resolve it to the last via <c>row[columnNames[i]] = value</c>, and
    /// <c>EnsureNoAmbiguousColumns</c> throws with a message telling the caller how to
    /// disambiguate. All three use <see cref="StringComparer.OrdinalIgnoreCase"/>, so all three see
    /// the same collisions.
    /// </para>
    /// <para>
    /// First-wins is deliberate <em>here</em> and is not simply the row-builders' rule spelled
    /// differently. This lookup only ever sees a reader whose columns were aliased by
    /// <c>SelectBothInternal</c> with the disjoint <c>f_</c> and <c>j_</c> prefixes, so the ordinary
    /// unaliased-join collision the row-builders hit - <c>SELECT o.Id, c.id</c> - cannot occur. A
    /// duplicate reaching here means two prefixed aliases genuinely collided, and first-wins keeps
    /// the entity order the prefixes encode.
    /// </para>
    /// <para>
    /// Converging the three is deliberately not done from this finding: it is
    /// <c>EnsureNoAmbiguousColumns</c>'s throw that the other two should probably adopt, and that is
    /// a behavioural change to shipped query paths that belongs with the batch-1 finding against
    /// <c>QueryPartialList</c> rather than bolted on here. Recorded so whoever takes that one settles
    /// all three at once instead of leaving the library with two answers and a half.
    /// </para>
    /// </remarks>
    internal static Dictionary<string, int> BuildOrdinalLookup(IDataReader reader)
    {
        var map = new Dictionary<string, int>(reader.FieldCount, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < reader.FieldCount; i++)
        {
            string name = reader.GetName(i);
            if (!map.ContainsKey(name))
                map[name] = i;
        }

        return map;
    }
}

/// <summary>
/// AUD-R30: one compiled rename regex per dialect parameter prefix ('@', ':', '$'), shared across
/// every <see cref="JoinedQueryBuilder{TFrom, TJoin}"/> closed type - a static on the generic
/// builder would re-create the cache (and its compiled regexes) once per entity pair.
/// </summary>
internal static class JoinParameterRenameRegexes
{
    internal static readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Text.RegularExpressions.Regex> Cache = new();
}

/// <summary>
/// Per-parameter-prefix cache for the grouped-join HAVING rename pattern. Separate from
/// <see cref="JoinParameterRenameRegexes"/> because the two sequences use different tokens and one
/// pattern matching both would let a <c>jp</c> rename rewrite a <c>jhp</c> operand.
/// </summary>
internal static class HavingParameterRenameRegexes
{
    internal static readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Text.RegularExpressions.Regex> Cache = new();
}
