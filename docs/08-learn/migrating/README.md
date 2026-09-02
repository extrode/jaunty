# Migrating to Jaunty

Guides for moving an existing codebase onto Jaunty. Written for people who already have a working
data-access layer and are deciding whether the port is worth it.

| Guide | Read it if |
|---|---|
| [Strict mapping: the one thing that will surprise you](strict-mapping.md) | **Read this first, whatever you are coming from.** It is the single largest source of first-hour friction, and it is a two-minute fix once you know the rule |
| [From Dapper](from-dapper.md) | You use Dapper, Dapper.Contrib, or Dapper Plus |
| [From EF Core](from-ef-core.md) | You use EF Core and are considering moving some or all of it |

## Before you start

**Do not port everything.** Jaunty coexists with whatever you have: it is a set of extension methods
on `IDbConnection`, with no context, no registration and no ownership of the connection. Dapper,
EF Core and Jaunty can all run against the same connection in the same method. Port one query, run
your tests, and decide from there.

**Budget for the mapping change, not the API change.** The method names are close enough to whatever
you are using that the mechanical port is quick. What takes the time is that Jaunty's default mapping
is strict and your current ORM's is not - which is the point of the product, and the reason for the
first guide in the table.

## When not to migrate

Stated plainly in [the README](../../../README.md#jaunty-and-dapper). The short
version: if you need change tracking, a unit of work, lazy loading or migrations, that is EF Core's
job and Jaunty does not do it. If your team knows Dapper and your data layer is not causing you
problems, "it is stricter" is not on its own worth a port.
