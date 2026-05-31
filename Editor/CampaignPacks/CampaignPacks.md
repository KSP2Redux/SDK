# Campaign Pack Authoring

Campaign Packs are an editor-only prototype for describing exploration-mode content bundles before we wire anything into runtime campaign loading.

The authoring source is a set of Unity `.asset` files. The bake action writes sibling JSON files that are intended to become the runtime or Patch Manager handoff format later.

## Asset Types

`CampaignPack` is the player-facing profile. It has an `id`, localization keys for name and description, an optional `galaxyDefinitionKey`, and optional references to a tech tree set, mission set, and science set.

`TechTreeSet` is a list of tech node IDs.

`MissionSet` is a list of mission IDs.

`ScienceSet` is a list of experiment IDs, science region IDs, and discoverable IDs.

`CampaignPackExtension` lets another asset add or remove content from an existing pack or set without copying the original definition.

## Why It Is Split Up

The goal is to avoid tying planets, missions, tech, and science into one giant definition too early.

A mod can reuse the same galaxy with a different mission set, or add extra missions to an existing campaign pack without duplicating the galaxy definition. A planet pack can ship a default campaign pack, while another mod can target that pack or one of its sets with extra content.

## Effective Content

The editor preview resolves content in this order:

1. Start with the campaign pack's referenced base sets.
2. Apply matching extension additions.
3. Apply matching extension removals.

Removals run last, so if the same ID is both added and removed, the removal wins.

Extensions can be referenced directly by the pack or discovered globally by matching target IDs. The resolver deduplicates the same extension asset if it appears in both paths.

## Editor Workflow

Create individual assets from `Assets/Redux SDK`:

- `New Campaign Pack`
- `New Tech Tree Set`
- `New Mission Set`
- `New Science Set`
- `New Campaign Pack Extension`

For a quick local example, select a project folder and run `Assets/Redux SDK/Create Campaign Pack Example`. This creates one pack, its three content sets, and one extension using known Redux IDs.

Use `Modding/Campaign Packs/Browser` to inspect the pack list, effective content, validation issues, and bake JSON.

Use `Export Localizations` on a `CampaignPack` inspector to export `NameLocKey` and `DescriptionLocKey` through the shared localization export flow. Campaign pack rows default to `campaign_packs_loc.csv`.

## Current Limits

This does not change campaign creation, save data, or runtime filtering yet.

The autocomplete catalog is still heuristic-based. It scans known JSON and authored assets, but it is not a final content database.

The extension ordering is currently stable by extension ID. We still need a design decision on whether final runtime ordering should follow ID, mod load order, dependency order, or an explicit priority field.

Localization export currently covers the campaign pack name and description only. Set IDs and extension IDs are data identifiers, not player-facing localization keys.
