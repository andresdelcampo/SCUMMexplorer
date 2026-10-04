param([string]$Output='Exe/UiRefinementSession-20261001/audits-1')
$ErrorActionPreference='Stop'
$prior=Get-Content Exe/DraftScopeSession-20261001/final-audits-1/literal/audit.json -Raw | ConvertFrom-Json
$current=Get-Content ($Output+'/literal/audit.json') -Raw | ConvertFrom-Json
$changes=@()
foreach($old in $prior.results) {
    $new=$current.results | Where-Object owner -EQ $old.owner
    if(!$new -or $new.status -ne $old.status -or $new.frames -ne $old.frames -or
        ($new.rooms -join ',') -ne ($old.rooms -join ',') -or ($new.sceneTrace -join '|') -ne ($old.sceneTrace -join '|')) {$changes+=$old.owner}
}
if($current.results.Count -ne 363 -or $changes.Count){throw "Root regression: $($changes -join ', ')"}
$oldSequences=Get-Content Exe/DraftScopeSession-20261001/final-audits-1/sequences.json -Raw | ConvertFrom-Json
$newSequences=Get-Content ($Output+'/sequences.json') -Raw | ConvertFrom-Json
if(($oldSequences | ConvertTo-Json -Depth 100 -Compress) -ne ($newSequences | ConvertTo-Json -Depth 100 -Compress)){throw 'Sequence audit changed.'}
$oldMembership=Get-Content Exe/DraftScopeSession-20261001/final-audits-1/established.json -Raw | ConvertFrom-Json
$newMembership=Get-Content ($Output+'/established.json') -Raw | ConvertFrom-Json
if(($oldMembership | ConvertTo-Json -Depth 100 -Compress) -ne ($newMembership | ConvertTo-Json -Depth 100 -Compress)){throw 'Established membership audit changed.'}
$report=@('363 literal roots: statuses, frame counts, reached rooms and scope boundaries unchanged.')
$report+=$current.results | Group-Object status | ForEach-Object {"$($_.Count): $($_.Name)"}
$report+='Four established memberships and both complete sequence packets unchanged.'
$report+=Get-Content ($Output+'/png-comparison.txt')
$report | Tee-Object -FilePath ($Output+'/comparison.txt')
