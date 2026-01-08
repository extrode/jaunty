using System.Data;

namespace Jaunty.PublicApi.Interfaces;

public interface IMapped<T> where T : IMapped<T>, new()
{
#if NET8_0_OR_GREATER
    static abstract T ReadEntity(IDataReader reader);
#else
    T ReadEntity(IDataReader reader);
#endif
}
