using System.Data;

namespace Jaunty.Interfaces;

public interface IRowMapper<T>
{
    T Map(IDataReader reader);
}