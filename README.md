# ListSortHelper

This is a slightly modified version of the [ArraySortHelper.cs](https://github.com/dotnet/coreclr/blob/b19809113b632dadff6de1410c5c125220ff7f26/src/mscorlib/src/System/Collections/Generic/ArraySortHelper.cs) in coreclr to provide the same sorting algorithm used in Array publicly to IList<T>. This is useful for implementing sorting for custom implementation of IList<T>.

Forked ArraySortHelper from coreclr at b19809113b632dadff6de1410c5c125220ff7f26