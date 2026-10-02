"""Reuse the bounded input recorder in a new task; never rewrite its history."""
from pathlib import Path
base = Path('.verification/room23-input-20261002/Room23InputProbe')
base.mkdir(parents=True, exist_ok=False)
source = Path('.verification/room25-input-20261002/Room25InputProbe')
program = (source/'Program.cs').read_text()
program = program.replace('new[] { "direct", "input" }', 'new[] { "input" }')
program = program.replace('== 25', '== 23').replace('= 25;', '= 23;').replace('= 25)', '= 23)')
begin = program.index('    void Use(')
end = program.index('    string? failure', begin)
program = program[:begin] + program[end:]
begin = program.index('        WalkBox(4,')
end = program.index('\n    }\n    catch', begin)
program = program[:begin] + '''        WalkBox(3, "endCutscene at Room 23 local script 202 +0x01BD");
        WalkBox(2, "endCutscene at Room 23 local script 214 +0x018A");
        WalkBox(6, "endCutscene at Room 23 local script 200 +0x004D");
        WalkBox(9, "endCutscene at Room 23 local script 200 +0x00A8");
        WalkBox(10, "endCutscene at Room 23 local script 200 +0x00EE");
        WalkBox(7, "endCutscene at Room 23 local script 200 +0x0079");''' + program[end:]
(base/'Program.cs').write_text(program,encoding='utf-8')
(base/'Room23InputProbe.csproj').write_bytes((source/'Room25InputProbe.csproj').read_bytes())
