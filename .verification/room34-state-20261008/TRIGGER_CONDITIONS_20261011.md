# Room 34 fortress reveal: what the code requires (11 October 2026, English Roland EGA)

Read from the decoded scripts (`ega-seed-study` modes `ops`, `global`, `objs`; `cutscene-scan-ega local 34 200|entry`, `boxes 34`, `walk 34 ...`). Source facts, not native observations.

**The monitor (Room 34 local 200).** Each frame it reads the ego's walk box. When the box changes to **8** and the box before the change was **6**, and camera x is below 464, it runs the reveal (`cutscene`, DCF = 1, pan to 464, delay 300, pan back to 376). The reveal is a one-shot (DCF).

**When it exists.** The entry script starts local 200 only if D7A (late) = 0, DCF = 0 and D6D (input) = 0, and in that same early branch it **hides box 6** (flags 0x80) and enables 7, 8 and (locked) 9. The late branch does the opposite: box 6 enabled, 7/8/9 hidden, no monitor.

**Box 6** is the vertical spur (246,64)-(246,80) that ends at object 560's walk point (246,64); object 560's Use (verb 0x38) is the exit to Room 46 (the portal). Global 97 (the return from the portal) puts the ego at (246,64), walks it to (246,80) and then hides box 6 again.

**Writers of the preconditions.** Waking Rusty (Script 85 +0x000A/+0x000C) stops local 200 and sets DCF. Entering Room 46 sets D7A. So after the wake and after the portal the monitor cannot run. That leaves the first arrival (from Room 33) and the return from the east (Room 35 puts the ego at object 559's walk point (378,134) and walks it to (330,79) in box 8; the box before is 9) as the only eligible entries; in both, box 6 is hidden.

**Explorer walker, early state, fresh:** west to east the ego reports boxes 1, 2, 4, 7, 8 (box 5 is skipped, box 6 is never entered); a walk to object 560's point stops at (246,82) in box 5. No cutscene begins.

**Reading.** In the early state the ego has no way into box 6, and in the late state the monitor does not exist; the condition "6 then 8" asks for a state this code never produces by walking. Candidates for what could still do it: a script that places the ego in box 6 while the monitor runs (none found: the literal box-6 writers are the entry, local 207/209 and global 97), or the real engine's box bookkeeping differing from the Explorer's (hidden-box handling in the pathing/`adjustXYToBeInBox`, not yet compared). A native trace of the ego's box while walking east from the clearing, or while clicking object 560 in the early state, would separate these.
