using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Dax.Template.Extensions
{
    public static class ReflectionHelper
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

        public static object? GetPropertyValue(this object obj, string propertyName, bool errorIfNotFound = true)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }
            Type objType = obj.GetType();
            PropertyInfo? propInfo = GetPropertyInfo(objType, propertyName);
            if (propInfo == null && errorIfNotFound)
            {
                throw new ArgumentOutOfRangeException(
                            nameof(propertyName),
                            string.Format("Couldn't find property {0} in type {1}", propertyName, objType.FullName));
            }
            return propInfo?.GetValue(obj, null);
        }

        public static void SetPropertyValue(this object obj, string propertyName, object val)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }
            Type objType = obj.GetType();
            PropertyInfo? propInfo = GetPropertyInfo(objType, propertyName);
            if (propInfo == null)
            {
                throw new ArgumentOutOfRangeException(
                            nameof(propertyName),
                            string.Format("Couldn't find property {0} in type {1}", propertyName, objType.FullName));
            }
            propInfo.SetValue(obj, val, null);
        }
    }
}
