# Cutscene state audit (11 October 2026)

`dotnet run --project .verification/cutscene-state-audit -c Release -- "Games/loom ega mt32 en" [limit]` (output in `output.txt`, about 8 minutes on 8 workers).

Question: how many scanned cutscenes would play differently if the player had the state their own code reads? For each of the 218 scan groups on the reference English EGA the harness runs the trigger in a fresh session (the base), then again after the supported Game Status edits (staff held, 8 notes, each story bit the scene's own code reads set to 1, all of them, and everything together). Outcome = lines printed + rooms entered. Edits wait until `GameStatus.CanEdit`.

Result:
- 218 groups; **19 begin no cutscene at all from a fresh game** (room entries 2, 10, 13, 15, 17, 22, 23, 24, 25, 28, 34, 36, 37, 47, walks/looks in 25 and 34, uses in 34 and 43).
- **76 of 218 (35%) print different lines or enter different rooms** under at least one variant: 59 under a story bit, 37 under the staff, 30 under 8 notes, 62 under the full set.
- 15 variants could not be applied (editing never became available, for example around Room 34's entry); they are reported as `!ERR` in the output and are not counted as sensitive.

Reading: a fresh-state run is one branch of many for about a third of the scenes. Setting state is mechanically possible (the edits exist and change outcomes), but the audit proves only *that* the outcome changes, not which variant is the story's. Choosing the right variant needs the scene's branch conditions (see R-8.26 to R-8.31 in REQUIREMENTS.md). Not covered: actor placement, costume and room object state, which this harness does not vary.
