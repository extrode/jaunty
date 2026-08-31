using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Microsoft.eShopWeb.Infrastructure.Data.Persistence;

// Rewrites Ardalis.Specification predicate / key-selector expressions that were authored against
// a domain aggregate (e.g. Basket, CatalogItem, Order) so they instead reference the flat Jaunty
// "Row" POCO (BasketRow, CatalogItemRow, OrderRow). The concrete eShopOnWeb specs only reference
// top-level scalar properties in their Where/OrderBy clauses, and every referenced property name
// matches 1:1 between the domain type and its Row type (Id, BuyerId, CatalogBrandId, CatalogTypeId,
// Name, ...), so a straight parameter swap + member remap by name is sufficient.
//
// Before remapping, closure-only sub-expressions (captured locals such as basketId / brandId,
// including patterns like "!brandId.HasValue") are partially evaluated to constants and the
// resulting boolean tree is simplified (true || x -> true, false || x -> x, etc.). This is required
// because the specs rely on CLR short-circuit semantics (e.g.
// "!brandId.HasValue || i.CatalogBrandId == brandId") that do NOT hold in SQL's three-valued logic:
// without this pass, an unset filter would translate to "... OR CatalogBrandId IS NULL" and wrongly
// exclude every row. This mirrors what EF Core's query translator does with the same specs.
internal static class SpecRetargeter
{
    // Rewrites Expression<Func<TDomain,bool>> -> Expression<Func<TRow,bool>>.
    public static Expression<Func<TRow, bool>> RetargetPredicate<TDomain, TRow>(
        Expression<Func<TDomain, bool>> predicate)
    {
        var domainParam = predicate.Parameters[0];
        var simplifiedBody = PartialEvaluator.SimplifyForParameter(predicate.Body, domainParam);

        var rowParam = Expression.Parameter(typeof(TRow), domainParam.Name);
        var visitor = new ParameterRemapVisitor(domainParam, rowParam, typeof(TRow));
        var body = visitor.Visit(simplifiedBody);
        return Expression.Lambda<Func<TRow, bool>>(body, rowParam);
    }

    // Rewrites a domain key selector (Func<TDomain,TKey>) -> Expression<Func<TRow,object?>>, boxing
    // value-type keys so the result matches Jaunty's OrderBy(Expression<Func<T,object?>>) signature.
    public static Expression<Func<TRow, object?>> RetargetKey<TDomain, TRow>(
        LambdaExpression keySelector)
    {
        var domainParam = keySelector.Parameters[0];
        var rowParam = Expression.Parameter(typeof(TRow), domainParam.Name);
        var visitor = new ParameterRemapVisitor(domainParam, rowParam, typeof(TRow));
        var body = visitor.Visit(keySelector.Body);

        if (body.Type != typeof(object))
        {
            body = Expression.Convert(body, typeof(object));
        }

        return Expression.Lambda<Func<TRow, object?>>(body, rowParam);
    }

    // Evaluates any sub-tree that does not depend on the lambda parameter into a ConstantExpression,
    // then folds constant booleans through && / || / ! so short-circuiting predicates collapse.
    private static class PartialEvaluator
    {
        public static Expression SimplifyForParameter(Expression body, ParameterExpression parameter)
        {
            var independent = new IndependenceMarker(parameter).Collect(body);
            var evaluated = new SubtreeEvaluator(independent).Visit(body);
            return new BooleanFolder().Visit(evaluated);
        }

        // Marks the maximal sub-trees that contain no reference to the target parameter.
        private sealed class IndependenceMarker : ExpressionVisitor
        {
            private readonly ParameterExpression _parameter;
            private readonly HashSet<Expression> _independent = new();
            private bool _currentDependsOnParameter;

            public IndependenceMarker(ParameterExpression parameter) => _parameter = parameter;

            public HashSet<Expression> Collect(Expression body)
            {
                Visit(body);
                return _independent;
            }

            public override Expression? Visit(Expression? node)
            {
                if (node is null)
                {
                    return null;
                }

                var parentDepends = _currentDependsOnParameter;
                _currentDependsOnParameter = false;

                base.Visit(node);

                var thisDepends = _currentDependsOnParameter;
                if (node == _parameter)
                {
                    thisDepends = true;
                }

                if (!thisDepends && node.NodeType != ExpressionType.Constant)
                {
                    _independent.Add(node);
                }

                _currentDependsOnParameter = parentDepends || thisDepends;
                return node;
            }
        }

        // Replaces marked parameter-independent sub-trees with their evaluated constant value.
        private sealed class SubtreeEvaluator : ExpressionVisitor
        {
            private readonly HashSet<Expression> _independent;

            public SubtreeEvaluator(HashSet<Expression> independent) => _independent = independent;

            public override Expression? Visit(Expression? node)
            {
                if (node is null)
                {
                    return null;
                }

                if (_independent.Contains(node))
                {
                    var value = Expression.Lambda(node).Compile().DynamicInvoke();
                    return Expression.Constant(value, node.Type);
                }

                return base.Visit(node);
            }
        }

        // Folds constant booleans through logical operators so short-circuiting collapses cleanly.
        private sealed class BooleanFolder : ExpressionVisitor
        {
            protected override Expression VisitBinary(BinaryExpression node)
            {
                var left = Visit(node.Left);
                var right = Visit(node.Right);

                if (node.NodeType is ExpressionType.OrElse or ExpressionType.Or)
                {
                    if (IsBool(left, out var lb)) return lb ? Constant(true) : right;
                    if (IsBool(right, out var rb)) return rb ? Constant(true) : left;
                }
                else if (node.NodeType is ExpressionType.AndAlso or ExpressionType.And)
                {
                    if (IsBool(left, out var lb)) return lb ? right : Constant(false);
                    if (IsBool(right, out var rb)) return rb ? left : Constant(false);
                }

                return node.Update(left, node.Conversion, right);
            }

            protected override Expression VisitUnary(UnaryExpression node)
            {
                var operand = Visit(node.Operand);
                if (node.NodeType == ExpressionType.Not && IsBool(operand, out var b))
                {
                    return Constant(!b);
                }

                return node.Update(operand);
            }

            private static bool IsBool(Expression expression, out bool value)
            {
                if (expression is ConstantExpression { Value: bool b })
                {
                    value = b;
                    return true;
                }

                value = false;
                return false;
            }

            private static ConstantExpression Constant(bool value)
                => Expression.Constant(value, typeof(bool));
        }
    }

    private sealed class ParameterRemapVisitor : ExpressionVisitor
    {
        private readonly ParameterExpression _domainParam;
        private readonly ParameterExpression _rowParam;
        private readonly Type _rowType;

        public ParameterRemapVisitor(
            ParameterExpression domainParam, ParameterExpression rowParam, Type rowType)
        {
            _domainParam = domainParam;
            _rowParam = rowParam;
            _rowType = rowType;
        }

        protected override Expression VisitParameter(ParameterExpression node)
            => node == _domainParam ? _rowParam : base.VisitParameter(node);

        protected override Expression VisitMember(MemberExpression node)
        {
            // Only remap member accesses whose root is the domain parameter (e.g. b.BuyerId).
            // Closure member chains have already been folded to constants by the partial evaluator.
            if (node.Expression == _domainParam)
            {
                var propertyName = node.Member.Name;
                var rowProperty = _rowType.GetProperty(
                    propertyName, BindingFlags.Instance | BindingFlags.Public);

                if (rowProperty is null)
                {
                    throw new InvalidOperationException(
                        $"Row type '{_rowType.Name}' has no public property '{propertyName}' " +
                        $"matching domain member referenced by a specification. SQL translation " +
                        $"cannot proceed without a 1:1 property mapping.");
                }

                return Expression.Property(_rowParam, rowProperty);
            }

            return base.VisitMember(node);
        }
    }
}
