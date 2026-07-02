namespace Dax.Template.Tests.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text;

    /// <summary>
    /// Builds a deterministic, human-readable textual dump of an assembly's public API surface (public and
    /// protected/protected-internal types and members) via reflection. This is a soft change-detector for
    /// Phase M Stage 0 (P0-a): it surfaces intended-vs-accidental public-surface changes for review in each
    /// PR via <see cref="GoldenFile"/> snapshotting — it is NOT a hard freeze/gate (the public API is open
    /// to improvement; see .claude/SESSION_HANDOFF.md "Phase M — locked decisions" #4).
    /// </summary>
    /// <remarks>
    /// NOTE (scope limit): nullable reference type annotations (e.g. <c>string</c> vs <c>string?</c> on a
    /// reference-typed member) are erased from the reflected member signature by the compiler into
    /// <c>[Nullable]</c>/<c>[NullableContext]</c> attributes, which this dump deliberately excludes as
    /// compiler noise (see <see cref="NoiseAttributeNames"/>). Consequently, a change that flips a
    /// reference type's nullability annotation without touching its underlying reflected shape will NOT be
    /// caught here — this dump is a reflection-shape change-detector, not a full API-compat / NRT-compat
    /// tool.
    /// </remarks>
    public static class PublicApiSnapshot
    {
        /// <summary>
        /// Builds the dump: one line per externally-visible type declaration (class/struct/interface/enum,
        /// its modifiers, base type and implemented interfaces), followed by one indented line per
        /// externally-visible member it declares (constructors, methods, properties, fields, events).
        /// Everything is sorted with <see cref="StringComparer.Ordinal"/> so the output is byte-stable
        /// across runs and machines.
        /// </summary>
        public static string Build(Assembly assembly)
        {
            var sb = new StringBuilder();

            var types = GetLoadableTypes(assembly)
                .Where(IsPublicSurfaceType)
                .OrderBy(FormatTypeName, StringComparer.Ordinal)
                .ToArray();

            foreach (var type in types)
            {
                sb.Append(BuildTypeDeclarationLine(type)).Append('\n');

                foreach (var line in BuildMemberLines(type).OrderBy(l => l, StringComparer.Ordinal))
                {
                    sb.Append("  ").Append(line).Append('\n');
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// <see cref="Assembly.GetTypes"/> throws <see cref="ReflectionTypeLoadException"/> for the WHOLE
        /// assembly if even one type fails to load (e.g. an optional dependency assembly is missing at
        /// reflection time). Falling back to <see cref="ReflectionTypeLoadException.Types"/> (dropping the
        /// null entries for the types that didn't load) yields an actionable partial dump instead of an
        /// opaque throw that hides every other type in the assembly.
        /// </summary>
        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t is not null).Select(t => t!);
            }
        }

        // ----- type-level -----

        private static bool IsPublicSurfaceType(Type type)
        {
            if (IsCompilerGenerated(type)) return false;
            if (type.Name.Contains('<')) return false; // closures, anonymous types, state machines

            if (type.IsNested)
            {
                if (type.DeclaringType is null || !IsPublicSurfaceType(type.DeclaringType)) return false;
                return type.IsNestedPublic || type.IsNestedFamily || type.IsNestedFamORAssem;
            }

            return type.IsPublic;
        }

        private static string BuildTypeDeclarationLine(Type type)
        {
            var parts = new List<string>
            {
                GetTypeAccessibility(type),
            };

            var modifiers = GetTypeModifiers(type);
            if (!string.IsNullOrEmpty(modifiers)) parts.Add(modifiers);

            parts.Add(GetTypeKind(type));
            parts.Add(FormatTypeName(type));

            var line = string.Join(" ", parts);

            if (type.IsEnum)
            {
                line += " : " + FormatTypeName(Enum.GetUnderlyingType(type));
                return line + FormatAttributesSuffix(type);
            }

            var relations = new List<string>();
            if (type.BaseType is not null && type.BaseType != typeof(object) && type.BaseType != typeof(ValueType))
            {
                relations.Add(FormatTypeName(type.BaseType));
            }

            relations.AddRange(
                type.GetInterfaces()
                    .Where(IsPublicSurfaceType)
                    .Select(FormatTypeName)
                    .Distinct()
                    .OrderBy(n => n, StringComparer.Ordinal));

            if (relations.Count > 0)
            {
                line += " : " + string.Join(", ", relations);
            }

            return line + FormatAttributesSuffix(type);
        }

        private static string GetTypeAccessibility(Type type)
        {
            if (!type.IsNested) return "public";
            if (type.IsNestedPublic) return "public";
            if (type.IsNestedFamily) return "protected";
            if (type.IsNestedFamORAssem) return "protected internal";
            return "internal"; // unreachable: filtered out by IsPublicSurfaceType
        }

        private static string GetTypeKind(Type type)
        {
            if (type.IsEnum) return "enum";
            if (type.IsInterface) return "interface";
            if (typeof(Delegate).IsAssignableFrom(type)) return "delegate";
            if (type.IsValueType) return "struct";
            return "class";
        }

        private static string GetTypeModifiers(Type type)
        {
            if (type.IsInterface || type.IsEnum) return string.Empty;
            if (type.IsAbstract && type.IsSealed) return "static";

            var mods = new List<string>();
            if (type.IsAbstract) mods.Add("abstract");
            if (type.IsSealed) mods.Add("sealed");
            return string.Join(" ", mods);
        }

        // ----- member-level -----

        private static IEnumerable<string> BuildMemberLines(Type type)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            foreach (var ctor in type.GetConstructors(flags))
            {
                if (ctor.IsStatic) continue; // static initializers are not consumer-facing
                if (IsCompilerGenerated(ctor)) continue;
                if (!IsExternallyVisible(ctor.Attributes)) continue;
                yield return BuildConstructorLine(type, ctor);
            }

            foreach (var method in type.GetMethods(flags))
            {
                if (IsCompilerGenerated(method)) continue;
                if (!IsExternallyVisible(method.Attributes)) continue;
                if (method.IsSpecialName && IsAccessorName(method.Name)) continue; // represented via property/event below
                yield return BuildMethodLine(method);
            }

            foreach (var property in type.GetProperties(flags))
            {
                var line = BuildPropertyLine(property);
                if (line is not null) yield return line;
            }

            foreach (var evt in type.GetEvents(flags))
            {
                var line = BuildEventLine(evt);
                if (line is not null) yield return line;
            }

            foreach (var field in type.GetFields(flags))
            {
                if (field.IsSpecialName) continue; // e.g. enum's "value__" backing field
                if (IsCompilerGenerated(field)) continue;
                if (!IsExternallyVisible(field.Attributes)) continue;
                yield return BuildFieldLine(field);
            }
        }

        private static bool IsAccessorName(string name) =>
            name.StartsWith("get_", StringComparison.Ordinal) ||
            name.StartsWith("set_", StringComparison.Ordinal) ||
            name.StartsWith("add_", StringComparison.Ordinal) ||
            name.StartsWith("remove_", StringComparison.Ordinal);

        private static string BuildConstructorLine(Type type, ConstructorInfo ctor)
        {
            var vis = GetAccessibility(ctor.Attributes);
            var name = GetSimpleName(type);
            var parameters = string.Join(", ", ctor.GetParameters().Select(FormatParameter));
            return $"ctor {vis} {name}({parameters}){FormatAttributesSuffix(ctor)}";
        }

        // TODO (Stage 2+): no operator overloads exist in the library today, so `method.Name` for one would
        // currently print verbatim as the raw CLR name (e.g. "op_Addition"). If/when an operator overload is
        // added, special-case IsSpecialName + the "op_" prefix here to render it in C# operator syntax
        // (e.g. "operator +(...)") instead of silently emitting the mangled CLR name.
        private static string BuildMethodLine(MethodInfo method)
        {
            var vis = GetAccessibility(method.Attributes);
            var modifiers = GetMethodModifiers(method);
            var modifiersText = string.IsNullOrEmpty(modifiers) ? string.Empty : modifiers + " ";
            var returnType = FormatTypeName(method.ReturnType);
            var generic = method.IsGenericMethodDefinition
                ? "<" + string.Join(", ", method.GetGenericArguments().Select(FormatTypeName)) + ">"
                : string.Empty;
            var parameters = string.Join(", ", method.GetParameters().Select(FormatParameter));
            return $"method {vis} {modifiersText}{returnType} {method.Name}{generic}({parameters}){FormatAttributesSuffix(method)}";
        }

        private static string? BuildPropertyLine(PropertyInfo property)
        {
            var getter = property.GetGetMethod(nonPublic: true);
            var setter = property.GetSetMethod(nonPublic: true);

            var getterVisible = getter is not null && !IsCompilerGenerated(getter) && IsExternallyVisible(getter.Attributes);
            var setterVisible = setter is not null && !IsCompilerGenerated(setter) && IsExternallyVisible(setter.Attributes);
            if (!getterVisible && !setterVisible) return null;

            var getterVis = getterVisible ? GetAccessibility(getter!.Attributes) : null;
            var setterVis = setterVisible ? GetAccessibility(setter!.Attributes) : null;
            var dominant = new[] { getterVis, setterVis }
                .Where(v => v is not null)
                .OrderByDescending(v => VisibilityRank(v!))
                .First()!;

            var accessors = new List<string>();
            if (getterVisible) accessors.Add(getterVis == dominant ? "get;" : $"{getterVis} get;");
            if (setterVisible) accessors.Add(setterVis == dominant ? "set;" : $"{setterVis} set;");

            var modifierSource = getterVisible ? getter! : setter!;
            var modifiers = GetMethodModifiers(modifierSource);
            var modifiersText = string.IsNullOrEmpty(modifiers) ? string.Empty : modifiers + " ";

            var indexParameters = property.GetIndexParameters();
            var name = indexParameters.Length > 0
                ? $"this[{string.Join(", ", indexParameters.Select(FormatParameter))}]"
                : property.Name;

            var typeName = FormatTypeName(property.PropertyType);
            return $"property {dominant} {modifiersText}{typeName} {name} {{ {string.Join(" ", accessors)} }}{FormatAttributesSuffix(property)}";
        }

        private static string? BuildEventLine(EventInfo evt)
        {
            var add = evt.GetAddMethod(nonPublic: true);
            if (add is null || IsCompilerGenerated(add) || !IsExternallyVisible(add.Attributes)) return null;

            var vis = GetAccessibility(add.Attributes);
            var modifiers = GetMethodModifiers(add);
            var modifiersText = string.IsNullOrEmpty(modifiers) ? string.Empty : modifiers + " ";
            var typeName = FormatTypeName(evt.EventHandlerType!);
            return $"event {vis} {modifiersText}{typeName} {evt.Name}{FormatAttributesSuffix(evt)}";
        }

        private static string BuildFieldLine(FieldInfo field)
        {
            var vis = GetAccessibility(field.Attributes);

            var modifiers = new List<string>();
            if (field.IsLiteral)
            {
                modifiers.Add("const");
            }
            else
            {
                if (field.IsStatic) modifiers.Add("static");
                if (field.IsInitOnly) modifiers.Add("readonly");
            }
            var modifiersText = modifiers.Count > 0 ? string.Join(" ", modifiers) + " " : string.Empty;

            var typeName = FormatTypeName(field.FieldType);
            var valueText = field.IsLiteral ? " = " + FormatLiteral(field.GetRawConstantValue(), field.FieldType) : string.Empty;

            return $"field {vis} {modifiersText}{typeName} {field.Name}{valueText}{FormatAttributesSuffix(field)}";
        }

        // ----- attributes -----

        /// <summary>
        /// Attribute type names (short form, e.g. "CompilerGenerated") that are emitted by the compiler or
        /// runtime as implementation detail rather than authored by a library developer to convey API intent.
        /// This is a DENYLIST (not an allowlist) on purpose: a genuinely new, user-authored attribute (e.g. a
        /// future <c>[Experimental]</c> or a custom SQLBI attribute) is captured by default rather than
        /// silently dropped; only known compiler/infrastructure noise is excluded here.
        /// </summary>
        private static readonly HashSet<string> NoiseAttributeNames = new(StringComparer.Ordinal)
        {
            "CompilerGeneratedAttribute",
            "NullableAttribute",
            "NullableContextAttribute",
            "NativeIntegerAttribute",
            "IsReadOnlyAttribute",
            "IsByRefLikeAttribute",
            "IsUnmanagedAttribute",
            "ScopedRefAttribute",
            "RefSafetyRulesAttribute",
            "DynamicAttribute",
            "TupleElementNamesAttribute",
            "AsyncStateMachineAttribute",
            "AsyncIteratorStateMachineAttribute",
            "IteratorStateMachineAttribute",
            "AsyncMethodBuilderAttribute",
            "ExtensionAttribute", // compiler-synthesized marker for "this"-parameter extension methods
            "DebuggerBrowsableAttribute",
            "DebuggerDisplayAttribute",
            "DebuggerHiddenAttribute",
            "DebuggerNonUserCodeAttribute",
            "DebuggerStepThroughAttribute",
            "DebuggerStepperBoundaryAttribute",
            "DebuggerTypeProxyAttribute",
            "DebuggerVisualizerAttribute",
        };

        /// <summary>
        /// Builds a deterministic, sorted, comma-separated "[Attr1, Attr2]" suffix (or "" if none) for the
        /// application-relevant custom attributes directly declared on <paramref name="member"/>. Uses
        /// <see cref="MemberInfo.GetCustomAttributesData"/> rather than instantiating attribute instances, so
        /// a malformed/unloadable attribute value cannot itself throw while dumping the surface, and renders
        /// each attribute by its short type name (the "Attribute" suffix trimmed) for readability.
        /// </summary>
        private static string FormatAttributesSuffix(MemberInfo member)
        {
            var names = member.GetCustomAttributesData()
                .Select(a => a.AttributeType.Name)
                .Where(n => !NoiseAttributeNames.Contains(n))
                .Select(TrimAttributeSuffix)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            return names.Length == 0 ? string.Empty : " [" + string.Join(", ", names) + "]";
        }

        private static string TrimAttributeSuffix(string name) =>
            name.EndsWith("Attribute", StringComparison.Ordinal) ? name[..^"Attribute".Length] : name;

        // ----- shared helpers -----

        private static bool IsCompilerGenerated(MemberInfo member) =>
            member.GetCustomAttribute<CompilerGeneratedAttribute>() is not null;

        private static bool IsExternallyVisible(FieldAttributes access)
        {
            var masked = access & FieldAttributes.FieldAccessMask;
            return masked is FieldAttributes.Public or FieldAttributes.Family or FieldAttributes.FamORAssem;
        }

        private static bool IsExternallyVisible(MethodAttributes access)
        {
            var masked = access & MethodAttributes.MemberAccessMask;
            return masked is MethodAttributes.Public or MethodAttributes.Family or MethodAttributes.FamORAssem;
        }

        private static string GetAccessibility(MethodAttributes access)
        {
            var masked = access & MethodAttributes.MemberAccessMask;
            return masked switch
            {
                MethodAttributes.Public => "public",
                MethodAttributes.Family => "protected",
                MethodAttributes.FamORAssem => "protected internal",
                _ => "private", // unreachable: filtered out before this is called
            };
        }

        private static string GetAccessibility(FieldAttributes access)
        {
            var masked = access & FieldAttributes.FieldAccessMask;
            return masked switch
            {
                FieldAttributes.Public => "public",
                FieldAttributes.Family => "protected",
                FieldAttributes.FamORAssem => "protected internal",
                _ => "private", // unreachable: filtered out before this is called
            };
        }

        private static int VisibilityRank(string visibility) => visibility switch
        {
            "public" => 3,
            "protected internal" => 2,
            "protected" => 1,
            _ => 0,
        };

        private static string GetMethodModifiers(MethodInfo method)
        {
            var mods = new List<string>();
            if (method.IsStatic) mods.Add("static");

            var isNewSlot = (method.Attributes & MethodAttributes.VtableLayoutMask) == MethodAttributes.NewSlot;
            if (method.IsAbstract) mods.Add("abstract");
            else if (method.IsVirtual && method.IsFinal && !isNewSlot) mods.Add("sealed override");
            else if (method.IsVirtual && !isNewSlot) mods.Add("override");
            else if (method.IsVirtual && isNewSlot) mods.Add("virtual");

            return string.Join(" ", mods);
        }

        private static string GetSimpleName(Type type)
        {
            if (!type.IsGenericType) return type.Name;

            var name = type.Name;
            var backtick = name.IndexOf('`');
            if (backtick >= 0) name = name[..backtick];
            var args = type.GetGenericArguments().Select(FormatTypeName);
            return $"{name}<{string.Join(", ", args)}>";
        }

        private static string FormatParameter(ParameterInfo parameter)
        {
            var type = parameter.ParameterType;
            var prefix = string.Empty;

            if (type.IsByRef)
            {
                type = type.GetElementType()!;
                prefix = parameter.IsOut ? "out " : (parameter.IsIn ? "in " : "ref ");
            }
            else if (parameter.GetCustomAttribute<ParamArrayAttribute>() is not null)
            {
                prefix = "params ";
            }

            var typeName = FormatTypeName(type);
            var defaultText = FormatDefaultValue(parameter);
            return $"{prefix}{typeName} {parameter.Name}{defaultText}";
        }

        private static string FormatDefaultValue(ParameterInfo parameter)
        {
            if (!parameter.HasDefaultValue) return string.Empty;

            object? value;
            try
            {
                value = parameter.DefaultValue;
            }
            catch (Exception ex) when (ex is FormatException or InvalidOperationException)
            {
                // e.g. "= default" on a non-primitive value type cannot be represented as a metadata constant.
                return " = default";
            }

            return " = " + FormatLiteral(value, parameter.ParameterType);
        }

        private static string FormatLiteral(object? value, Type declaredType)
        {
            if (value is null || value is DBNull)
            {
                // Reflection reports HasDefaultValue=true/DefaultValue=null for "= default" on a non-nullable
                // value type (e.g. CancellationToken cancellationToken = default) since it cannot represent a
                // non-primitive struct default as a metadata constant. Only render "null" for reference types
                // and Nullable<T>; a non-nullable value type must have been "default", not an actual null.
                var isNullableValueType = declaredType.IsValueType && Nullable.GetUnderlyingType(declaredType) is not null;
                return declaredType.IsValueType && !isNullableValueType ? "default" : "null";
            }
            if (value is bool b) return b ? "true" : "false";
            if (value is string s) return $"\"{s}\"";

            var underlyingType = Nullable.GetUnderlyingType(declaredType) ?? declaredType;
            if (underlyingType.IsEnum)
            {
                // Render BOTH the symbolic name (readability) and the underlying integral value (in
                // parentheses). The symbolic name alone is tautological for an enum MEMBER's own constant
                // (e.g. "AutoNamingEnum.Prefix = AutoNamingEnum.Prefix") and is blind to a renumber (swapping
                // Prefix=0/Suffix=1, or changing a [Flags] bit) since the symbolic name is unchanged even
                // though the wire/serialized value is. `value` here is already the raw underlying integral
                // (from FieldInfo.GetRawConstantValue()/ParameterInfo.DefaultValue), so it is formatted
                // directly rather than re-derived from the enum instance.
                var symbolicName = Enum.ToObject(underlyingType, value);
                var integralValue = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "0";
                return $"{FormatTypeName(underlyingType)}.{symbolicName} ({integralValue})";
            }

            return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "default";
        }

        private static string FormatTypeName(Type type)
        {
            if (type.IsGenericParameter) return type.Name;

            if (type.IsByRef) return FormatTypeName(type.GetElementType()!) + "&";

            if (type.IsArray)
            {
                var rank = type.GetArrayRank();
                var brackets = rank == 1 ? "[]" : "[" + new string(',', rank - 1) + "]";
                return FormatTypeName(type.GetElementType()!) + brackets;
            }

            var nullableUnderlying = Nullable.GetUnderlyingType(type);
            if (nullableUnderlying is not null) return FormatTypeName(nullableUnderlying) + "?";

            if (type.IsGenericType)
            {
                var definition = type.GetGenericTypeDefinition();
                var name = GetQualifiedNameWithoutArity(definition);

                var args = type.GetGenericArguments().Select(FormatTypeName);
                return $"{name}<{string.Join(", ", args)}>";
            }

            var fullName = type.FullName ?? type.Name;
            return fullName.Replace('+', '.');
        }

        /// <summary>
        /// Builds "Namespace.Outer.Inner" for a (possibly nested) generic type definition by stripping each
        /// name segment's own `N arity suffix individually. Using <see cref="Type.FullName"/> directly and
        /// stripping only the first backtick would truncate everything after a "+" nesting separator for a
        /// type nested inside a generic type (e.g. it would collapse "Outer`1+Inner" down to "Outer",
        /// silently dropping ".Inner" from the dump).
        /// </summary>
        private static string GetQualifiedNameWithoutArity(Type genericTypeDefinition)
        {
            var segments = new List<string>();
            for (var current = genericTypeDefinition; current is not null; current = current.DeclaringType)
            {
                var name = current.Name;
                var backtick = name.IndexOf('`');
                segments.Insert(0, backtick >= 0 ? name[..backtick] : name);
            }

            var ns = genericTypeDefinition.Namespace;
            var joined = string.Join(".", segments);
            return string.IsNullOrEmpty(ns) ? joined : ns + "." + joined;
        }
    }
}
