using System.Collections;
using System.Reflection;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace Ironbees.Core.Yaml;

/// <summary>A key in a YAML document that no property of the target type reads.</summary>
/// <param name="Path">Dotted path of the key (<c>model.max_tokens</c>, <c>steps[2].agent_name</c>).</param>
/// <param name="Suggestion">The key the target type does read when it differs only in spelling convention or case
/// (<c>maxTokens</c> for <c>max_tokens</c>), otherwise <c>null</c>.</param>
internal sealed record YamlUnknownKey(string Path, string? Suggestion)
{
    public override string ToString() =>
        Suggestion is null
            ? $"Unknown key '{Path}' is not read"
            : $"Unknown key '{Path}' is not read (did you mean '{Suggestion}'?)";
}

/// <summary>
/// Finds the keys a deserializer built with <c>IgnoreUnmatchedProperties()</c> drops without a word — a misspelled or
/// wrongly cased key (<c>max_tokens</c> where the file format is camelCase) silently becomes the property's default.
/// </summary>
/// <remarks>
/// A key is known when a public settable (or init) property of the target type maps to it through the naming convention,
/// or through <see cref="YamlMemberAttribute.Alias"/>; <see cref="YamlIgnoreAttribute"/> properties are not known.
/// Values of class-typed properties, and the class items of list-typed ones, are checked against their own types.
/// Dictionary-typed properties take any key and are not descended into; nor are scalar or <see cref="object"/> values.
/// </remarks>
internal static class YamlUnknownKeys
{
    public static IReadOnlyList<YamlUnknownKey> Find(string yaml, Type target, INamingConvention namingConvention)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(namingConvention);
        if (string.IsNullOrWhiteSpace(yaml))
            return [];

        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));

        var unknown = new List<YamlUnknownKey>();
        foreach (var document in stream.Documents)
        {
            if (document.RootNode is YamlMappingNode root)
                Walk(root, target, namingConvention, prefix: "", unknown);
        }

        return unknown;
    }

    private static void Walk(YamlMappingNode map, Type type, INamingConvention naming, string prefix, List<YamlUnknownKey> unknown)
    {
        var known = KnownKeys(type, naming);
        foreach (var (keyNode, valueNode) in map.Children)
        {
            if (keyNode is not YamlScalarNode { Value: { } key } || key == "<<")
                continue;

            var path = prefix.Length == 0 ? key : $"{prefix}.{key}";
            if (known.TryGetValue(key, out var property))
            {
                Descend(valueNode, property.PropertyType, naming, path, unknown);
                continue;
            }

            var normalized = Normalize(key);
            var suggestion = known.Keys.FirstOrDefault(k => Normalize(k) == normalized);
            unknown.Add(new YamlUnknownKey(path, suggestion));
        }
    }

    private static void Descend(YamlNode node, Type type, INamingConvention naming, string path, List<YamlUnknownKey> unknown)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (IsOpaque(type))
            return;

        if (type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type))
        {
            if (node is not YamlSequenceNode sequence || ElementType(type) is not { } element || IsOpaque(element))
                return;
            var i = 0;
            foreach (var item in sequence.Children)
            {
                if (item is YamlMappingNode itemMap)
                    Walk(itemMap, element, naming, $"{path}[{i}]", unknown);
                i++;
            }
            return;
        }

        if (node is YamlMappingNode map)
            Walk(map, type, naming, path, unknown);
    }

    /// <summary>Types whose YAML value is not checked: scalars, <see cref="object"/>, dictionaries.</summary>
    private static bool IsOpaque(Type type) =>
        type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(object) || type == typeof(decimal)
        || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan) || type == typeof(Guid)
        || type == typeof(Uri) || IsDictionary(type);

    private static bool IsDictionary(Type type) =>
        typeof(IDictionary).IsAssignableFrom(type)
        || type.GetInterfaces().Append(type).Any(i => i.IsGenericType &&
            (i.GetGenericTypeDefinition() == typeof(IDictionary<,>) || i.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)));

    private static Type? ElementType(Type type) =>
        type.IsArray
            ? type.GetElementType()
            : type.GetInterfaces().Append(type)
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                ?.GetGenericArguments()[0];

    private static Dictionary<string, PropertyInfo> KnownKeys(Type type, INamingConvention naming)
    {
        var keys = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.SetMethod is null || property.GetIndexParameters().Length > 0)
                continue;
            if (property.GetCustomAttribute<YamlIgnoreAttribute>() is not null)
                continue;
            var alias = property.GetCustomAttribute<YamlMemberAttribute>()?.Alias;
            keys.TryAdd(string.IsNullOrEmpty(alias) ? naming.Apply(property.Name) : alias, property);
        }

        return keys;
    }

    private static string Normalize(string key) =>
        new string(key.Where(c => c is not ('_' or '-')).ToArray()).ToUpperInvariant();
}
