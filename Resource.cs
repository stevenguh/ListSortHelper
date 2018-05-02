namespace IListSortHelper
{
  /// <summary>
  /// A class to contain all the resource strings.
  /// </summary>
  /// <remarks>
  /// Use this until there's a clearer way to use resx with dotnet.
  /// </remarks>
  internal static class Resource
  {
    internal const string Arg_BogusIComparer = "Unable to sort because the IComparer.Compare() method returns inconsistent results. Either a value does not compare equal to itself, or one value repeatedly compared to another value yields different results. IComparer: '{0}'.";
    internal const string InvalidOperation_IComparerFailed = "Failed to compare two elements in the array.";
  }
}