# Indiana Jones and the Last Crusade EGA conversations

## Implemented export and current scope

The standalone HTML conversation reference is implemented by `tools/Indy3ConversationExport`. Current English comparison resources are in `Games/FUTURE/indy3 ega en`; the default output is `Games/FUTURE/indy3-conversations.html` with `Games/FUTURE/indy3-conversations.graph.json` beside it. Regenerate it through the exporter after changing extraction. Indy interactive gameplay and further conversation expansion are deferred.

The historical construction goal was a standalone, locally openable HTML reference for the multiple-choice conversations in the supplied EGA resources. The export shows source room artwork and choices/replies with verified continuations; unresolved scene associations, conditions and terminal actions stay explicit. Reserve outcome and preferred-route annotations for a later pass; do not infer them now.

## Source and current limits

- Use the supplied EGA `.LFL` resources. The existing `LoomTextExtractor` identifies a choice set from a run of literal `verbOps` labels, then attaches spoken lines in later `isEqual` fall-through ranges for the same verb IDs. Its entries carry room, script, offset, and choice IDs.
- `ConversationId` groups a choice set in one decoded script. It does **not** link a reply to a later choice set, prove runtime order, evaluate conditions, or establish which room a global script actually plays in. Storage order alone is insufficient for a full conversation tree.
- Scripts can be partially decoded or skipped. Room artwork is a stored room background preview, and objects or actors may be absent. Show provenance and clearly label unresolved links, scene association, and incomplete branches rather than guessing.

## Historical construction and review checklist

1. Inventory every detected multi-choice set in the EGA resources. Record its script, room ownership, option labels, attached replies, code offsets, and extraction diagnostics. Audit global-script sets separately for room association.
2. Recover verified edges between successive choice sets from decoded control flow and script calls where possible. Keep unresolved transitions explicit. A tree can repeat a shared node when needed, but retain stable source IDs to avoid false or cyclic paths.
3. Export room backgrounds from the EGA decoder. Where a conversation's room is established, show that room first. Where it is not established, show a clearly identified game-derived scene only if supported; otherwise leave scene unresolved.
4. Generate a standalone HTML with inline data/artwork or local companion assets, a conversation index, keyboard-accessible collapsible choices, speaker labels, and readable source details. Keep outcome and happy-path slots absent or explicitly pending.
5. Validate with representative branching conversations against the EGA scripts, verify counts of exported versus discovered choice sets, check every image/ID reference, run appropriate tests/build, and open the HTML in a browser for visual and interaction review.

## Acceptance boundary

Call the result a **full conversation tree** only if every transition and room association is supported by the game data and the extraction audit accounts for all discovered choice sets. Otherwise describe the actual coverage precisely and retain the unresolved paths in the HTML so they can be completed from source evidence later.

## Historical supplied-EGA baseline audit (2026-09-28)

The Spanish examples, menu counts and test totals below describe that original dataset and checkpoint. They are not a fresh audit of the current English folder.

`tools/Indy3ConversationAudit` can generate an ignored `indy3-conversation-audit.json` output from the supplied EGA files. The existing extractor finds 75 choice groups (71 indexed-script groups, 4 room-script groups), but 9 ranges still have decoder issues and 11 additional `verbOps` labels in choice-bearing scripts are ungrouped. No label-bearing script sits wholly outside those detected groups. The ungrouped labels include seven spoken-choice labels in script 30 and conditional alternatives in scripts 70, 78, and 95. Script 95 has a jump between two labels for one choice slot, demonstrating that consecutive-label runs alone lose dialogue text. Script 30 shows a single selection dispatch for four verb slots even though only its first two labels are grouped.

The 75 groups also contain non-conversation menus: the global verb bar in script 96, travel destinations in rooms 6 and 15, and object/fight action menus. Exclude these by script role and control-flow context, not hard-coded text. Most indexed dialogue scripts have no `RoomId`; a script's containing `.LFL` is not always its play room. For example, script 121 stored in `23.LFL` calls dialogue scripts 70, 71, and 103 stored in `28.LFL`, while Room 45 entry calls script 117 stored in `46.LFL`.

The call inventory has direct room-side chains for several dialogue scripts: Room 23 local 202 calls script 121, which calls 70/71/103; Room 39 object 586 verb `0A` calls 67, which calls 78; Room 72 entry calls 94, which calls 95; Room 39 entry calls 92, which calls 93; Room 31 entry calls 106, which calls 105; Room 23 entry calls 107, which calls 108; and Room 22 entry calls 77, which calls 76. Scripts 29 and 30 use a dynamic dispatcher: Room 1 entry binds `var[0x032]` to script 29 and Room 3 entry binds it to script 30; indexed script 2 executes `startScript var[0x032]`. The room exits clear the binding. Script 86 has no established caller in the decoded paths; its storage alongside Room 32 is archival context, not execution-room proof.

The independent audit's `--verify` mode checks representative room provenance against the supplied data: 29 in Room 1, 30 in Room 3, 78 in Room 39 rather than storage Room 40, 104 on the normal path through Room 31, and 117 in Rooms 45 and 46. Some scripts have multiple possible contexts because conditions and cutscene overrides are not evaluated.

Run the reproducible audit from the repository root with `rtk proxy dotnet run --project tools/Indy3ConversationAudit/Indy3ConversationAudit.csproj -- "Games/FUTURE/indy3 ega en" --verify`. To refresh the full ignored inventory, omit `--verify` and save the JSON output in a fresh ignored verification directory.

For interpreter semantics, compare the decoded instructions with ScummVM's [v5 script engine](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script_v5.cpp) and [resource handling](https://github.com/scummvm/scummvm/blob/master/engines/scumm/resource.cpp). These references help decode script control flow and actor text operands; the supplied EGA `.LFL` files remain the evidence for each dialogue edge and scene.

## Export graph review checklist

The exporter writes `Games/FUTURE/indy3-conversations.graph.json` alongside the offline HTML. The graph records individual option replies, links to later menus, alternative labels for reused verb slots, conditional preludes, scene evidence, and explicit exclusions. Check those fields against source offsets, not just rendered prose. The HTML verifier in `tools/Indy3ConversationExport/verify-html.mjs` checks offline image payloads, internal links, and structure; `tools/Indy3ConversationAudit/inspect_html.py` lists options with neither a spoken reply nor a later menu.

The focused control-flow regression is indexed script 29: menu 1 verb 121 leads to menu 2 only; verb 122 leads to menu 3. Menu 3 verb 120 replies appear in the decoded order `+0x04C6`, `+0x0269`, `+0x057A`. Its introductory lines belong to menu 1 only. Do not mistake shared verb IDs across successive menus for shared handlers.

Review the terminal-option report by source role. Punch and offer-object actions may remain as options in a mixed dialogue, while a travel-destination UI is not a conversation. Room 6's `A casa de Henry`, `En avion a Venecia`, and `Anular` are excluded because its entry script writes `var[0x033]=0x00C8` and starts the travel dispatcher script `0x15`; it does not load a room directly. The six text-labelled options with no reply or next-menu link in script 95's menus 9–15 jump to `+0x1B0A` or `+0x1B13`, then set flags and stop without printing a line. Its first-menu offer-object branch has game-state-dependent paths to later menus. Conditional reachability must be described as such; the graph cannot establish which item or game state the player has.

The 28 September candidate had 75 discovered choice groups: 67 included conversation menus and 8 excluded command/travel/action menus. Its HTML contains 201 options; 17 options have neither a literal spoken reply nor a verified link. All 11 additional labels found outside the extractor's initial grouped runs are represented in the graph's option or alternative-label fields. Empty options can represent script-side action or state updates, and conditional item-dependent branches remain a static overapproximation rather than a runtime transcript. Script 86 has no proven runtime caller; its Room 32 picture is explicitly an archival storage association.

The 28 September HTML verifier reported 91 unique IDs, 79 valid continuation links, 94 embedded images representing 12 distinct room backgrounds, a parsed inline script, and no external asset dependency. Selection slots can be overwritten before later handlers; those downstream replies and links are labelled as possible conditional paths instead of a demonstrated single playthrough.

Historical 28 September repository validation after the reader and fixture fixes: the data-configured test suite passed 219/219 with no skips. The solution build completed with zero errors; three transient DLL copy warnings were retried successfully while the desktop app held files open. Regenerate and verify the HTML separately after any exporter change.
