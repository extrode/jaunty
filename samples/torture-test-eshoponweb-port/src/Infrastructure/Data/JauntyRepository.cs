using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Jaunty;
using Jaunty.Core;
using Jaunty.Fluent;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.BasketAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Data.Persistence;

namespace Microsoft.eShopWeb.Infrastructure.Data;

/// <summary>
/// Jaunty-backed replacement for the former EF Core <c>EfRepository&lt;T&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// Reads: the Ardalis <see cref="ISpecification{T}"/> is translated into a real Jaunty fluent query
/// over the flat "Row" POCO. The spec's <c>WhereExpressions</c> and <c>OrderExpressions</c> (authored
/// against the domain aggregate) are retargeted to the Row type by <see cref="SpecRetargeter"/> and
/// pushed to SQL via <c>connection.From&lt;TRow&gt;().Where(...).OrderBy(...).Skip(...).Take(...)</c>,
/// so filtering / ordering / paging happen in the database, not in memory. <c>Include</c>/
/// <c>ThenInclude</c> child collections are hydrated with one additional targeted
/// <c>WhereIn(fk, rootIds)</c> query per include path (never a full child-table scan); the flattened
/// owned value objects (<c>ItemOrdered_*</c>, <c>ShipToAddress_*</c>) are already columns on the
/// child/parent Row, so the second <c>ThenInclude</c> level needs no extra round trip.
/// </para>
/// <para>
/// Writes: aggregates are translated to flat Rows and persisted with Jaunty's immediate
/// <c>Insert</c>/<c>Update</c>/<c>Delete</c>. Parent + child rows are written inside a single
/// transaction. There is no change tracker, so <see cref="SaveChangesAsync"/> is a no-op.
/// </para>
/// <para>
/// <c>SearchCriterias</c> (LIKE) is not implemented; none of the eShopOnWeb specs use it. The
/// projection (<c>TResult</c>) query overloads are also not implemented (no concrete spec uses
/// them) and throw <see cref="NotSupportedException"/>.
/// </para>
/// </remarks>
public class JauntyRepository<T> : IReadRepository<T>, IRepository<T>
    where T : class, IAggregateRoot
{
    private readonly IDbConnection _connection;

    public JauntyRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    // ---- materialization (spec -> SQL) --------------------------------------------

    // Loads the full aggregate set (no spec): used by the parameterless List/Count/Any overloads.
    private List<T> LoadAll() => QueryAggregates(specification: null, rootId: null);

    // Loads a single aggregate (with its child collections hydrated) by primary key.
    private T? LoadById(object id)
    {
        var key = Convert.ToInt32(id);
        return QueryAggregates(specification: null, rootId: key).FirstOrDefault();
    }

    // Runs one Row query with the spec's Where/OrderBy/Skip/Take pushed to SQL, maps the resulting
    // root rows to domain aggregates, and hydrates any Include child collections with a single
    // targeted WhereIn query keyed on the root ids. When <paramref name="rootId"/> is set the root
    // set is additionally restricted to that primary key (used by GetByIdAsync).
    private List<T> QueryAggregates(ISpecification<T>? specification, int? rootId)
    {
        var type = typeof(T);

        if (type == typeof(CatalogItem))
        {
            var rows = QueryRootRows<CatalogItemRow>(specification, rootId, r => r.Id);
            return Cast(rows.Select(r => r.ToAggregate()));
        }

        if (type == typeof(CatalogBrand))
        {
            var rows = QueryRootRows<CatalogBrandRow>(specification, rootId, r => r.Id);
            return Cast(rows.Select(r => r.ToAggregate()));
        }

        if (type == typeof(CatalogType))
        {
            var rows = QueryRootRows<CatalogTypeRow>(specification, rootId, r => r.Id);
            return Cast(rows.Select(r => r.ToAggregate()));
        }

        if (type == typeof(Basket))
        {
            var rows = QueryRootRows<BasketRow>(specification, rootId, r => r.Id);
            var ids = rows.Select(r => r.Id).ToList();
            var itemRows = LoadChildren<BasketItemRow>(r => r.BasketId, ids);
            return Cast(rows.Select(r => r.ToAggregate(itemRows)));
        }

        if (type == typeof(Order))
        {
            var rows = QueryRootRows<OrderRow>(specification, rootId, r => r.Id);
            var ids = rows.Select(r => r.Id).ToList();
            var itemRows = LoadChildren<OrderItemRow>(r => r.OrderId, ids);
            return Cast(rows.Select(r => r.ToAggregate(itemRows)));
        }

        throw new NotSupportedException(
            $"JauntyRepository does not support aggregate type '{type.FullName}'.");
    }

    private static List<T> Cast(IEnumerable<object> aggregates) => aggregates.Cast<T>().ToList();

    // Builds and executes the root Row query, translating the spec into real SQL clauses.
    private List<TRow> QueryRootRows<TRow>(
        ISpecification<T>? specification, int? rootId, Expression<Func<TRow, int>> idSelector)
        where TRow : new()
    {
        // The QueryBuilder returned by every clause method is the same underlying instance, so the
        // clause-specific interfaces can be reached by re-casting the shared terminal.
        IQueryTerminal<TRow> query = _connection.From<TRow>();
        var whereApplied = false;

        if (rootId.HasValue)
        {
            var predicate = BuildIdEquals(idSelector, rootId.Value);
            query = ((IFromClause<TRow>)query).Where(predicate);
            whereApplied = true;
        }

        if (specification is not null)
        {
            foreach (var where in specification.WhereExpressions)
            {
                var predicate = SpecRetargeter.RetargetPredicate<T, TRow>(where.Filter);
                query = whereApplied
                    ? ((IWhereClause<TRow>)query).And(predicate)
                    : ((IFromClause<TRow>)query).Where(predicate);
                whereApplied = true;
            }

            var hasExplicitOrder = specification.OrderExpressions.Any();

            foreach (var order in specification.OrderExpressions)
            {
                var keySelector = SpecRetargeter.RetargetKey<T, TRow>(order.KeySelector);
                query = ApplyOrder(query, keySelector, order.OrderType);
            }

            if (!hasExplicitOrder && (specification.Skip.HasValue || specification.Take.HasValue))
            {
                query = ApplyOrder(query, ToObjectSelector(idSelector), OrderTypeEnum.OrderBy);
            }

            if (specification.Skip.HasValue)
            {
                query = ApplySkip(query, specification.Skip.Value);
            }

            if (specification.Take.HasValue)
            {
                query = ApplyTake(query, specification.Take.Value);
            }
        }

        return query.Select();
    }

    // Builds row => row.<Id> == value from an int id selector, for the GetByIdAsync fast path.
    private static Expression<Func<TRow, bool>> BuildIdEquals<TRow>(
        Expression<Func<TRow, int>> idSelector, int value)
    {
        var body = Expression.Equal(idSelector.Body, Expression.Constant(value));
        return Expression.Lambda<Func<TRow, bool>>(body, idSelector.Parameters[0]);
    }

    // OrderBy/OrderByDescending/ThenBy/ThenByDescending all append to the same ORDER BY list on the
    // shared builder; IFromClause exposes the initial ordering, IOrderByClause the ThenBy chain.
    private static IQueryTerminal<TRow> ApplyOrder<TRow>(
        IQueryTerminal<TRow> query, Expression<Func<TRow, object?>> keySelector, OrderTypeEnum orderType)
        where TRow : new()
        => orderType switch
        {
            OrderTypeEnum.OrderBy => ((IFromClause<TRow>)query).OrderBy(keySelector),
            OrderTypeEnum.OrderByDescending => ((IFromClause<TRow>)query).OrderByDescending(keySelector),
            OrderTypeEnum.ThenBy => ((IOrderByClause<TRow>)query).ThenBy(keySelector),
            OrderTypeEnum.ThenByDescending => ((IOrderByClause<TRow>)query).ThenByDescending(keySelector),
            _ => query,
        };

    // Skip/Take set the same OFFSET/LIMIT state on the shared builder regardless of the fluent
    // interface used; IFromClause exposes both and is implemented by the builder in every state.
    private static IQueryTerminal<TRow> ApplySkip<TRow>(IQueryTerminal<TRow> query, int count)
        where TRow : new()
        => ((IFromClause<TRow>)query).Skip(count);

    private static IQueryTerminal<TRow> ApplyTake<TRow>(IQueryTerminal<TRow> query, int count)
        where TRow : new()
        => ((IFromClause<TRow>)query).Take(count);

    private static Expression<Func<TRow, object?>> ToObjectSelector<TRow>(Expression<Func<TRow, int>> selector)
    {
        var body = Expression.Convert(selector.Body, typeof(object));
        return Expression.Lambda<Func<TRow, object?>>(body, selector.Parameters[0]);
    }

    // Fetches child rows for the given parent ids with a single WHERE fk IN (...) query. Returns an
    // empty list when there are no parents so no query is issued.
    private List<TChildRow> LoadChildren<TChildRow>(
        Expression<Func<TChildRow, int>> foreignKey, IReadOnlyCollection<int> parentIds)
        where TChildRow : new()
    {
        if (parentIds.Count == 0)
        {
            return new List<TChildRow>();
        }

        return _connection.From<TChildRow>()
            .WhereIn(foreignKey, parentIds)
            .Select();
    }

    private List<T> Evaluate(ISpecification<T> specification)
        => QueryAggregates(specification, rootId: null);

    // ---- IReadRepositoryBase<T> ---------------------------------------------------

    public Task<T?> GetByIdAsync<TId>(TId id, CancellationToken cancellationToken = default)
        where TId : notnull
        => Task.FromResult(LoadById(id));

    public Task<T?> FirstOrDefaultAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
        => Task.FromResult<T?>(Evaluate(specification).FirstOrDefault());

    public Task<TResult?> FirstOrDefaultAsync<TResult>(ISpecification<T, TResult> specification, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Projection specifications (ISpecification<T, TResult>) are not supported by JauntyRepository.");

    public Task<T?> SingleOrDefaultAsync(ISingleResultSpecification<T> specification, CancellationToken cancellationToken = default)
        => Task.FromResult<T?>(Evaluate(specification).SingleOrDefault());

    public Task<TResult?> SingleOrDefaultAsync<TResult>(ISingleResultSpecification<T, TResult> specification, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Projection specifications (ISpecification<T, TResult>) are not supported by JauntyRepository.");

    public Task<T?> GetBySpecAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
        => Task.FromResult<T?>(Evaluate(specification).FirstOrDefault());

    public Task<TResult?> GetBySpecAsync<TResult>(ISpecification<T, TResult> specification, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Projection specifications (ISpecification<T, TResult>) are not supported by JauntyRepository.");

    public Task<List<T>> ListAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(LoadAll());

    public Task<List<T>> ListAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
        => Task.FromResult(Evaluate(specification));

    public Task<List<TResult>> ListAsync<TResult>(ISpecification<T, TResult> specification, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Projection specifications (ISpecification<T, TResult>) are not supported by JauntyRepository.");

    public Task<int> CountAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
        => Task.FromResult(Evaluate(specification).Count);

    public Task<int> CountAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(LoadAll().Count);

    public Task<bool> AnyAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
        => Task.FromResult(Evaluate(specification).Any());

    public Task<bool> AnyAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(LoadAll().Any());

    public async IAsyncEnumerable<T> AsAsyncEnumerable(ISpecification<T> specification)
    {
        foreach (var item in Evaluate(specification))
        {
            yield return item;
        }

        await Task.CompletedTask;
    }

    // ---- IRepositoryBase<T> -------------------------------------------------------

    public Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        Persist(entity, PersistMode.Insert);
        return Task.FromResult(entity);
    }

    public Task<IEnumerable<T>> AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        var materialized = entities.ToList();
        foreach (var entity in materialized)
        {
            Persist(entity, PersistMode.Insert);
        }
        return Task.FromResult<IEnumerable<T>>(materialized);
    }

    public Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        Persist(entity, PersistMode.Update);
        return Task.CompletedTask;
    }

    public Task UpdateRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        foreach (var entity in entities)
        {
            Persist(entity, PersistMode.Update);
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        Persist(entity, PersistMode.Delete);
        return Task.CompletedTask;
    }

    public Task DeleteRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        foreach (var entity in entities)
        {
            Persist(entity, PersistMode.Delete);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// No-op. Jaunty's Insert/Update/Delete are immediate (executed when
    /// Add/Update/Delete are called), not deferred like EF Core's change tracker,
    /// so there is nothing to flush here. Always returns 0.
    /// </summary>
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    // ---- write translation --------------------------------------------------------

    private enum PersistMode { Insert, Update, Delete }

    private void Persist(T entity, PersistMode mode)
    {
        switch (entity)
        {
            case CatalogItem item:
                PersistFlat(item, item.ToRow(), mode);
                break;
            case CatalogBrand brand:
                PersistFlat(brand, brand.ToRow(), mode);
                break;
            case CatalogType type:
                PersistFlat(type, type.ToRow(), mode);
                break;
            case Basket basket:
                PersistBasket(basket, mode);
                break;
            case Order order:
                PersistOrder(order, mode);
                break;
            default:
                throw new NotSupportedException(
                    $"JauntyRepository cannot persist aggregate type '{typeof(T).FullName}'.");
        }
    }

    private void PersistFlat<TRow>(BaseEntity entity, TRow row, PersistMode mode) where TRow : class, new()
    {
        switch (mode)
        {
            case PersistMode.Insert:
                var newId = (int)_connection.Insert(row);
                AggregateMappers.SetBaseId(entity, newId);
                break;
            case PersistMode.Update:
                _connection.Update(row);
                break;
            case PersistMode.Delete:
                _connection.Delete(row);
                break;
        }
    }

    private void EnsureOpen()
    {
        if (_connection.State != ConnectionState.Open)
        {
            _connection.Open();
        }
    }

    private void PersistBasket(Basket basket, PersistMode mode)
    {
        EnsureOpen();
        using var tx = _connection.BeginTransaction();

        var basketRow = basket.ToRow();

        switch (mode)
        {
            case PersistMode.Insert:
            {
                var newId = (int)_connection.Insert(basketRow, CommandOptions.WithTransaction(tx));
                AggregateMappers.SetBaseId(basket, newId);
                foreach (var item in basket.Items)
                {
                    _connection.Insert(item.ToRow(newId), CommandOptions.WithTransaction(tx));
                }
                break;
            }
            case PersistMode.Update:
            {
                _connection.Update(basketRow, CommandOptions.WithTransaction(tx));
                // Replace child rows: delete existing, re-insert current set.
                DeleteChildren("BasketItems", "BasketId", basket.Id, tx);
                foreach (var item in basket.Items)
                {
                    var childRow = item.ToRow(basket.Id);
                    childRow.Id = 0; // force re-insert as new identity
                    _connection.Insert(childRow, CommandOptions.WithTransaction(tx));
                }
                break;
            }
            case PersistMode.Delete:
            {
                DeleteChildren("BasketItems", "BasketId", basket.Id, tx);
                _connection.Delete(basketRow, CommandOptions.WithTransaction(tx));
                break;
            }
        }

        tx.Commit();
    }

    private void PersistOrder(Order order, PersistMode mode)
    {
        EnsureOpen();
        using var tx = _connection.BeginTransaction();

        var orderRow = order.ToRow();

        switch (mode)
        {
            case PersistMode.Insert:
            {
                var newId = (int)_connection.Insert(orderRow, CommandOptions.WithTransaction(tx));
                AggregateMappers.SetBaseId(order, newId);
                foreach (var item in order.OrderItems)
                {
                    _connection.Insert(item.ToRow(newId), CommandOptions.WithTransaction(tx));
                }
                break;
            }
            case PersistMode.Update:
            {
                _connection.Update(orderRow, CommandOptions.WithTransaction(tx));
                DeleteChildren("OrderItems", "OrderId", order.Id, tx);
                foreach (var item in order.OrderItems)
                {
                    var childRow = item.ToRow(order.Id);
                    childRow.Id = 0;
                    _connection.Insert(childRow, CommandOptions.WithTransaction(tx));
                }
                break;
            }
            case PersistMode.Delete:
            {
                DeleteChildren("OrderItems", "OrderId", order.Id, tx);
                _connection.Delete(orderRow, CommandOptions.WithTransaction(tx));
                break;
            }
        }

        tx.Commit();
    }

    private void DeleteChildren(string table, string fkColumn, int parentId, IDbTransaction tx)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = $"DELETE FROM {table} WHERE {fkColumn} = @parentId";
        var p = command.CreateParameter();
        p.ParameterName = "@parentId";
        p.Value = parentId;
        command.Parameters.Add(p);
        command.ExecuteNonQuery();
    }
}
