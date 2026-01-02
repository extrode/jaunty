namespace Jaunty;

public static partial class Jaunty
{
    internal static object[] CombineParams(object param1, object param2, object[] rest)
    {
        var result = new object[2 + rest.Length];
        result[0] = param1;
        result[1] = param2;

        for (int i = 0; i < rest.Length; i++)
            result[i + 2] = rest[i];

        return result;
    }
}
