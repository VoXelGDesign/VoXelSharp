using VoXelSharp.Protocol.Packets;

namespace VoXelSharp.Server.Configuration;

/// <summary>
/// Supplies the server-side data needed to drive the mandatory Configuration session flow
/// (Known Packs -&gt; Registry Data -&gt; Update Tags -&gt; Feature Flags -&gt; Finish Configuration).
/// </summary>
public interface IConfigurationDataProvider
{
    /// <summary>Known Packs (clientbound, select_known_packs): the server's data packs.</summary>
    KnownPacksPacket KnownPacks { get; }

    /// <summary>Feature Flags (clientbound, update_enabled_features): enabled features.</summary>
    FeatureFlagsPacket FeatureFlags { get; }

    /// <summary>Update Tags (clientbound, update_tags): registry tag definitions.</summary>
    UpdateTagsPacket UpdateTags { get; }

    /// <summary>Registry Data (clientbound, registry_data): one packet per synchronized registry.</summary>
    IReadOnlyList<RegistryDataPacket> RegistryData { get; }
}

/// <summary>
/// Default <see cref="IConfigurationDataProvider"/> for Minecraft 26.3 / protocol 777.
/// Registry entry lists are verified from <c>docs/data/registries.json</c>; the advertised
/// known pack is <c>minecraft:core</c>, the only data pack a vanilla client recognizes
/// (wiki "Known packs negotiation", 2026-10-09). Entry order defines the numeric IDs the
/// client assigns, so the lists below preserve the ordering from <c>docs/data/registries.json</c>.
/// </summary>
public sealed class ServerConfigurationDataProvider : IConfigurationDataProvider
{
    /// <summary>Known Packs version string for <c>minecraft:core</c> (matches the Minecraft version).</summary>
    public const string CorePackVersion = "26.3";

    /// <inheritdoc />
    public KnownPacksPacket KnownPacks { get; } =
        new([new KnownPack("minecraft", "core", CorePackVersion)]);

    /// <inheritdoc />
    public FeatureFlagsPacket FeatureFlags { get; } = new(["minecraft:vanilla"]);

    /// <inheritdoc />
    public UpdateTagsPacket UpdateTags { get; } = new([]);

    /// <inheritdoc />
    public IReadOnlyList<RegistryDataPacket> RegistryData { get; } = BuildRegistryData();

    private static IReadOnlyList<RegistryDataPacket> BuildRegistryData()
    {
        // Entry ID lists are copied verbatim from docs/data/registries.json (Minecraft 26.3 /
        // protocol 777, wiki-compiled). Entry Data is omitted (Prefixed Optional NBT = false):
        // the client's entry NBT is sourced from the advertised minecraft:core known pack.
        // Registries whose required entry lists are not yet compiled (cat_variant and similar
        // "Non-empty" synchronized registries) are not sent yet: their entries are UNVERIFIED.
        return
        [
            new RegistryDataPacket("minecraft:banner_pattern",
            [
                new RegistryDataEntry("minecraft:bordure_indented", null),
                new RegistryDataEntry("minecraft:creeper", null),
                new RegistryDataEntry("minecraft:field_masoned", null),
                new RegistryDataEntry("minecraft:flow", null),
                new RegistryDataEntry("minecraft:flower", null),
                new RegistryDataEntry("minecraft:globe", null),
                new RegistryDataEntry("minecraft:guster", null),
                new RegistryDataEntry("minecraft:mojang", null),
                new RegistryDataEntry("minecraft:piglin", null),
                new RegistryDataEntry("minecraft:skull", null),
            ]),
            new RegistryDataPacket("minecraft:damage_type",
            [
                new RegistryDataEntry("minecraft:cactus", null),
                new RegistryDataEntry("minecraft:campfire", null),
                new RegistryDataEntry("minecraft:cramming", null),
                new RegistryDataEntry("minecraft:dragon_breath", null),
                new RegistryDataEntry("minecraft:drown", null),
                new RegistryDataEntry("minecraft:dry_out", null),
                new RegistryDataEntry("minecraft:ender_pearl", null),
                new RegistryDataEntry("minecraft:fall", null),
                new RegistryDataEntry("minecraft:fly_into_wall", null),
                new RegistryDataEntry("minecraft:freeze", null),
                new RegistryDataEntry("minecraft:generic", null),
                new RegistryDataEntry("minecraft:generic_kill", null),
                new RegistryDataEntry("minecraft:hot_floor", null),
                new RegistryDataEntry("minecraft:in_fire", null),
                new RegistryDataEntry("minecraft:in_wall", null),
                new RegistryDataEntry("minecraft:lava", null),
                new RegistryDataEntry("minecraft:lightning_bolt", null),
                new RegistryDataEntry("minecraft:magic", null),
                new RegistryDataEntry("minecraft:on_fire", null),
                new RegistryDataEntry("minecraft:out_of_world", null),
                new RegistryDataEntry("minecraft:outside_border", null),
                new RegistryDataEntry("minecraft:spear", null),
                new RegistryDataEntry("minecraft:stalagmite", null),
                new RegistryDataEntry("minecraft:starve", null),
                new RegistryDataEntry("minecraft:sweet_berry_bush", null),
                new RegistryDataEntry("minecraft:wither", null),
            ]),
            new RegistryDataPacket("minecraft:dimension_type",
            [
                new RegistryDataEntry("minecraft:overworld", null),
                new RegistryDataEntry("minecraft:the_nether", null),
                new RegistryDataEntry("minecraft:the_end", null),
            ]),
            new RegistryDataPacket("minecraft:instrument",
            [
                new RegistryDataEntry("minecraft:ponder_goat_horn", null),
            ]),
            new RegistryDataPacket("minecraft:jukebox_song",
            [
                new RegistryDataEntry("minecraft:11", null),
                new RegistryDataEntry("minecraft:13", null),
                new RegistryDataEntry("minecraft:5", null),
                new RegistryDataEntry("minecraft:blocks", null),
                new RegistryDataEntry("minecraft:bounce", null),
                new RegistryDataEntry("minecraft:cat", null),
                new RegistryDataEntry("minecraft:chirp", null),
                new RegistryDataEntry("minecraft:creator", null),
                new RegistryDataEntry("minecraft:creator_music_box", null),
                new RegistryDataEntry("minecraft:far", null),
                new RegistryDataEntry("minecraft:lava_chicken", null),
                new RegistryDataEntry("minecraft:mall", null),
                new RegistryDataEntry("minecraft:mellohi", null),
                new RegistryDataEntry("minecraft:otherside", null),
                new RegistryDataEntry("minecraft:pigstep", null),
                new RegistryDataEntry("minecraft:precipice", null),
                new RegistryDataEntry("minecraft:relic", null),
                new RegistryDataEntry("minecraft:stal", null),
                new RegistryDataEntry("minecraft:strad", null),
                new RegistryDataEntry("minecraft:tears", null),
                new RegistryDataEntry("minecraft:wait", null),
                new RegistryDataEntry("minecraft:ward", null),
            ]),
            new RegistryDataPacket("minecraft:trim_material",
            [
                new RegistryDataEntry("minecraft:amethyst", null),
                new RegistryDataEntry("minecraft:copper", null),
                new RegistryDataEntry("minecraft:diamond", null),
                new RegistryDataEntry("minecraft:emerald", null),
                new RegistryDataEntry("minecraft:gold", null),
                new RegistryDataEntry("minecraft:iron", null),
                new RegistryDataEntry("minecraft:lapis", null),
                new RegistryDataEntry("minecraft:netherite", null),
                new RegistryDataEntry("minecraft:quartz", null),
                new RegistryDataEntry("minecraft:redstone", null),
                new RegistryDataEntry("minecraft:resin", null),
            ]),
            new RegistryDataPacket("minecraft:worldgen/biome",
            [
                new RegistryDataEntry("minecraft:plains", null),
            ]),
            new RegistryDataPacket("minecraft:chicken_variant",
            [
                new RegistryDataEntry("minecraft:cold", null),
                new RegistryDataEntry("minecraft:temperate", null),
                new RegistryDataEntry("minecraft:warm", null),
            ]),
        ];
    }
}
