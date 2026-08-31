using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.BasketAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Persistence;

// Hand-written translators between the flat Jaunty "Row" POCOs and the eShopOnWeb DDD aggregates.
// The aggregates expose private setters, private/absent parameterless constructors and private
// backing-field collections, so reflection is used here to hydrate them. This is app-layer port
// code (not Jaunty core), so reflection is acceptable.
internal static class AggregateMappers
{
    private const BindingFlags InstanceNonPublic =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    // ---- shared reflection helpers -------------------------------------------------

    internal static void SetBaseId(BaseEntity entity, int id)
    {
        // BaseEntity.Id has a protected setter.
        typeof(BaseEntity)
            .GetProperty(nameof(BaseEntity.Id))!
            .SetValue(entity, id);
    }

    private static T CreatePrivate<T>()
    {
        // Invokes a private/absent parameterless constructor.
        return (T)Activator.CreateInstance(typeof(T), nonPublic: true)!;
    }

    private static void SetPrivateProperty(object target, string propertyName, object? value)
    {
        var prop = target.GetType().GetProperty(propertyName, InstanceNonPublic)!;
        prop.SetValue(target, value);
    }

    private static void SetBackingField(object target, string fieldName, object? value)
    {
        var field = target.GetType().GetField(fieldName, InstanceNonPublic)!;
        field.SetValue(target, value);
    }

    // ---- CatalogItem ---------------------------------------------------------------

    public static CatalogItem ToAggregate(this CatalogItemRow row)
    {
        var item = new CatalogItem(
            row.CatalogTypeId,
            row.CatalogBrandId,
            row.Description,
            row.Name,
            row.Price,
            row.PictureUri ?? string.Empty);
        SetBaseId(item, row.Id);
        return item;
    }

    public static CatalogItemRow ToRow(this CatalogItem item) => new()
    {
        Id = item.Id,
        Name = item.Name,
        Description = item.Description,
        Price = item.Price,
        PictureUri = item.PictureUri,
        CatalogTypeId = item.CatalogTypeId,
        CatalogBrandId = item.CatalogBrandId,
    };

    // ---- CatalogBrand --------------------------------------------------------------

    public static CatalogBrand ToAggregate(this CatalogBrandRow row)
    {
        var brand = new CatalogBrand(row.Brand);
        SetBaseId(brand, row.Id);
        return brand;
    }

    public static CatalogBrandRow ToRow(this CatalogBrand brand) => new()
    {
        Id = brand.Id,
        Brand = brand.Brand,
    };

    // ---- CatalogType ---------------------------------------------------------------

    public static CatalogType ToAggregate(this CatalogTypeRow row)
    {
        var type = new CatalogType(row.Type);
        SetBaseId(type, row.Id);
        return type;
    }

    public static CatalogTypeRow ToRow(this CatalogType type) => new()
    {
        Id = type.Id,
        Type = type.Type,
    };

    // ---- Basket + BasketItem -------------------------------------------------------

    public static Basket ToAggregate(this BasketRow row, IEnumerable<BasketItemRow> itemRows)
    {
        var basket = new Basket(row.BuyerId);
        SetBaseId(basket, row.Id);

        var items = itemRows
            .Where(i => i.BasketId == row.Id)
            .Select(ToBasketItem)
            .ToList();

        SetBackingField(basket, "_items", items);
        return basket;
    }

    private static BasketItem ToBasketItem(BasketItemRow row)
    {
        var item = new BasketItem(row.CatalogItemId, row.Quantity, row.UnitPrice);
        SetBaseId(item, row.Id);
        SetPrivateProperty(item, nameof(BasketItem.BasketId), row.BasketId);
        return item;
    }

    public static BasketRow ToRow(this Basket basket) => new()
    {
        Id = basket.Id,
        BuyerId = basket.BuyerId,
    };

    public static BasketItemRow ToRow(this BasketItem item, int basketId) => new()
    {
        Id = item.Id,
        UnitPrice = item.UnitPrice,
        Quantity = item.Quantity,
        CatalogItemId = item.CatalogItemId,
        BasketId = basketId,
    };

    // ---- Order + OrderItem ---------------------------------------------------------

    public static Order ToAggregate(this OrderRow row, IEnumerable<OrderItemRow> itemRows)
    {
        var address = ToAddress(row);

        var items = itemRows
            .Where(i => i.OrderId == row.Id)
            .Select(ToOrderItem)
            .ToList();

        var order = new Order(row.BuyerId, address, items);
        SetBaseId(order, row.Id);

        if (!string.IsNullOrEmpty(row.OrderDate) &&
            DateTimeOffset.TryParse(
                row.OrderDate,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var orderDate))
        {
            SetPrivateProperty(order, nameof(Order.OrderDate), orderDate);
        }

        return order;
    }

    private static Address ToAddress(OrderRow row)
    {
        return new Address(
            row.ShipToAddress_Street,
            row.ShipToAddress_City,
            row.ShipToAddress_State ?? string.Empty,
            row.ShipToAddress_Country,
            row.ShipToAddress_ZipCode);
    }

    private static OrderItem ToOrderItem(OrderItemRow row)
    {
        var itemOrdered = new CatalogItemOrdered(
            row.ItemOrdered_CatalogItemId,
            row.ItemOrdered_ProductName,
            row.ItemOrdered_PictureUri);

        var item = new OrderItem(itemOrdered, row.UnitPrice, row.Units);
        SetBaseId(item, row.Id);
        return item;
    }

    public static OrderRow ToRow(this Order order)
    {
        var address = order.ShipToAddress;
        return new OrderRow
        {
            Id = order.Id,
            BuyerId = order.BuyerId,
            OrderDate = order.OrderDate.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            ShipToAddress_Street = address.Street,
            ShipToAddress_City = address.City,
            ShipToAddress_State = address.State,
            ShipToAddress_Country = address.Country,
            ShipToAddress_ZipCode = address.ZipCode,
        };
    }

    public static OrderItemRow ToRow(this OrderItem item, int orderId) => new()
    {
        Id = item.Id,
        OrderId = orderId,
        UnitPrice = item.UnitPrice,
        Units = item.Units,
        ItemOrdered_CatalogItemId = item.ItemOrdered.CatalogItemId,
        ItemOrdered_ProductName = item.ItemOrdered.ProductName,
        ItemOrdered_PictureUri = item.ItemOrdered.PictureUri,
    };
}
