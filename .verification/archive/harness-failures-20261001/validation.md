# Unattended harness failures (1 October)

`Directory.Build.targets` includes `.verification/HarnessFailurePolicy.cs` in verification projects only. Rebuild a harness before using it: already-built historical executables cannot inherit this policy retroactively. App/Core/tests do not include it.

The module initializer registers fatal exception handling before configuring Windows error-reporting flags. Console/main/worker unhandled exceptions and WPF dispatcher exceptions write a full diagnostic, then exit 1 without a crash/debug/close dialog. `SCUMM_HARNESS_FAILURE_LOG` selects an exact diagnostic destination; default is ignored `Exe/HarnessFailures/failure-<pid>.log`. Caught harness failures still follow their harness's ordinary reporting.

Windows flags follow [SetErrorMode](https://learn.microsoft.com/en-us/windows/win32/api/errhandlingapi/nf-errhandlingapi-seterrormode) and [WerSetFlags](https://learn.microsoft.com/en-us/windows/win32/api/werapi/nf-werapi-wersetflags). The first native import mistakenly targeted wer.dll; the probes caught initialization failure. Corrected to Kernel32.dll and registered exception handling first. No machine-wide settings changed.

`powershell -NoProfile -ExecutionPolicy Bypass -File .verification/archive/harness-failures-20261001/run.ps1 -Output Exe/VerificationSession-20261001/harness-failures-run2`: **4/4**, deliberate main-thread, worker-thread, WPF dispatcher and console crashes. Each exits 1 and creates a diagnostic within ten seconds; no user input. Both builds zero warnings/errors. The launcher acquires the process handle before waiting, so the exit code remains available.
