# Actual call boundary phases - 2 October 2026

Continues `f84460f`, preserving `3f32e5f`, `32f1ecb`, `a3a90ec` and every earlier handoff constraint.

Fifteen new cases replay actual executor observations and perturb only copied evidence. Ten failures reproduce before the guard; five controls already pass (`Exe/RuntimeBoundariesSession-20261002/order-before`). After the guard all77 call-focused cases pass in `order-after`.

Initialization requires an aligned begin at offset0 with no stop/resume result. Ordinary calls initialize before host return; a deferred chain requires its observed host return and terminated chaining caller. Foreign host instruction/site/path, begin masquerading as continuation/end, missing or yielding chain caller, late initialization, native/omitted scheduler and cancellation boundaries cannot supply correspondence. The entire observed incoming-dispatch requirement remains unchanged. No bound, sequence, stream field or execution policy changes.

Assignments, child end/yield/restoration, host return, caller continuation/end, restored locals and original writer terminals remain independent. The structural triple proves no successful return, local origin, transported domain/effect or completion. All estimates, historical origins, omitted/external/concurrent/alias/cancellation/replacement/scheduler/callee limits and original bounds remain. Full current-source preservation follows separately. All57 protected files remain hash-identical and unstaged; no App launch or push.

Follow-up review after `4b7c646` adds three child-phase cases: copied begin records could masquerade as both yielded and terminal child ends. `child-phase-before` reproduces both failures and retains one genuine control. Child ends now require end phase and restored locals require begin phase. `child-phase-final` passes80 call-focused cases; in total18 new phase cases reproduce12 failures and retain6 controls. The in-progress second full preservation run was stopped before aggregation for this source correction; it remains dated partial evidence. Fresh final verification uses a third output base and includes the correction.
