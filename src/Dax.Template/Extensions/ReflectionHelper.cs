using System;
using System.Reflection;

namespace Dax.Template.Extensions;

/// <summary>
/// Reads and writes object properties by name via .NET reflection, including <b>non-public</b> members
/// reached through a base-type inheritance walk. This is how <see cref="Engine.GetModelChanges"/> reaches
/// internal state of the Microsoft.AnalysisServices.Tabular (TOM) object model that has no public API
/// (e.g. the transaction log chain <c>TxManager</c> -&gt; <c>CurrentSavepoint</c> -&gt; <c>AllBodies</c>).
/// </summary>
/// <remarks>
/// Because every property is resolved by string name rather than by compile-time reference, this class is
/// fragile across TOM version upgrades: a renamed or removed internal member will not fail to compile, it
/// will only surface at runtime as an <see cref="ArgumentOutOfRangeException"/> (or silently as <see
/// langword="null"/> when the caller passes <c>errorIfNotFound: false</c>). Re-verify every reflected
/// member name in <see cref="Engine.GetModelChanges"/> (via <c>Engine.TomInternalMembers</c>) after any
/// Microsoft.AnalysisServices.Tabular package bump.
/// </remarks>
internal static class ReflectionHelper
{
    private static PropertyInfo? GetPropertyInfo(Type? type, string propertyName)
    {
        PropertyInfo? propInfo;
        do
        {
            propInfo = type?.GetProperty(
                        propertyName,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            type = type?.BaseType;
        }
        while (propInfo == null && type != null);
        return propInfo;
    }

    internal static object? GetPropertyValue(this object obj, string propertyName, bool errorIfNotFound = true)
    {
        ArgumentNullException.ThrowIfNull(obj);
        Type objType = obj.GetType();
        PropertyInfo? propInfo = GetPropertyInfo(objType, propertyName);
        if (propInfo == null && errorIfNotFound)
        {
            throw new ArgumentOutOfRangeException(
                        nameof(propertyName),
                        $"Couldn't find property {propertyName} in type {objType.FullName}");
        }
        return propInfo?.GetValue(obj, null);
    }

    internal static void SetPropertyValue(this object obj, string propertyName, object val)
    {
        ArgumentNullException.ThrowIfNull(obj);
        Type objType = obj.GetType();
        PropertyInfo? propInfo = GetPropertyInfo(objType, propertyName);
        if (propInfo == null)
        {
            throw new ArgumentOutOfRangeException(
                        nameof(propertyName),
                        $"Couldn't find property {propertyName} in type {objType.FullName}");
        }
        propInfo.SetValue(obj, val, null);
    }
}