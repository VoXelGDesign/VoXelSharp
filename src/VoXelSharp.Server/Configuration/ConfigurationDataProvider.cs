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
    /// <remarks>
    /// Tag entry IDs are 0-based indices into the corresponding Registry Data entry order
    /// (the order in which entries are sent for that registry). Empty entry lists mean the tag
    /// exists so references resolve; the client supplies the actual entries from its pack.
    /// </remarks>
    public UpdateTagsPacket UpdateTags { get; } = new(
    [
        // Timeline tags (required by 26.3 dimension_type data which references
        // "timelines": "#minecraft:in_overworld" and "default_clock": "minecraft:overworld").
        // The client resolves timeline tags through the "root" tag registry (earlier crash:
        // "Unbound tags in registry ResourceKey[minecraft:root / minecraft:timeline]").
        // IDs: 0=day, 1=early_game, 2=moon, 3=villager_schedule (our minecraft:timeline Registry
        // Data order). Tag values from vanilla 26.3 data/minecraft/tags/timeline/.
        new TaggedRegistry("minecraft:timeline",
        [
            // in_overworld = ["#minecraft:universal", "minecraft:day", "minecraft:moon", "minecraft:early_game"]
            RegistryTag.With("minecraft:in_overworld", 3, 0, 2, 1), // universal→villager_schedule(3), day(0), moon(2), early_game(1)
            RegistryTag.With("minecraft:in_nether", 3),              // universal→villager_schedule(3)
            RegistryTag.With("minecraft:in_end", 3),                 // universal→villager_schedule(3)
            RegistryTag.With("minecraft:universal", 3),              // villager_schedule(3)
        ]),

        // Block infiniburn tags referenced by dimension_type's "infiniburn" field, plus tags
        // referenced by enchantment entry NBT (block references in effects). Must exist in the
        // block registry even with empty entry lists.
        TaggedRegistry.WithEmptyTags("minecraft:block",
            "minecraft:infiniburn_overworld", "minecraft:infiniburn_end", "minecraft:infiniburn_nether",
            "minecraft:lightning_rods", "minecraft:soul_speed_blocks", "minecraft:blocks_wind_charge_explosions"),

        // Damage type tags — items reference these during data component building. Empty entry
        // lists (tags exist to resolve references; actual entries defined by the client's
        // built-in damage type tag files as the pack provides them).
        TaggedRegistry.WithEmptyTags("minecraft:damage_type",
            "minecraft:always_hurts_ender_dragons", "minecraft:always_kills_armor_stands",
            "minecraft:always_most_significant_fall", "minecraft:always_triggers_silverfish",
            "minecraft:avoids_guardian_thorns", "minecraft:burn_from_stepping",
            "minecraft:burns_armor_stands", "minecraft:bypasses_armor",
            "minecraft:bypasses_cooldown", "minecraft:bypasses_effects",
            "minecraft:bypasses_enchantments", "minecraft:bypasses_invulnerability",
            "minecraft:bypasses_resistance", "minecraft:bypasses_shield",
            "minecraft:bypasses_wolf_armor", "minecraft:can_break_armor_stand",
            "minecraft:damages_helmet", "minecraft:ignites_armor_stands",
            "minecraft:is_drowning", "minecraft:is_explosion",
            "minecraft:is_fall", "minecraft:is_fire",
            "minecraft:is_freezing", "minecraft:is_lightning",
            "minecraft:is_player_attack", "minecraft:is_projectile",
            "minecraft:mace_smash", "minecraft:no_anger",
            "minecraft:no_impact", "minecraft:no_knockback",
            "minecraft:no_wolf_retaliation", "minecraft:panic_causes",
            "minecraft:panic_environmental_causes", "minecraft:sulfur_cube_with_block_immune_to",
            "minecraft:witch_resistant_to", "minecraft:wither_immune_to"),

        // All item tags referenced by registries we send (sulfur_cube_archetype, enchantment, etc.).
        // "Tag data is never sourced from known packs" — must be sent in full via UpdateTags.
        // Empty entry arrays: tags exist (references resolve) but no items assigned (acceptable for M4).
        TaggedRegistry.WithEmptyTags("minecraft:item",
            // Sulfur cube archetype item tags (referenced by each entry's "items" field).
            "minecraft:sulfur_cube_archetype/bouncy", "minecraft:sulfur_cube_archetype/explosive",
            "minecraft:sulfur_cube_archetype/fast_flat", "minecraft:sulfur_cube_archetype/fast_sliding",
            "minecraft:sulfur_cube_archetype/high_resistance", "minecraft:sulfur_cube_archetype/hot",
            "minecraft:sulfur_cube_archetype/light", "minecraft:sulfur_cube_archetype/regular",
            "minecraft:sulfur_cube_archetype/slow_bouncy", "minecraft:sulfur_cube_archetype/slow_flat",
            "minecraft:sulfur_cube_archetype/slow_sliding", "minecraft:sulfur_cube_archetype/sticky",
            // Enchantable tags (referenced by enchantment entry NBT supported_items/primary_items).
            "minecraft:enchantable/armor", "minecraft:enchantable/bow",
            "minecraft:enchantable/chest_armor", "minecraft:enchantable/crossbow",
            "minecraft:enchantable/durability", "minecraft:enchantable/equippable",
            "minecraft:enchantable/fire_aspect", "minecraft:enchantable/fishing",
            "minecraft:enchantable/foot_armor", "minecraft:enchantable/head_armor",
            "minecraft:enchantable/leg_armor", "minecraft:enchantable/lunge",
            "minecraft:enchantable/mace", "minecraft:enchantable/melee_weapon",
            "minecraft:enchantable/mining", "minecraft:enchantable/mining_loot",
            "minecraft:enchantable/sharp_weapon", "minecraft:enchantable/sweeping",
            "minecraft:enchantable/trident", "minecraft:enchantable/vanishing",
            "minecraft:enchantable/weapon",
            // Decorative/crafting tags (referenced by item component init).
            "minecraft:decorated_pot_sherds", "minecraft:decorated_pot_ingredients",
            "minecraft:trim_materials", "minecraft:trimmable_armor"),

        // Banner pattern tags from jar data/minecraft/tags/banner_pattern/.
        // no_item_required lists indices of 32 patterns that need no craftable item.
        // pattern_item/<name> tags list item entry IDs; we send empty (client loads data from pack).
        new TaggedRegistry("minecraft:banner_pattern",
        [
            // 32 patterns that require no craftable item (indices into our 43-entry alphabetical list).
            RegistryTag.With("minecraft:no_item_required",
                26, 27, 28, 29, 31, 38, 35, 37, 32, 36, 34, 33, 25, 5, 30,
                39, 40, 41, 42, 7, 10, 9, 8, 3, 23, 19, 17, 20, 18, 1, 14, 15),
            // 11 patterns that resolve from pack data (tags must exist for item component init).
            RegistryTag.Empty("minecraft:pattern_item/bordure_indented"),
            RegistryTag.Empty("minecraft:pattern_item/creeper"),
            RegistryTag.Empty("minecraft:pattern_item/field_masoned"),
            RegistryTag.Empty("minecraft:pattern_item/flow"),
            RegistryTag.Empty("minecraft:pattern_item/flower"),
            RegistryTag.Empty("minecraft:pattern_item/globe"),
            RegistryTag.Empty("minecraft:pattern_item/guster"),
            RegistryTag.Empty("minecraft:pattern_item/mojang"),
            RegistryTag.Empty("minecraft:pattern_item/piglin"),
            RegistryTag.Empty("minecraft:pattern_item/skull"),
        ]),

        // Enchantment tags — required for enchantment NBT tag references to resolve.
        // Entry indices follow our 43-entry alphabetical order (0 = aqua_affinity, ... 42 = wind_burst).
        new TaggedRegistry("minecraft:enchantment",
        [
            RegistryTag.With("minecraft:curse", 2, 41),
            RegistryTag.With("minecraft:double_trade_price", 2, 41, 38, 36, 14, 23, 42),
            RegistryTag.With("minecraft:exclusive_set/armor", 28, 3, 11, 27),
            RegistryTag.With("minecraft:exclusive_set/boots", 14, 7),
            RegistryTag.With("minecraft:exclusive_set/bow", 16, 23),
            RegistryTag.With("minecraft:exclusive_set/crossbow", 24, 25),
            RegistryTag.With("minecraft:exclusive_set/damage", 33, 35, 1, 15, 6, 4),
            RegistryTag.With("minecraft:exclusive_set/mining", 13, 34),
            RegistryTag.With("minecraft:exclusive_set/riptide", 19, 5),
            RegistryTag.With("minecraft:in_enchanting_table",
                28, 11, 9, 3, 27, 31, 0, 39, 7, 33, 35, 1, 17, 10, 18, 37,
                8, 34, 40, 13, 26, 29, 12, 16, 20, 22, 19, 15, 32, 5, 24, 30, 25, 6, 4, 21),
            RegistryTag.With("minecraft:non_treasure",
                28, 11, 9, 3, 27, 31, 0, 39, 7, 33, 35, 1, 17, 10, 18, 37,
                8, 34, 40, 13, 26, 29, 12, 16, 20, 22, 19, 15, 32, 5, 24, 30, 25, 6, 4, 21),
            RegistryTag.With("minecraft:on_mob_spawn_equipment",
                28, 11, 9, 3, 27, 31, 0, 39, 7, 33, 35, 1, 17, 10, 18, 37,
                8, 34, 40, 13, 26, 29, 12, 16, 20, 22, 19, 15, 32, 5, 24, 30, 25, 6, 4, 21),
            RegistryTag.With("minecraft:on_random_loot",
                28, 11, 9, 3, 27, 31, 0, 39, 7, 33, 35, 1, 17, 10, 18, 37, 8,
                34, 40, 13, 26, 29, 12, 16, 20, 22, 19, 15, 32, 5, 24, 30, 25, 6, 4, 21,
                2, 41, 14, 23),
            RegistryTag.With("minecraft:on_traded_equipment",
                28, 11, 9, 3, 27, 31, 0, 39, 7, 33, 35, 1, 17, 10, 18, 37,
                8, 34, 40, 13, 26, 29, 12, 16, 20, 22, 19, 15, 32, 5, 24, 30, 25, 6, 4, 21),
            RegistryTag.With("minecraft:prevents_bee_spawns_when_mining", 34),
            RegistryTag.With("minecraft:prevents_decorated_pot_shattering", 34),
            RegistryTag.With("minecraft:prevents_ice_melting", 34),
            RegistryTag.With("minecraft:prevents_infested_spawns", 34),
            RegistryTag.With("minecraft:smelts_loot", 10),
            RegistryTag.With("minecraft:tooltip_order",
                2, 41, 32, 5, 42, 14, 21, 33, 35, 1, 15, 26, 6, 4, 25, 37, 24, 10,
                12, 17, 29, 28, 3, 11, 27, 9, 13, 18, 34, 20, 8, 30, 22, 31, 0, 36, 38, 7,
                39, 19, 40, 16, 23),
            RegistryTag.With("minecraft:tradeable",
                28, 11, 9, 3, 27, 31, 0, 39, 7, 33, 35, 1, 17, 10, 18, 37, 8,
                34, 40, 13, 26, 29, 12, 16, 20, 22, 19, 15, 32, 5, 24, 30, 25, 6, 4, 21,
                2, 41, 14, 23),
            RegistryTag.With("minecraft:treasure", 2, 41, 38, 36, 14, 23, 42),
        ]),

        // Entity type tags referenced by enchantment NBT (entity_type predicates).
        // Empty entry lists — tags exist so NBT parsing succeeds; no entity types assigned.
        TaggedRegistry.WithEmptyTags("minecraft:entity_type",
            "minecraft:arrows", "minecraft:sensitive_to_bane_of_arthropods",
            "minecraft:sensitive_to_impaling", "minecraft:sensitive_to_smite"),
    ]);

    /// <inheritdoc />
    /// <remarks>
    /// Entry Data is omitted (Prefixed Optional NBT = false): the client's entry NBT is sourced
    /// from the advertised <c>minecraft:core</c> known pack. Entry order defines numeric IDs.
    /// </remarks>
    public IReadOnlyList<RegistryDataPacket> RegistryData { get; } = BuildRegistryData();

    private static IReadOnlyList<RegistryDataPacket> BuildRegistryData()
    {
        // Entry ID lists are copied verbatim from docs/data/registries.json (Minecraft 26.3 /
        // protocol 777, wiki-compiled) and the vanilla 26.3 data pack (where noted).
        return
        [
            // 43 actual 26.3 banner patterns extracted from the vanilla jar, verbatim alphabetical
            // order from data/minecraft/banner_pattern/ (Mojang data generator) matching the
            // client's minecraft:core known pack.
            RegistryDataPacket.Create("minecraft:banner_pattern",
                "minecraft:base", "minecraft:border", "minecraft:bricks", "minecraft:circle",
                "minecraft:creeper", "minecraft:cross", "minecraft:curly_border",
                "minecraft:diagonal_left", "minecraft:diagonal_right", "minecraft:diagonal_up_left",
                "minecraft:diagonal_up_right", "minecraft:flow", "minecraft:flower", "minecraft:globe",
                "minecraft:gradient", "minecraft:gradient_up", "minecraft:guster",
                "minecraft:half_horizontal", "minecraft:half_horizontal_bottom",
                "minecraft:half_vertical", "minecraft:half_vertical_right", "minecraft:mojang",
                "minecraft:piglin", "minecraft:rhombus", "minecraft:skull", "minecraft:small_stripes",
                "minecraft:square_bottom_left", "minecraft:square_bottom_right",
                "minecraft:square_top_left", "minecraft:square_top_right", "minecraft:straight_cross",
                "minecraft:stripe_bottom", "minecraft:stripe_center", "minecraft:stripe_downleft",
                "minecraft:stripe_downright", "minecraft:stripe_left", "minecraft:stripe_middle",
                "minecraft:stripe_right", "minecraft:stripe_top", "minecraft:triangle_bottom",
                "minecraft:triangle_top", "minecraft:triangles_bottom", "minecraft:triangles_top"),

            // Full 51 damage_type entries from vanilla 26.3 jar (data/minecraft/damage_type/).
            RegistryDataPacket.Create("minecraft:damage_type",
                "minecraft:arrow", "minecraft:bad_respawn_point", "minecraft:cactus",
                "minecraft:campfire", "minecraft:cramming", "minecraft:dragon_breath",
                "minecraft:drown", "minecraft:dry_out", "minecraft:ender_pearl",
                "minecraft:explosion", "minecraft:fall", "minecraft:falling_anvil",
                "minecraft:falling_block", "minecraft:falling_stalactite", "minecraft:fireball",
                "minecraft:fireworks", "minecraft:fly_into_wall", "minecraft:freeze",
                "minecraft:generic", "minecraft:generic_kill", "minecraft:hot_floor",
                "minecraft:in_fire", "minecraft:in_wall", "minecraft:indirect_magic",
                "minecraft:lava", "minecraft:lightning_bolt", "minecraft:mace_smash",
                "minecraft:magic", "minecraft:mob_attack", "minecraft:mob_attack_no_aggro",
                "minecraft:mob_projectile", "minecraft:on_fire", "minecraft:out_of_world",
                "minecraft:outside_border", "minecraft:player_attack", "minecraft:player_explosion",
                "minecraft:sonic_boom", "minecraft:spear", "minecraft:spit", "minecraft:stalagmite",
                "minecraft:starve", "minecraft:sting", "minecraft:sulfur_cube_hot",
                "minecraft:sweet_berry_bush", "minecraft:thorns", "minecraft:thrown",
                "minecraft:trident", "minecraft:unattributed_fireball", "minecraft:wind_charge",
                "minecraft:wither", "minecraft:wither_skull"),

            // Timeline and world_clock registries (new in 26.3): the client's own pack provides
            // the NBT for these entries (Data omitted). Entry order defines the numeric IDs used
            // by timeline tags. Order matches the jar's alphabetical sort.
            RegistryDataPacket.Create("minecraft:timeline",
                "minecraft:day", "minecraft:early_game", "minecraft:moon", "minecraft:villager_schedule"),

            RegistryDataPacket.Create("minecraft:world_clock",
                "minecraft:overworld", "minecraft:the_end"),

            // worldgen/block_state_provider_type — referenced by dimension_type's
            // monster_spawn_light_level.type = "minecraft:uniform". Uses empty entry list so the
            // pack fills the actual sub-entries; uniform's NBT comes from the pack.
            RegistryDataPacket.Create("minecraft:worldgen/block_state_provider_type",
                "minecraft:uniform"),

            // Variant registries — required by 26.3 to be non-empty.
            // Entries from vanilla jar data/minecraft/<registry>/*.json; Data omitted (pack).
            RegistryDataPacket.Create("minecraft:cat_variant",
                "minecraft:all_black", "minecraft:black", "minecraft:british_shorthair",
                "minecraft:calico", "minecraft:jellie", "minecraft:persian", "minecraft:ragdoll",
                "minecraft:red", "minecraft:siamese", "minecraft:tabby", "minecraft:white"),
            RegistryDataPacket.Create("minecraft:cat_sound_variant",
                "minecraft:classic", "minecraft:royal"),
            RegistryDataPacket.Create("minecraft:chicken_sound_variant",
                "minecraft:classic", "minecraft:picky"),
            RegistryDataPacket.Create("minecraft:cow_sound_variant",
                "minecraft:classic", "minecraft:moody"),
            RegistryDataPacket.Create("minecraft:cow_variant",
                "minecraft:cold", "minecraft:temperate", "minecraft:warm"),
            RegistryDataPacket.Create("minecraft:frog_variant",
                "minecraft:cold", "minecraft:temperate", "minecraft:warm"),
            RegistryDataPacket.Create("minecraft:painting_variant",
                "minecraft:alban", "minecraft:aztec", "minecraft:aztec2", "minecraft:backyard",
                "minecraft:baroque", "minecraft:bomb", "minecraft:bouquet",
                "minecraft:burning_skull", "minecraft:bust", "minecraft:cavebird",
                "minecraft:changing", "minecraft:cotan", "minecraft:courbet", "minecraft:creebet",
                "minecraft:dennis", "minecraft:donkey_kong", "minecraft:earth", "minecraft:endboss",
                "minecraft:fern", "minecraft:fighters", "minecraft:finding", "minecraft:fire",
                "minecraft:graham", "minecraft:humble", "minecraft:kebab", "minecraft:lowmist",
                "minecraft:match", "minecraft:meditative", "minecraft:orb", "minecraft:owlemons",
                "minecraft:passage", "minecraft:pigscene", "minecraft:plant", "minecraft:pointer",
                "minecraft:pond", "minecraft:pool", "minecraft:prairie_ride", "minecraft:sea",
                "minecraft:skeleton", "minecraft:skull_and_roses", "minecraft:stage",
                "minecraft:sunflowers", "minecraft:sunset", "minecraft:tides", "minecraft:unpacked",
                "minecraft:void", "minecraft:wanderer", "minecraft:wasteland", "minecraft:water",
                "minecraft:wind", "minecraft:wither"),
            RegistryDataPacket.Create("minecraft:pig_sound_variant",
                "minecraft:big", "minecraft:classic", "minecraft:mini"),
            RegistryDataPacket.Create("minecraft:pig_variant",
                "minecraft:cold", "minecraft:temperate", "minecraft:warm"),
            RegistryDataPacket.Create("minecraft:wolf_sound_variant",
                "minecraft:angry", "minecraft:big", "minecraft:classic", "minecraft:cute",
                "minecraft:grumpy", "minecraft:puglin", "minecraft:sad"),
            RegistryDataPacket.Create("minecraft:wolf_variant",
                "minecraft:ashen", "minecraft:black", "minecraft:chestnut", "minecraft:pale",
                "minecraft:rusty", "minecraft:snowy", "minecraft:spotted", "minecraft:striped",
                "minecraft:woods"),
            RegistryDataPacket.Create("minecraft:zombie_nautilus_variant",
                "minecraft:temperate", "minecraft:warm"),

            // Block transformer (item tool transformers referenced by item properties).
            RegistryDataPacket.Create("minecraft:block_transformer",
                "minecraft:shovel", "minecraft:axe", "minecraft:hoe"),

            // Sulfur cube archetype (required synchronized registry, full 26.3 entries).
            RegistryDataPacket.Create("minecraft:sulfur_cube_archetype",
                "minecraft:bouncy", "minecraft:explosive", "minecraft:fast_flat",
                "minecraft:fast_sliding", "minecraft:high_resistance", "minecraft:hot",
                "minecraft:light", "minecraft:regular", "minecraft:slow_bouncy",
                "minecraft:slow_flat", "minecraft:slow_sliding", "minecraft:sticky"),

            RegistryDataPacket.Create("minecraft:dimension_type",
                "minecraft:overworld", "minecraft:overworld_caves", "minecraft:the_nether",
                "minecraft:the_end"),

            // Full 26.3 instrument entries from data/minecraft/instrument/.
            RegistryDataPacket.Create("minecraft:instrument",
                "minecraft:admire_goat_horn", "minecraft:call_goat_horn", "minecraft:dream_goat_horn",
                "minecraft:feel_goat_horn", "minecraft:ponder_goat_horn", "minecraft:seek_goat_horn",
                "minecraft:sing_goat_horn", "minecraft:yearn_goat_horn"),

            // Full 26.3 jukebox_song entries from data/minecraft/jukebox_song/.
            RegistryDataPacket.Create("minecraft:jukebox_song",
                "minecraft:11", "minecraft:13", "minecraft:5", "minecraft:blocks", "minecraft:bounce",
                "minecraft:cat", "minecraft:chirp", "minecraft:creator", "minecraft:creator_music_box",
                "minecraft:far", "minecraft:lava_chicken", "minecraft:mall", "minecraft:mellohi",
                "minecraft:otherside", "minecraft:pigstep", "minecraft:precipice", "minecraft:relic",
                "minecraft:stal", "minecraft:strad", "minecraft:tears", "minecraft:wait",
                "minecraft:ward"),

            RegistryDataPacket.Create("minecraft:trim_material",
                "minecraft:amethyst", "minecraft:copper", "minecraft:diamond", "minecraft:emerald",
                "minecraft:gold", "minecraft:iron", "minecraft:lapis", "minecraft:netherite",
                "minecraft:quartz", "minecraft:redstone", "minecraft:resin"),

            RegistryDataPacket.Create("minecraft:worldgen/biome",
                "minecraft:plains"),

            RegistryDataPacket.Create("minecraft:chicken_variant",
                "minecraft:cold", "minecraft:temperate", "minecraft:warm"),

            // decorated_pot_pattern (26.3, referenced by item component potPattern).
            // Full 23 entries from data/minecraft/decorated_pot_pattern/.
            RegistryDataPacket.Create("minecraft:decorated_pot_pattern",
                "minecraft:angler", "minecraft:archer", "minecraft:arms_up", "minecraft:blade",
                "minecraft:brewer", "minecraft:burn", "minecraft:danger", "minecraft:explorer",
                "minecraft:flow", "minecraft:friend", "minecraft:guster", "minecraft:heart",
                "minecraft:heartbreak", "minecraft:howl", "minecraft:miner", "minecraft:mourner",
                "minecraft:plenty", "minecraft:prize", "minecraft:scrape", "minecraft:sheaf",
                "minecraft:shelter", "minecraft:skull", "minecraft:snort"),

            // trim_pattern (26.3, referenced by item component trim pattern).
            // Full 18 entries from data/minecraft/trim_pattern/.
            RegistryDataPacket.Create("minecraft:trim_pattern",
                "minecraft:bolt", "minecraft:coast", "minecraft:dune", "minecraft:eye",
                "minecraft:flow", "minecraft:host", "minecraft:raiser", "minecraft:rib",
                "minecraft:sentry", "minecraft:shaper", "minecraft:silence", "minecraft:snout",
                "minecraft:spire", "minecraft:tide", "minecraft:vex", "minecraft:ward",
                "minecraft:wayfinder", "minecraft:wild"),

            // enchantment (26.3, referenced by item enchantment component).
            // Full 43 entries from data/minecraft/enchantment/.
            // Enchantment tags (exclusive_set/*, curse, etc.) come from pack tags.
            RegistryDataPacket.Create("minecraft:enchantment",
                "minecraft:aqua_affinity", "minecraft:bane_of_arthropods", "minecraft:binding_curse",
                "minecraft:blast_protection", "minecraft:breach", "minecraft:channeling",
                "minecraft:density", "minecraft:depth_strider", "minecraft:efficiency",
                "minecraft:feather_falling", "minecraft:fire_aspect", "minecraft:fire_protection",
                "minecraft:flame", "minecraft:fortune", "minecraft:frost_walker", "minecraft:impaling",
                "minecraft:infinity", "minecraft:knockback", "minecraft:looting", "minecraft:loyalty",
                "minecraft:luck_of_the_sea", "minecraft:lunge", "minecraft:lure", "minecraft:mending",
                "minecraft:multishot", "minecraft:piercing", "minecraft:power",
                "minecraft:projectile_protection", "minecraft:protection", "minecraft:punch",
                "minecraft:quick_charge", "minecraft:respiration", "minecraft:riptide",
                "minecraft:sharpness", "minecraft:silk_touch", "minecraft:smite",
                "minecraft:soul_speed", "minecraft:sweeping_edge", "minecraft:swift_sneak",
                "minecraft:thorns", "minecraft:unbreaking", "minecraft:vanishing_curse",
                "minecraft:wind_burst"),
        ];
    }
}
