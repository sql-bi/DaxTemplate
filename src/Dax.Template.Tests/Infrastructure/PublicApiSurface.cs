namespace Dax.Template.Tests.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text;

    /// <summary>
    /// Reflects over an assembly and produces a deterministic, sorted, plain-text dump of its public
    /// (and protected, since protected members participate in the inheritance contract) surface: types
    /// and their constructors, fields, properties, events, methods, and enum members.
    /// </summary>
    /// <remarks>
    /// Used by <c>Dax.Template.Tests.PublicApiGoldenTests</c> as a P0 change-detector golden file (Phase M
    /// Stage 0): it is NOT a hard API freeze/gate. When the public surface legitimately changes, regenerate
    /// the snapshot (<c>UPDATE_GOLDEN=1</c>) and review the diff before committing it. Output is sorted by
    /// stable keys (type full name; formatted member text) and never depends on reflection enumeration
    /// order, so it stays identical across repeated runs and .NET versions.
    /// </remarks>
    public static class PublicApiSurface
    {
        private const BindingFlags MemberFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        private static readonly Dictionary<Type, string> BuiltInAliases = new()
        {
            [typeof(void)] = "void",
            [typeof(object)] = "object",
            [typeof(string)] = "string",
            [typeof(bool)] = "bool",
            [typeof(byte)] = "byte",
            [typeof(sbyte)] = "sbyte",
            [typeof(short)] = "short",
            [typeof(ushort)] = "ushort",
            [typeof(int)] = "int",
            [typeof(uint)] = "uint",
            [typeof(long)] = "long",
            [typeof(ulong)] = "ulong",
            [typeof(float)] = "float",
            [typeof(double)] = "double",
            [typeof(decimal)] = "decimal",
            [typeof(char)] = "char",
        };

        // Most-visible-first; used to pick the "dominant" accessibility of a property from its accessors.
        private static readonly string[] VisibilityRank =
            ["public", "protected internal", "protected", "internal", "private protected", "private"];

        /// <summary>
        /// Produces the deterministic public-API text dump for <paramref name="assembly"/>. Line endings are
        /// normalized to <c>\n</c>.
        /// </summary>
        public static string Dump(Assembly assembly)
        {
            var sb = new StringBuilder();

            var types = assembly.GetTypes()
                .Where(IsSurfaceVisible)
                .OrderBy(t => t.FullName, StringComparer.Ordinal);

            foreach (var type in types)
            {
                sb.Append(FormatTypeHeader(type)).Append('\n');

                var memberLines = GetSurfaceMembers(type)
                    .Select(FormatMember)
                    .OrderBy(line => line, StringComparer.Ordinal);

                foreach (var line in memberLines)
                {
                    sb.Append("  ").Append(line).Append('\n');
                }
            }

            return sb.ToString().Replace("\r\n", "\n");
        }

        // ----- type-level -----

        private static bool IsSurfaceVisible(Type type)
        {
            if (IsCompilerGenerated(type)) return false;
            return type.IsPublic || type.IsNestedPublic || type.IsNestedFamily || type.IsNestedFamORAssem;
        }

        private static string FormatTypeHeader(Type type)
        {
            var accessibility = TypeAccessibility(type);
            var kind = TypeKind(type);
            var name = FormatTypeName(type, includeNamespace: true);

            if (kind == "delegate")
            {
                var invoke = type.GetMethod("Invoke")!;
                return $"{accessibility} delegate {FormatTypeName(invoke.ReturnType, true)} {name}({FormatParameters(invoke.GetParameters())})";
            }

            var modifiers = new List<string>();
            if (type.IsAbstract && type.IsSealed) modifiers.Add("static");
            else if (kind == "class")
            {
                if (type.IsAbstract) modifiers.Add("abstract");
                if (type.IsSealed) modifiers.Add("sealed");
            }

            var bases = new List<string>();
            if (kind == "enum")
            {
                bases.Add(FormatTypeName(Enum.GetUnderlyingType(type), includeNamespace: false));
            }
            else
            {
                if (type.BaseType is { } baseType && baseType != typeof(object) && baseType != typeof(ValueType))
                {
                    bases.Add(FormatTypeName(baseType, includeNamespace: true));
                }

                var interfaces = type.GetInterfaces()
                    .Select(i => FormatTypeName(i, includeNamespace: true))
                    .OrderBy(s => s, StringComparer.Ordinal);
                bases.AddRange(interfaces);
            }

            var modifierText = modifiers.Count > 0 ? string.Join(" ", modifiers) + " " : "";
            var baseText = bases.Count > 0 ? " : " + string.Join(", ", bases) : "";

            return $"{accessibility} {modifierText}{kind} {name}{baseText}";
        }

        private static string TypeAccessibility(Type type)
        {
            if (type.IsPublic || type.IsNestedPublic) return "public";
            if (type.IsNestedFamORAssem) return "protected internal";
            if (type.IsNestedFamily) return "protected";
            return "internal";
        }

        private static string TypeKind(Type type)
        {
            if (type.IsInterface) return "interface";
            if (type.IsEnum) return "enum";
            if (typeof(Delegate).IsAssignableFrom(type)) return "delegate";
            if (type.IsValueType) return "struct";
            return "class";
        }

        // ----- member-level -----

        private static IEnumerable<MemberInfo> GetSurfaceMembers(Type type)
        {
            // Delegate signatures are captured entirely in the type header (via Invoke); the compiler-emitted
            // .ctor/Invoke/BeginInvoke/EndInvoke boilerplate is identical for every delegate and adds no signal.
            if (typeof(Delegate).IsAssignableFrom(type)) yield break;

            foreach (var ctor in type.GetConstructors(MemberFlags))
            {
                if (IsMemberVisible(ctor.IsPublic, ctor.IsFamily, ctor.IsFamilyOrAssembly))
                    yield return ctor;
            }

            foreach (var field in type.GetFields(MemberFlags))
            {
                if (field.IsSpecialName || IsCompilerGenerated(field)) continue;
                if (IsMemberVisible(field.IsPublic, field.IsFamily, field.IsFamilyOrAssembly))
                    yield return field;
            }

            foreach (var property in type.GetProperties(MemberFlags))
            {
                var accessor = property.GetMethod ?? property.SetMethod;
                if (accessor is null) continue;
                if (IsMemberVisible(accessor.IsPublic, accessor.IsFamily, accessor.IsFamilyOrAssembly))
                    yield return property;
            }

            foreach (var evt in type.GetEvents(MemberFlags))
            {
                var accessor = evt.AddMethod ?? evt.RemoveMethod;
                if (accessor is null) continue;
                if (IsMemberVisible(accessor.IsPublic, accessor.IsFamily, accessor.IsFamilyOrAssembly))
                    yield return evt;
            }

            foreach (var method in type.GetMethods(MemberFlags))
            {
                // Skip property/event accessors (get_X, set_X, add_X, remove_X) but keep operator overloads
                // (op_Addition, etc.), which are also flagged IsSpecialName.
                if (method.IsSpecialName && !method.Name.StartsWith("op_", StringComparison.Ordinal)) continue;
                if (IsCompilerGenerated(method)) continue;
                if (IsMemberVisible(method.IsPublic, method.IsFamily, method.IsFamilyOrAssembly))
                    yield return method;
            }
        }

        private static bool IsMemberVisible(bool isPublic, bool isFamily, bool isFamilyOrAssembly) =>
            isPublic || isFamily || isFamilyOrAssembly;

        private static bool IsCompilerGenerated(MemberInfo member) =>
            member.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false) || member.Name.Contains('<');

        private static string FormatMember(MemberInfo member) => member switch
        {
            ConstructorInfo ctor => FormatConstructor(ctor),
            FieldInfo field => FormatField(field),
            PropertyInfo property => FormatProperty(property),
            EventInfo evt => FormatEvent(evt),
            MethodInfo method => FormatMethod(method),
            _ => throw new NotSupportedException($"Unsupported member type '{member.GetType()}' for '{member.Name}'."),
        };

        private static string FormatConstructor(ConstructorInfo ctor)
        {
            var accessibility = MemberAccessibility(ctor.IsPublic, ctor.IsFamily, ctor.IsFamilyOrAssembly);
            var typeName = ctor.DeclaringType!.Name;
            var tick = typeName.IndexOf('`');
            if (tick >= 0) typeName = typeName[..tick];
            return $"ctor {accessibility} {typeName}({FormatParameters(ctor.GetParameters())})";
        }

        private static string FormatField(FieldInfo field)
        {
            var accessibility = MemberAccessibility(field.IsPublic, field.IsFamily, field.IsFamilyOrAssembly);

            if (field.IsLiteral)
            {
                var rawValue = field.GetRawConstantValue();
                var valueText = field.DeclaringType!.IsEnum
                    ? Convert.ToInt64(rawValue, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)
                    : FormatConstantValue(rawValue);
                return $"field {accessibility} const {FormatTypeName(field.FieldType, true)} {field.Name} = {valueText}";
            }

            var modifiers = new List<string>();
            if (field.IsDefined(typeof(RequiredMemberAttribute), inherit: false)) modifiers.Add("required");
            if (field.IsStatic) modifiers.Add("static");
            if (field.IsInitOnly) modifiers.Add("readonly");
            var modifierText = modifiers.Count > 0 ? string.Join(" ", modifiers) + " " : "";

            return $"field {accessibility} {modifierText}{FormatTypeName(field.FieldType, true)} {field.Name}";
        }

        private static string FormatProperty(PropertyInfo property)
        {
            var getMethod = property.GetMethod;
            var setMethod = property.SetMethod;
            var getVisible = getMethod is not null && IsMemberVisible(getMethod.IsPublic, getMethod.IsFamily, getMethod.IsFamilyOrAssembly);
            var setVisible = setMethod is not null && IsMemberVisible(setMethod.IsPublic, setMethod.IsFamily, setMethod.IsFamilyOrAssembly);

            var dominant = (getVisible, setVisible) switch
            {
                (true, true) => MoreVisible(
                    MemberAccessibility(getMethod!.IsPublic, getMethod.IsFamily, getMethod.IsFamilyOrAssembly),
                    MemberAccessibility(setMethod!.IsPublic, setMethod.IsFamily, setMethod.IsFamilyOrAssembly)),
                (true, false) => MemberAccessibility(getMethod!.IsPublic, getMethod.IsFamily, getMethod.IsFamilyOrAssembly),
                (false, true) => MemberAccessibility(setMethod!.IsPublic, setMethod.IsFamily, setMethod.IsFamilyOrAssembly),
                _ => "private",
            };

            var accessorMethod = (getMethod ?? setMethod)!;
            var requiredText = property.IsDefined(typeof(RequiredMemberAttribute), inherit: false) ? "required " : "";
            var staticText = accessorMethod.IsStatic ? "static " : "";

            var accessors = new List<string>();
            if (getVisible) accessors.Add(FormatAccessor(getMethod!, dominant, "get"));
            if (setVisible) accessors.Add(FormatAccessor(setMethod!, dominant, IsInitOnly(setMethod!) ? "init" : "set"));

            var indexParameters = property.GetIndexParameters();
            var propertyName = indexParameters.Length > 0
                ? $"this[{FormatParameters(indexParameters)}]"
                : property.Name;

            return $"property {dominant} {requiredText}{staticText}{FormatTypeName(property.PropertyType, true)} {propertyName} {{ {string.Join(" ", accessors)} }}";
        }

        private static string FormatAccessor(MethodInfo accessor, string dominantAccessibility, string keyword)
        {
            var accessorAccessibility = MemberAccessibility(accessor.IsPublic, accessor.IsFamily, accessor.IsFamilyOrAssembly);
            return accessorAccessibility == dominantAccessibility ? $"{keyword};" : $"{accessorAccessibility} {keyword};";
        }

        private static bool IsInitOnly(MethodInfo setMethod) =>
            setMethod.ReturnParameter.GetRequiredCustomModifiers().Any(m => m == typeof(IsExternalInit));

        private static string FormatEvent(EventInfo evt)
        {
            var accessor = (evt.AddMethod ?? evt.RemoveMethod)!;
            var accessibility = MemberAccessibility(accessor.IsPublic, accessor.IsFamily, accessor.IsFamilyOrAssembly);
            var staticText = accessor.IsStatic ? "static " : "";
            return $"event {accessibility} {staticText}{FormatTypeName(evt.EventHandlerType!, true)} {evt.Name}";
        }

        private static string FormatMethod(MethodInfo method)
        {
            var accessibility = MemberAccessibility(method.IsPublic, method.IsFamily, method.IsFamilyOrAssembly);

            var modifiers = new List<string>();
            if (method.IsStatic) modifiers.Add("static");
            if (method.IsAbstract) modifiers.Add("abstract");
            else if (method.IsVirtual && method.IsFinal) modifiers.Add("sealed override");
            else if (method.IsVirtual && !method.DeclaringType!.IsInterface) modifiers.Add("virtual");
            var modifierText = modifiers.Count > 0 ? string.Join(" ", modifiers) + " " : "";

            var generic = method.IsGenericMethodDefinition
                ? "<" + string.Join(", ", method.GetGenericArguments().Select(a => a.Name)) + ">"
                : "";

            return $"method {accessibility} {modifierText}{FormatTypeName(method.ReturnType, true)} {method.Name}{generic}({FormatParameters(method.GetParameters())})";
        }

        private static string MemberAccessibility(bool isPublic, bool isFamily, bool isFamilyOrAssembly)
        {
            if (isPublic) return "public";
            if (isFamilyOrAssembly) return "protected internal";
            if (isFamily) return "protected";
            return "internal";
        }

        private static string MoreVisible(string a, string b) =>
            Array.IndexOf(VisibilityRank, a) <= Array.IndexOf(VisibilityRank, b) ? a : b;

        // ----- shared formatting -----

        private static string FormatParameters(ParameterInfo[] parameters) =>
            string.Join(", ", parameters.Select(FormatParameter));

        private static string FormatParameter(ParameterInfo parameter)
        {
            var prefix = parameter.IsOut ? "out " : parameter.ParameterType.IsByRef ? "ref " : "";
            var typeName = FormatTypeName(parameter.ParameterType, includeNamespace: true);
            var defaultText = parameter.HasDefaultValue ? $" = {FormatConstantValue(parameter.DefaultValue)}" : "";
            return $"{prefix}{typeName} {parameter.Name}{defaultText}";
        }

        private static string FormatConstantValue(object? value) => value switch
        {
            null => "null",
            string s => $"\"{s}\"",
            bool b => b ? "true" : "false",
            char c => $"'{c}'",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "null",
        };

        private static string FormatTypeName(Type type, bool includeNamespace)
        {
            if (type.IsByRef) return FormatTypeName(type.GetElementType()!, includeNamespace);

            if (type.IsArray)
            {
                var rank = type.GetArrayRank();
                var suffix = rank == 1 ? "[]" : "[" + new string(',', rank - 1) + "]";
                return FormatTypeName(type.GetElementType()!, includeNamespace) + suffix;
            }

            if (type.IsPointer) return FormatTypeName(type.GetElementType()!, includeNamespace) + "*";
            if (type.IsGenericParameter) return type.Name;

            if (Nullable.GetUnderlyingType(type) is { } underlying)
                return FormatTypeName(underlying, includeNamespace) + "?";

            if (BuiltInAliases.TryGetValue(type, out var alias)) return alias;

            if (type.IsGenericType)
            {
                var name = type.Name;
                var tick = name.IndexOf('`');
                if (tick >= 0) name = name[..tick];

                var typeArguments = type.GetGenericArguments();
                var ownArity = typeArguments.Length;
                if (type.IsNested && type.DeclaringType is { IsGenericType: true } declaringType)
                {
                    ownArity = typeArguments.Length - declaringType.GetGenericArguments().Length;
                }

                var ownArguments = typeArguments.Skip(typeArguments.Length - ownArity).Select(a => FormatTypeName(a, includeNamespace));
                var argumentsText = ownArity > 0 ? "<" + string.Join(", ", ownArguments) + ">" : "";

                return $"{GetPrefix(type, includeNamespace)}{name}{argumentsText}";
            }

            return GetPrefix(type, includeNamespace) + type.Name;
        }

        private static string GetPrefix(Type type, bool includeNamespace)
        {
            if (type.IsNested && type.DeclaringType is not null)
                return FormatTypeName(type.DeclaringType, includeNamespace) + ".";

            if (includeNamespace && !string.IsNullOrEmpty(type.Namespace))
                return type.Namespace + ".";

            return "";
        }
    }
}