namespace Jaunty.PublicApi.Interfaces
{
    public interface IEntity
    {
        long Id { get; set; }
    }

    public interface IEntity<T>
    {
        T Id { get; set; }
    }
}
