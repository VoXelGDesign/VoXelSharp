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
    public UpdateTagsPacket UpdateTags { get; } = new(
    [
        // Timeline tags (required by 26.3 dimension_type data which references
        // "timelines": "#minecraft:in_overworld" and "default_clock": "minecraft:overworld").
        // The client resolves timeline tags through the "root" tag registry (the earlier crash
        // showed "Unbound tags in registry ResourceKey[minecraft:root / minecraft:timeline]").
        // Entry IDs below are the 0-based indices of entries in the minecraft:timeline registry
        // order we send: 0=day, 1=early_game, 2=moon, 3=villager_schedule.
        // Tag values from vanilla 26.3 data/minecraft/tags/timeline/.
        // Also add infiniburn block tag (referenced by dimension_type's "infiniburn": "#minecraft:infiniburn_overworld").
        new TaggedRegistry("minecraft:timeline",
        [
            // in_overworld = ["#minecraft:universal", "minecraft:day", "minecraft:moon", "minecraft:early_game"]
            new RegistryTag("minecraft:in_overworld", [3, 0, 2, 1]), // universal→villager_schedule(3), day(0), moon(2), early_game(1)
            // in_nether = ["#minecraft:universal"]
            new RegistryTag("minecraft:in_nether", [3]),               // universal→villager_schedule(3)
            // in_end = ["#minecraft:universal"]
            new RegistryTag("minecraft:in_end", [3]),                  // universal→villager_schedule(3)
            // universal = ["minecraft:villager_schedule"]
            new RegistryTag("minecraft:universal", [3]),               // villager_schedule(3)
        ]),
        // Block infiniburn tags referenced by dimension_type's "infiniburn" field.
        // These tags must exist in the block registry even with empty entry lists.
        new TaggedRegistry("minecraft:block",
        [
            new RegistryTag("minecraft:infiniburn_overworld", []),
            new RegistryTag("minecraft:infiniburn_end", []),
            new RegistryTag("minecraft:infiniburn_nether", []),
            // Block tags referenced by enchantment entry NBT (block references in effects).
            new RegistryTag("minecraft:lightning_rods", []),
            new RegistryTag("minecraft:soul_speed_blocks", []),
            new RegistryTag("minecraft:blocks_wind_charge_explosions", []),
        ]),
        // Damage type tags — items reference these during data component building.
        // Empty entry lists (tags exist to resolve references; actual entries defined by
        // the client's built-in damage type tag files as the pack provides them).
        new TaggedRegistry("minecraft:damage_type",
        [
            new RegistryTag("minecraft:always_hurts_ender_dragons", []),
            new RegistryTag("minecraft:always_kills_armor_stands", []),
            new RegistryTag("minecraft:always_most_significant_fall", []),
            new RegistryTag("minecraft:always_triggers_silverfish", []),
            new RegistryTag("minecraft:avoids_guardian_thorns", []),
            new RegistryTag("minecraft:burn_from_stepping", []),
            new RegistryTag("minecraft:burns_armor_stands", []),
            new RegistryTag("minecraft:bypasses_armor", []),
            new RegistryTag("minecraft:bypasses_cooldown", []),
            new RegistryTag("minecraft:bypasses_effects", []),
            new RegistryTag("minecraft:bypasses_enchantments", []),
            new RegistryTag("minecraft:bypasses_invulnerability", []),
            new RegistryTag("minecraft:bypasses_resistance", []),
            new RegistryTag("minecraft:bypasses_shield", []),
            new RegistryTag("minecraft:bypasses_wolf_armor", []),
            new RegistryTag("minecraft:can_break_armor_stand", []),
            new RegistryTag("minecraft:damages_helmet", []),
            new RegistryTag("minecraft:ignites_armor_stands", []),
            new RegistryTag("minecraft:is_drowning", []),
            new RegistryTag("minecraft:is_explosion", []),
            new RegistryTag("minecraft:is_fall", []),
            new RegistryTag("minecraft:is_fire", []),
            new RegistryTag("minecraft:is_freezing", []),
            new RegistryTag("minecraft:is_lightning", []),
            new RegistryTag("minecraft:is_player_attack", []),
            new RegistryTag("minecraft:is_projectile", []),
            new RegistryTag("minecraft:mace_smash", []),
            new RegistryTag("minecraft:no_anger", []),
            new RegistryTag("minecraft:no_impact", []),
            new RegistryTag("minecraft:no_knockback", []),
            new RegistryTag("minecraft:no_wolf_retaliation", []),
            new RegistryTag("minecraft:panic_causes", []),
            new RegistryTag("minecraft:panic_environmental_causes", []),
            new RegistryTag("minecraft:sulfur_cube_with_block_immune_to", []),
            new RegistryTag("minecraft:witch_resistant_to", []),
            new RegistryTag("minecraft:wither_immune_to", []),
        ]),
        // All item tags referenced by registries we send (sulfur_cube_archetype, enchantment, etc.).
        // "Tag data is never sourced from known packs" — must be sent in full via UpdateTags.
        // Empty entry arrays: tags exist (references resolve) but no items assigned (acceptable for M4).
        new TaggedRegistry("minecraft:item",
        [
            // Sulfur cube archetype item tags (referenced by each entry's "items" field).
            new RegistryTag("minecraft:sulfur_cube_archetype/bouncy", []),
            new RegistryTag("minecraft:sulfur_cube_archetype/explosive", []),
            new RegistryTag("minecraft:sulfur_cube_archetype/fast_flat", []),
            new RegistryTag("minecraft:sulfur_cube_archetype/fast_sliding", []),
            new RegistryTag("minecraft:sulfur_cube_archetype/high_resistance", []),
            new RegistryTag("minecraft:sulfur_cube_archetype/hot", []),
            new RegistryTag("minecraft:sulfur_cube_archetype/light", []),
            new RegistryTag("minecraft:sulfur_cube_archetype/regular", []),
            new RegistryTag("minecraft:sulfur_cube_archetype/slow_bouncy", []),
            new RegistryTag("minecraft:sulfur_cube_archetype/slow_flat", []),
            new RegistryTag("minecraft:sulfur_cube_archetype/slow_sliding", []),
            new RegistryTag("minecraft:sulfur_cube_archetype/sticky", []),
            // Enchantable tags (referenced by enchantment entry NBT supported_items/primary_items).
            new RegistryTag("minecraft:enchantable/armor", []),
            new RegistryTag("minecraft:enchantable/bow", []),
            new RegistryTag("minecraft:enchantable/chest_armor", []),
            new RegistryTag("minecraft:enchantable/crossbow", []),
            new RegistryTag("minecraft:enchantable/durability", []),
            new RegistryTag("minecraft:enchantable/equippable", []),
            new RegistryTag("minecraft:enchantable/fire_aspect", []),
            new RegistryTag("minecraft:enchantable/fishing", []),
            new RegistryTag("minecraft:enchantable/foot_armor", []),
            new RegistryTag("minecraft:enchantable/head_armor", []),
            new RegistryTag("minecraft:enchantable/leg_armor", []),
            new RegistryTag("minecraft:enchantable/lunge", []),
            new RegistryTag("minecraft:enchantable/mace", []),
            new RegistryTag("minecraft:enchantable/melee_weapon", []),
            new RegistryTag("minecraft:enchantable/mining", []),
            new RegistryTag("minecraft:enchantable/mining_loot", []),
            new RegistryTag("minecraft:enchantable/sharp_weapon", []),
            new RegistryTag("minecraft:enchantable/sweeping", []),
            new RegistryTag("minecraft:enchantable/trident", []),
            new RegistryTag("minecraft:enchantable/vanishing", []),
            new RegistryTag("minecraft:enchantable/weapon", []),
            // Decorative/crafting tags (referenced by item component init).
            new RegistryTag("minecraft:decorated_pot_sherds", []),
            new RegistryTag("minecraft:decorated_pot_ingredients", []),
            new RegistryTag("minecraft:trim_materials", []),
            new RegistryTag("minecraft:trimmable_armor", []),
        ]),
        // Banner pattern tags from jar data/minecraft/tags/banner_pattern/.
        // no_item_required lists indices of 32 patterns that need no craftable item.
        // pattern_item/<name> tags list item entry IDs; we send empty (client loads data from pack).
        new TaggedRegistry("minecraft:banner_pattern",
        [
            // 32 patterns that require no craftable item (indices into our 43-entry alphabetical list).
            new RegistryTag("minecraft:no_item_required", [
                26, 27, 28, 29, 31, 38, 35, 37, 32, 36, 34, 33, 25, 5, 30,
                39, 40, 41, 42, 7, 10, 9, 8, 3, 23, 19, 17, 20, 18, 1, 14, 15
            ]),
            // 11 patterns that resolve from pack data (tags must exist for item component init).
            new RegistryTag("minecraft:pattern_item/bordure_indented", []),
            new RegistryTag("minecraft:pattern_item/creeper", []),
            new RegistryTag("minecraft:pattern_item/field_masoned", []),
            new RegistryTag("minecraft:pattern_item/flow", []),
            new RegistryTag("minecraft:pattern_item/flower", []),
            new RegistryTag("minecraft:pattern_item/globe", []),
            new RegistryTag("minecraft:pattern_item/guster", []),
            new RegistryTag("minecraft:pattern_item/mojang", []),
            new RegistryTag("minecraft:pattern_item/piglin", []),
            new RegistryTag("minecraft:pattern_item/skull", []),
        ]),
        // Enchantment tags — required for enchantment NBT tag references to resolve.
        // Entry indices follow our 43-entry alphabetical order (0 = aqua_affinity, ... 42 = wind_burst).
        new TaggedRegistry("minecraft:enchantment",
        [
            new RegistryTag("minecraft:curse", [2, 41]),
            new RegistryTag("minecraft:double_trade_price", [2, 41, 38, 36, 14, 23, 42]),
            new RegistryTag("minecraft:exclusive_set/armor", [28, 3, 11, 27]),
            new RegistryTag("minecraft:exclusive_set/boots", [14, 7]),
            new RegistryTag("minecraft:exclusive_set/bow", [16, 23]),
            new RegistryTag("minecraft:exclusive_set/crossbow", [24, 25]),
            new RegistryTag("minecraft:exclusive_set/damage", [33, 35, 1, 15, 6, 4]),
            new RegistryTag("minecraft:exclusive_set/mining", [13, 34]),
            new RegistryTag("minecraft:exclusive_set/riptide", [19, 5]),
            new RegistryTag("minecraft:in_enchanting_table",
                [28, 11, 9, 3, 27, 31, 0, 39, 7, 33, 35, 1, 17, 10, 18, 37,
                 8, 34, 40, 13, 26, 29, 12, 16, 20, 22, 19, 15, 32, 5, 24, 30, 25, 6, 4, 21]),
            new RegistryTag("minecraft:non_treasure",
                [28, 11, 9, 3, 27, 31, 0, 39, 7, 33, 35, 1, 17, 10, 18, 37,
                 8, 34, 40, 13, 26, 29, 12, 16, 20, 22, 19, 15, 32, 5, 24, 30, 25, 6, 4, 21]),
            new RegistryTag("minecraft:on_mob_spawn_equipment",
                [28, 11, 9, 3, 27, 31, 0, 39, 7, 33, 35, 1, 17, 10, 18, 37,
                 8, 34, 40, 13, 26, 29, 12, 16, 20, 22, 19, 15, 32, 5, 24, 30, 25, 6, 4, 21]),
            new RegistryTag("minecraft:on_random_loot",
                [28, 11, 9, 3, 27, 31, 0, 39, 7, 33, 35, 1, 17, 10, 18, 37, 8,
                 34, 40, 13, 26, 29, 12, 16, 20, 22, 19, 15, 32, 5, 24, 30, 25, 6, 4, 21,
                 2, 41, 14, 23]),
            new RegistryTag("minecraft:on_traded_equipment",
                [28, 11, 9, 3, 27, 31, 0, 39, 7, 33, 35, 1, 17, 10, 18, 37,
                 8, 34, 40, 13, 26, 29, 12, 16, 20, 22, 19, 15, 32, 5, 24, 30, 25, 6, 4, 21]),
            new RegistryTag("minecraft:prevents_bee_spawns_when_mining", [34]),
            new RegistryTag("minecraft:prevents_decorated_pot_shattering", [34]),
            new RegistryTag("minecraft:prevents_ice_melting", [34]),
            new RegistryTag("minecraft:prevents_infested_spawns", [34]),
            new RegistryTag("minecraft:smelts_loot", [10]),
            new RegistryTag("minecraft:tooltip_order",
                [2, 41, 32, 5, 42, 14, 21, 33, 35, 1, 15, 26, 6, 4, 25, 37, 24, 10,
                 12, 17, 29, 28, 3, 11, 27, 9, 13, 18, 34, 20, 8, 30, 22, 31, 0, 36, 38, 7,
                 39, 19, 40, 16, 23]),
            new RegistryTag("minecraft:tradeable",
                [28, 11, 9, 3, 27, 31, 0, 39, 7, 33, 35, 1, 17, 10, 18, 37, 8,
                 34, 40, 13, 26, 29, 12, 16, 20, 22, 19, 15, 32, 5, 24, 30, 25, 6, 4, 21,
                 2, 41, 14, 23]),
            new RegistryTag("minecraft:treasure", [2, 41, 38, 36, 14, 23, 42]),
        ]),
        // Entity type tags referenced by enchantment NBT (entity_type predicates).
        // Empty entry lists — tags exist so NBT parsing succeeds; no entity types assigned.
        new TaggedRegistry("minecraft:entity_type",
        [
            new RegistryTag("minecraft:arrows", []),
            new RegistryTag("minecraft:sensitive_to_bane_of_arthropods", []),
            new RegistryTag("minecraft:sensitive_to_impaling", []),
            new RegistryTag("minecraft:sensitive_to_smite", []),
        ]),
    ]);

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
                // 43 actual 26.3 banner patterns extracted from the vanilla jar.
                // Verbatim alphabetical order from data/minecraft/banner_pattern/ (Mojang data
                // generator) matching the client's minecraft:core known pack.
                new RegistryDataEntry("minecraft:base", null),
                new RegistryDataEntry("minecraft:border", null),
                new RegistryDataEntry("minecraft:bricks", null),
                new RegistryDataEntry("minecraft:circle", null),
                new RegistryDataEntry("minecraft:creeper", null),
                new RegistryDataEntry("minecraft:cross", null),
                new RegistryDataEntry("minecraft:curly_border", null),
                new RegistryDataEntry("minecraft:diagonal_left", null),
                new RegistryDataEntry("minecraft:diagonal_right", null),
                new RegistryDataEntry("minecraft:diagonal_up_left", null),
                new RegistryDataEntry("minecraft:diagonal_up_right", null),
                new RegistryDataEntry("minecraft:flow", null),
                new RegistryDataEntry("minecraft:flower", null),
                new RegistryDataEntry("minecraft:globe", null),
                new RegistryDataEntry("minecraft:gradient", null),
                new RegistryDataEntry("minecraft:gradient_up", null),
                new RegistryDataEntry("minecraft:guster", null),
                new RegistryDataEntry("minecraft:half_horizontal", null),
                new RegistryDataEntry("minecraft:half_horizontal_bottom", null),
                new RegistryDataEntry("minecraft:half_vertical", null),
                new RegistryDataEntry("minecraft:half_vertical_right", null),
                new RegistryDataEntry("minecraft:mojang", null),
                new RegistryDataEntry("minecraft:piglin", null),
                new RegistryDataEntry("minecraft:rhombus", null),
                new RegistryDataEntry("minecraft:skull", null),
                new RegistryDataEntry("minecraft:small_stripes", null),
                new RegistryDataEntry("minecraft:square_bottom_left", null),
                new RegistryDataEntry("minecraft:square_bottom_right", null),
                new RegistryDataEntry("minecraft:square_top_left", null),
                new RegistryDataEntry("minecraft:square_top_right", null),
                new RegistryDataEntry("minecraft:straight_cross", null),
                new RegistryDataEntry("minecraft:stripe_bottom", null),
                new RegistryDataEntry("minecraft:stripe_center", null),
                new RegistryDataEntry("minecraft:stripe_downleft", null),
                new RegistryDataEntry("minecraft:stripe_downright", null),
                new RegistryDataEntry("minecraft:stripe_left", null),
                new RegistryDataEntry("minecraft:stripe_middle", null),
                new RegistryDataEntry("minecraft:stripe_right", null),
                new RegistryDataEntry("minecraft:stripe_top", null),
                new RegistryDataEntry("minecraft:triangle_bottom", null),
                new RegistryDataEntry("minecraft:triangle_top", null),
                new RegistryDataEntry("minecraft:triangles_bottom", null),
                new RegistryDataEntry("minecraft:triangles_top", null),
            ]),
            new RegistryDataPacket("minecraft:damage_type",
            [
                // Full 51 damage_type entries from vanilla 26.3 jar (data/minecraft/damage_type/).
                new RegistryDataEntry("minecraft:arrow", null),
                new RegistryDataEntry("minecraft:bad_respawn_point", null),
                new RegistryDataEntry("minecraft:cactus", null),
                new RegistryDataEntry("minecraft:campfire", null),
                new RegistryDataEntry("minecraft:cramming", null),
                new RegistryDataEntry("minecraft:dragon_breath", null),
                new RegistryDataEntry("minecraft:drown", null),
                new RegistryDataEntry("minecraft:dry_out", null),
                new RegistryDataEntry("minecraft:ender_pearl", null),
                new RegistryDataEntry("minecraft:explosion", null),
                new RegistryDataEntry("minecraft:fall", null),
                new RegistryDataEntry("minecraft:falling_anvil", null),
                new RegistryDataEntry("minecraft:falling_block", null),
                new RegistryDataEntry("minecraft:falling_stalactite", null),
                new RegistryDataEntry("minecraft:fireball", null),
                new RegistryDataEntry("minecraft:fireworks", null),
                new RegistryDataEntry("minecraft:fly_into_wall", null),
                new RegistryDataEntry("minecraft:freeze", null),
                new RegistryDataEntry("minecraft:generic", null),
                new RegistryDataEntry("minecraft:generic_kill", null),
                new RegistryDataEntry("minecraft:hot_floor", null),
                new RegistryDataEntry("minecraft:in_fire", null),
                new RegistryDataEntry("minecraft:in_wall", null),
                new RegistryDataEntry("minecraft:indirect_magic", null),
                new RegistryDataEntry("minecraft:lava", null),
                new RegistryDataEntry("minecraft:lightning_bolt", null),
                new RegistryDataEntry("minecraft:mace_smash", null),
                new RegistryDataEntry("minecraft:magic", null),
                new RegistryDataEntry("minecraft:mob_attack", null),
                new RegistryDataEntry("minecraft:mob_attack_no_aggro", null),
                new RegistryDataEntry("minecraft:mob_projectile", null),
                new RegistryDataEntry("minecraft:on_fire", null),
                new RegistryDataEntry("minecraft:out_of_world", null),
                new RegistryDataEntry("minecraft:outside_border", null),
                new RegistryDataEntry("minecraft:player_attack", null),
                new RegistryDataEntry("minecraft:player_explosion", null),
                new RegistryDataEntry("minecraft:sonic_boom", null),
                new RegistryDataEntry("minecraft:spear", null),
                new RegistryDataEntry("minecraft:spit", null),
                new RegistryDataEntry("minecraft:stalagmite", null),
                new RegistryDataEntry("minecraft:starve", null),
                new RegistryDataEntry("minecraft:sting", null),
                new RegistryDataEntry("minecraft:sulfur_cube_hot", null),
                new RegistryDataEntry("minecraft:sweet_berry_bush", null),
                new RegistryDataEntry("minecraft:thorns", null),
                new RegistryDataEntry("minecraft:thrown", null),
                new RegistryDataEntry("minecraft:trident", null),
                new RegistryDataEntry("minecraft:unattributed_fireball", null),
                new RegistryDataEntry("minecraft:wind_charge", null),
                new RegistryDataEntry("minecraft:wither", null),
                new RegistryDataEntry("minecraft:wither_skull", null),
            ]),
            // Timeline and world_clock registries (new in 26.3): the client's own pack
            // provides the NBT for these entries (Data omitted). Entry order defines the
            // numeric IDs used by timeline tags. Order matches the jar's alphabetical sort.
            new RegistryDataPacket("minecraft:timeline",
            [
                new RegistryDataEntry("minecraft:day", null),           // id 0
                new RegistryDataEntry("minecraft:early_game", null),    // id 1
                new RegistryDataEntry("minecraft:moon", null),          // id 2
                new RegistryDataEntry("minecraft:villager_schedule", null), // id 3
            ]),
            new RegistryDataPacket("minecraft:world_clock",
            [
                new RegistryDataEntry("minecraft:overworld", null),     // id 0
                new RegistryDataEntry("minecraft:the_end", null),       // id 1
            ]),
            // worldgen/block_state_provider_type — referenced by dimension_type's
            // monster_spawn_light_level.type = "minecraft:uniform".
            // We add it with Data null (sourced from pack). ID order: uniform.
            // root's own data or entries.  Uses empty entry list so the pack fills the actual sub-entries.
            new RegistryDataPacket("minecraft:worldgen/block_state_provider_type",
            [
                new RegistryDataEntry("minecraft:uniform", null),
            ]),
            // Variant registries — required by 26.3 to be non-empty.
            // Entries from vanilla jar data/minecraft/<registry>/*.json; Data omitted (pack).
            new RegistryDataPacket("minecraft:cat_variant",
                [new RegistryDataEntry("minecraft:all_black", null), new RegistryDataEntry("minecraft:black", null),
                 new RegistryDataEntry("minecraft:british_shorthair", null), new RegistryDataEntry("minecraft:calico", null),
                 new RegistryDataEntry("minecraft:jellie", null), new RegistryDataEntry("minecraft:persian", null),
                 new RegistryDataEntry("minecraft:ragdoll", null), new RegistryDataEntry("minecraft:red", null),
                 new RegistryDataEntry("minecraft:siamese", null), new RegistryDataEntry("minecraft:tabby", null),
                 new RegistryDataEntry("minecraft:white", null)]),
            new RegistryDataPacket("minecraft:cat_sound_variant",
                [new RegistryDataEntry("minecraft:classic", null), new RegistryDataEntry("minecraft:royal", null)]),
            new RegistryDataPacket("minecraft:chicken_sound_variant",
                [new RegistryDataEntry("minecraft:classic", null), new RegistryDataEntry("minecraft:picky", null)]),
            new RegistryDataPacket("minecraft:cow_sound_variant",
                [new RegistryDataEntry("minecraft:classic", null), new RegistryDataEntry("minecraft:moody", null)]),
            new RegistryDataPacket("minecraft:cow_variant",
                [new RegistryDataEntry("minecraft:cold", null), new RegistryDataEntry("minecraft:temperate", null),
                 new RegistryDataEntry("minecraft:warm", null)]),
            new RegistryDataPacket("minecraft:frog_variant",
                [new RegistryDataEntry("minecraft:cold", null), new RegistryDataEntry("minecraft:temperate", null),
                 new RegistryDataEntry("minecraft:warm", null)]),
            new RegistryDataPacket("minecraft:painting_variant",
                [new RegistryDataEntry("minecraft:alban", null), new RegistryDataEntry("minecraft:aztec", null),
                 new RegistryDataEntry("minecraft:aztec2", null), new RegistryDataEntry("minecraft:backyard", null),
                 new RegistryDataEntry("minecraft:baroque", null), new RegistryDataEntry("minecraft:bomb", null),
                 new RegistryDataEntry("minecraft:bouquet", null), new RegistryDataEntry("minecraft:burning_skull", null),
                 new RegistryDataEntry("minecraft:bust", null), new RegistryDataEntry("minecraft:cavebird", null),
                 new RegistryDataEntry("minecraft:changing", null), new RegistryDataEntry("minecraft:cotan", null),
                 new RegistryDataEntry("minecraft:courbet", null), new RegistryDataEntry("minecraft:creebet", null),
                 new RegistryDataEntry("minecraft:dennis", null), new RegistryDataEntry("minecraft:donkey_kong", null),
                 new RegistryDataEntry("minecraft:earth", null), new RegistryDataEntry("minecraft:endboss", null),
                 new RegistryDataEntry("minecraft:fern", null), new RegistryDataEntry("minecraft:fighters", null),
                 new RegistryDataEntry("minecraft:finding", null), new RegistryDataEntry("minecraft:fire", null),
                 new RegistryDataEntry("minecraft:graham", null), new RegistryDataEntry("minecraft:humble", null),
                 new RegistryDataEntry("minecraft:kebab", null), new RegistryDataEntry("minecraft:lowmist", null),
                 new RegistryDataEntry("minecraft:match", null), new RegistryDataEntry("minecraft:meditative", null),
                 new RegistryDataEntry("minecraft:orb", null), new RegistryDataEntry("minecraft:owlemons", null),
                 new RegistryDataEntry("minecraft:passage", null), new RegistryDataEntry("minecraft:pigscene", null),
                 new RegistryDataEntry("minecraft:plant", null), new RegistryDataEntry("minecraft:pointer", null),
                 new RegistryDataEntry("minecraft:pond", null), new RegistryDataEntry("minecraft:pool", null),
                 new RegistryDataEntry("minecraft:prairie_ride", null), new RegistryDataEntry("minecraft:sea", null),
                 new RegistryDataEntry("minecraft:skeleton", null), new RegistryDataEntry("minecraft:skull_and_roses", null),
                 new RegistryDataEntry("minecraft:stage", null), new RegistryDataEntry("minecraft:sunflowers", null),
                 new RegistryDataEntry("minecraft:sunset", null), new RegistryDataEntry("minecraft:tides", null),
                 new RegistryDataEntry("minecraft:unpacked", null), new RegistryDataEntry("minecraft:void", null),
                 new RegistryDataEntry("minecraft:wanderer", null), new RegistryDataEntry("minecraft:wasteland", null),
                 new RegistryDataEntry("minecraft:water", null), new RegistryDataEntry("minecraft:wind", null),
                 new RegistryDataEntry("minecraft:wither", null)]),
            new RegistryDataPacket("minecraft:pig_sound_variant",
                [new RegistryDataEntry("minecraft:big", null), new RegistryDataEntry("minecraft:classic", null),
                 new RegistryDataEntry("minecraft:mini", null)]),
            new RegistryDataPacket("minecraft:pig_variant",
                [new RegistryDataEntry("minecraft:cold", null), new RegistryDataEntry("minecraft:temperate", null),
                 new RegistryDataEntry("minecraft:warm", null)]),
            new RegistryDataPacket("minecraft:wolf_sound_variant",
                [new RegistryDataEntry("minecraft:angry", null), new RegistryDataEntry("minecraft:big", null),
                 new RegistryDataEntry("minecraft:classic", null), new RegistryDataEntry("minecraft:cute", null),
                 new RegistryDataEntry("minecraft:grumpy", null), new RegistryDataEntry("minecraft:puglin", null),
                 new RegistryDataEntry("minecraft:sad", null)]),
            new RegistryDataPacket("minecraft:wolf_variant",
                [new RegistryDataEntry("minecraft:ashen", null), new RegistryDataEntry("minecraft:black", null),
                 new RegistryDataEntry("minecraft:chestnut", null), new RegistryDataEntry("minecraft:pale", null),
                 new RegistryDataEntry("minecraft:rusty", null), new RegistryDataEntry("minecraft:snowy", null),
                 new RegistryDataEntry("minecraft:spotted", null), new RegistryDataEntry("minecraft:striped", null),
                 new RegistryDataEntry("minecraft:woods", null)]),
            new RegistryDataPacket("minecraft:zombie_nautilus_variant",
                [new RegistryDataEntry("minecraft:temperate", null), new RegistryDataEntry("minecraft:warm", null)]),
            // Block transformer (item tool transformers referenced by item properties).
            new RegistryDataPacket("minecraft:block_transformer",
                [new RegistryDataEntry("minecraft:shovel", null), new RegistryDataEntry("minecraft:axe", null),
                 new RegistryDataEntry("minecraft:hoe", null)]),
            // Sulfur cube archetype (required synchronized registry, full 26.3 entries).
            new RegistryDataPacket("minecraft:sulfur_cube_archetype",
            [
                new RegistryDataEntry("minecraft:bouncy", null),
                new RegistryDataEntry("minecraft:explosive", null),
                new RegistryDataEntry("minecraft:fast_flat", null),
                new RegistryDataEntry("minecraft:fast_sliding", null),
                new RegistryDataEntry("minecraft:high_resistance", null),
                new RegistryDataEntry("minecraft:hot", null),
                new RegistryDataEntry("minecraft:light", null),
                new RegistryDataEntry("minecraft:regular", null),
                new RegistryDataEntry("minecraft:slow_bouncy", null),
                new RegistryDataEntry("minecraft:slow_flat", null),
                new RegistryDataEntry("minecraft:slow_sliding", null),
                new RegistryDataEntry("minecraft:sticky", null),
            ]),
            new RegistryDataPacket("minecraft:dimension_type",
            [
                new RegistryDataEntry("minecraft:overworld", null),
                new RegistryDataEntry("minecraft:overworld_caves", null),
                new RegistryDataEntry("minecraft:the_nether", null),
                new RegistryDataEntry("minecraft:the_end", null),
            ]),
            new RegistryDataPacket("minecraft:instrument",
            [
                // Full 26.3 instrument entries from data/minecraft/instrument/.
                new RegistryDataEntry("minecraft:admire_goat_horn", null),
                new RegistryDataEntry("minecraft:call_goat_horn", null),
                new RegistryDataEntry("minecraft:dream_goat_horn", null),
                new RegistryDataEntry("minecraft:feel_goat_horn", null),
                new RegistryDataEntry("minecraft:ponder_goat_horn", null),
                new RegistryDataEntry("minecraft:seek_goat_horn", null),
                new RegistryDataEntry("minecraft:sing_goat_horn", null),
                new RegistryDataEntry("minecraft:yearn_goat_horn", null),
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
            // decorated_pot_pattern (26.3, referenced by item component potPattern).
            // Full 23 entries from data/minecraft/decorated_pot_pattern/.
            new RegistryDataPacket("minecraft:decorated_pot_pattern",
            [
                new RegistryDataEntry("minecraft:angler", null),
                new RegistryDataEntry("minecraft:archer", null),
                new RegistryDataEntry("minecraft:arms_up", null),
                new RegistryDataEntry("minecraft:blade", null),
                new RegistryDataEntry("minecraft:brewer", null),
                new RegistryDataEntry("minecraft:burn", null),
                new RegistryDataEntry("minecraft:danger", null),
                new RegistryDataEntry("minecraft:explorer", null),
                new RegistryDataEntry("minecraft:flow", null),
                new RegistryDataEntry("minecraft:friend", null),
                new RegistryDataEntry("minecraft:guster", null),
                new RegistryDataEntry("minecraft:heart", null),
                new RegistryDataEntry("minecraft:heartbreak", null),
                new RegistryDataEntry("minecraft:howl", null),
                new RegistryDataEntry("minecraft:miner", null),
                new RegistryDataEntry("minecraft:mourner", null),
                new RegistryDataEntry("minecraft:plenty", null),
                new RegistryDataEntry("minecraft:prize", null),
                new RegistryDataEntry("minecraft:scrape", null),
                new RegistryDataEntry("minecraft:sheaf", null),
                new RegistryDataEntry("minecraft:shelter", null),
                new RegistryDataEntry("minecraft:skull", null),
                new RegistryDataEntry("minecraft:snort", null),
            ]),
            // trim_pattern (26.3, referenced by item component trim pattern).
            // Full 18 entries from data/minecraft/trim_pattern/.
            new RegistryDataPacket("minecraft:trim_pattern",
            [
                new RegistryDataEntry("minecraft:bolt", null),
                new RegistryDataEntry("minecraft:coast", null),
                new RegistryDataEntry("minecraft:dune", null),
                new RegistryDataEntry("minecraft:eye", null),
                new RegistryDataEntry("minecraft:flow", null),
                new RegistryDataEntry("minecraft:host", null),
                new RegistryDataEntry("minecraft:raiser", null),
                new RegistryDataEntry("minecraft:rib", null),
                new RegistryDataEntry("minecraft:sentry", null),
                new RegistryDataEntry("minecraft:shaper", null),
                new RegistryDataEntry("minecraft:silence", null),
                new RegistryDataEntry("minecraft:snout", null),
                new RegistryDataEntry("minecraft:spire", null),
                new RegistryDataEntry("minecraft:tide", null),
                new RegistryDataEntry("minecraft:vex", null),
                new RegistryDataEntry("minecraft:ward", null),
                new RegistryDataEntry("minecraft:wayfinder", null),
                new RegistryDataEntry("minecraft:wild", null),
            ]),
            // enchantment (26.3, referenced by item enchantment component).
            // Full 43 entries from data/minecraft/enchantment/.
            // Enchantment tags (exclusive_set/*, curse, etc.) come from pack tags.
            new RegistryDataPacket("minecraft:enchantment",
            [
                new RegistryDataEntry("minecraft:aqua_affinity", null),
                new RegistryDataEntry("minecraft:bane_of_arthropods", null),
                new RegistryDataEntry("minecraft:binding_curse", null),
                new RegistryDataEntry("minecraft:blast_protection", null),
                new RegistryDataEntry("minecraft:breach", null),
                new RegistryDataEntry("minecraft:channeling", null),
                new RegistryDataEntry("minecraft:density", null),
                new RegistryDataEntry("minecraft:depth_strider", null),
                new RegistryDataEntry("minecraft:efficiency", null),
                new RegistryDataEntry("minecraft:feather_falling", null),
                new RegistryDataEntry("minecraft:fire_aspect", null),
                new RegistryDataEntry("minecraft:fire_protection", null),
                new RegistryDataEntry("minecraft:flame", null),
                new RegistryDataEntry("minecraft:fortune", null),
                new RegistryDataEntry("minecraft:frost_walker", null),
                new RegistryDataEntry("minecraft:impaling", null),
                new RegistryDataEntry("minecraft:infinity", null),
                new RegistryDataEntry("minecraft:knockback", null),
                new RegistryDataEntry("minecraft:looting", null),
                new RegistryDataEntry("minecraft:loyalty", null),
                new RegistryDataEntry("minecraft:luck_of_the_sea", null),
                new RegistryDataEntry("minecraft:lunge", null),
                new RegistryDataEntry("minecraft:lure", null),
                new RegistryDataEntry("minecraft:mending", null),
                new RegistryDataEntry("minecraft:multishot", null),
                new RegistryDataEntry("minecraft:piercing", null),
                new RegistryDataEntry("minecraft:power", null),
                new RegistryDataEntry("minecraft:projectile_protection", null),
                new RegistryDataEntry("minecraft:protection", null),
                new RegistryDataEntry("minecraft:punch", null),
                new RegistryDataEntry("minecraft:quick_charge", null),
                new RegistryDataEntry("minecraft:respiration", null),
                new RegistryDataEntry("minecraft:riptide", null),
                new RegistryDataEntry("minecraft:sharpness", null),
                new RegistryDataEntry("minecraft:silk_touch", null),
                new RegistryDataEntry("minecraft:smite", null),
                new RegistryDataEntry("minecraft:soul_speed", null),
                new RegistryDataEntry("minecraft:sweeping_edge", null),
                new RegistryDataEntry("minecraft:swift_sneak", null),
                new RegistryDataEntry("minecraft:thorns", null),
                new RegistryDataEntry("minecraft:unbreaking", null),
                new RegistryDataEntry("minecraft:vanishing_curse", null),
                new RegistryDataEntry("minecraft:wind_burst", null),
            ]),
            // Timeline and world_clock registries (new in 26.3): required because
            // dimension_type entry NBT references timelines and default_clock fields that
            // bind to these registries. Data omitted: sourced from the minecraft:core pack.
            // old timeline with entries block - REMOVED (previously handled by empty-list version above)
            // old world_clock with entries block - REMOVED
        ];
    }
}
