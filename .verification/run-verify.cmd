@echo off
cd /d "%~dp0.."
set LOG=.verification\verify.log
echo START %date% %time% > %LOG%
where dotnet >> %LOG% 2>&1
dotnet build SCUMMexplorer.slnx --verbosity quiet -nologo >> %LOG% 2>&1
echo BUILD_EXIT=%ERRORLEVEL% >> %LOG%
set SCUMM_SOUND_TEST_ROOT=%CD%\Games\loom ega en
set SCUMM_INDY3_EGA_TEST_ROOT=%CD%\Games\indy3 ega en
dotnet test tests\SCUMMexplorer.Tests\SCUMMexplorer.Tests.csproj --no-build --verbosity quiet -nologo >> %LOG% 2>&1
echo TEST_EXIT=%ERRORLEVEL% >> %LOG%
if exist .verification\RoomAudit\RoomAudit.csproj dotnet build .verification\RoomAudit\RoomAudit.csproj --verbosity quiet -nologo >> %LOG% 2>&1
if exist Exe\RoomAudit\Debug\net10.0-windows\RoomAudit.dll dotnet Exe\RoomAudit\Debug\net10.0-windows\RoomAudit.dll "%CD%\Games\loom ega en" "%CD%\.verification\room-audit" >> %LOG% 2>&1
echo AUDIT_EXIT=%ERRORLEVEL% >> %LOG%
echo END %date% %time% >> %LOG%
