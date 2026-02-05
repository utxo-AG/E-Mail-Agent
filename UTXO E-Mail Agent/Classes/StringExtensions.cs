namespace UTXO_E_Mail_Agent.Classes;

public static class StringExtensions
{
    public static IEnumerable<T> OrEmptyIfNull<T>(this IEnumerable<T> source)
    {
        return source ?? Enumerable.Empty<T>();
    }

}