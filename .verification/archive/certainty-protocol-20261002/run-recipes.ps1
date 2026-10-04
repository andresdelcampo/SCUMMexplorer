param([string]$Output='Exe/CertaintyProtocolSession-20261002')
$ErrorActionPreference='Stop'
$rtkPath='C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe'
$project="$Output/observer-1/Audit/Audit.csproj"
& $rtkPath proxy dotnet build $project -c Release *> "$Output/observer-build.txt"
if($LASTEXITCODE){throw 'Observed build failed.'}
$recipes=@(
    @('room41.json','room41-1'),
    @('level4-egg-first.json','egg-first-1'),
    @('level4-dye-last.json','dye-last-1'),
    @('preserved-recipes.json','preserved-1')
)
foreach($recipe in $recipes) {
    $folder="$Output/$($recipe[1])"
    if(Test-Path -LiteralPath $folder){throw "Recipe output already exists: $folder"}
    & $rtkPath proxy dotnet run --project $project -c Release --no-build -- Games/loom ".verification/archive/room41-certainty-20261002/$($recipe[0])" $folder *> "$Output/$($recipe[1])-run.txt"
    if($LASTEXITCODE){throw "Recipe failed: $($recipe[0])"}
    Write-Output "Completed $($recipe[1])"
}
