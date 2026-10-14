using Discord;

namespace Asterion.Test.Lists;

internal static class ComponentTree
{
    /// <summary>Discord's limit of components per message, nested ones included</summary>
    public const int MaxComponentsPerMessage = 40;

    public static List<IMessageComponent> Flatten(MessageComponent components)
    {
        return Flatten(components.Components).ToList();
    }

    private static IEnumerable<IMessageComponent> Flatten(IEnumerable<IMessageComponent> components)
    {
        foreach (var component in components)
        {
            yield return component;

            IEnumerable<IMessageComponent> children = component switch
            {
                ContainerComponent container => container.Components,
                SectionComponent section => section.Components.Append(section.Accessory),
                ActionRowComponent row => row.Components,
                _ => []
            };

            foreach (var child in Flatten(children))
                yield return child;
        }
    }
}
