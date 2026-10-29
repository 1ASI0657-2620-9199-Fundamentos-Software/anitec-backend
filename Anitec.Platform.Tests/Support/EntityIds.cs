using System.Reflection;

namespace Anitec.Platform.Tests.Support;

/// <summary>
///     Some aggregates expose a get-only Id that the database generates. Tests assign it through the
///     compiler-generated backing field so that several entities can have distinct ids.
/// </summary>
public static class EntityIds
{
    public static T WithId<T>(this T entity, int id) where T : class
    {
        var field = typeof(T).GetField("<Id>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException($"{typeof(T).Name} has no auto-property Id.");
        field.SetValue(entity, id);
        return entity;
    }
}
