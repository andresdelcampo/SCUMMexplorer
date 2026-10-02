"""Enable optional operand bindings in the unchanged production-observer recipe harness."""
from pathlib import Path
import runpy
import sys

out = Path(sys.argv[1])
expanded = '--expanded' in sys.argv[2:]
sys.argv = ['create_audit.py', str(out)]
runpy.run_path('.verification/completion-observer-20261002/create_audit.py', run_name='__main__')
p = out / 'Program.cs'
source = p.read_text(encoding='utf-8')
source = source.replace('observation.PathEstimated,Values=observation.Values,',
                        'observation.PathEstimated,observation.OperandBindings,Values=observation.Values,')
source = source.replace('key => keys.Contains(key)||key.StartsWith("local["));',
                        'key => keys.Contains(key)||key.StartsWith("local[")||key.StartsWith("var[")||'
                        'key.StartsWith("bit[0xD6"), captureOperandBindings:true);')
if expanded:
    source = source.replace('site => site.Script is 10 or 20 or 22 or 23 or 30 or 31 ||',
        'site => site.Context=="Room 01 local script 211" || site.Script is 26 or 90 || '
        'site.Operands?.Contains("var[0x032")==true || site.Operands?.Contains("var[0x03A")==true || '
        'site.Script is 10 or 20 or 22 or 23 or 30 or 31 ||')
p.write_text(source, encoding='utf-8')
print('Pre-instruction bindings enabled; recipe actions unchanged; expanded source sites:', expanded)
