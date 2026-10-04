$ErrorActionPreference = 'Stop'
Set-Location 'D:\Documents\Personal\Code Projects\SCUMMexplorer'
$report = @('Checkout inventory, 30 September 2026', (git rev-parse HEAD), (git status --short))
$trees = git worktree list --porcelain
$report += $trees
foreach ($row in $trees) {
    if ($row.StartsWith('worktree ')) {
        $path = $row.Substring(9)
        $report += "STATUS $path"
        $report += git -C $path status --short
        $report += 'COMMITS NOT ON MASTER'
        $report += git -C $path log master..HEAD --oneline
    }
}
$report += 'ALL BRANCH COMMITS NOT ON MASTER'
$report += git log master.. --all --oneline
$report += Get-Item Exe/SCUMMexplorer.App/Release/net10.0-windows/SCUMMexplorer.App.exe | Format-List FullName,LastWriteTime,Length | Out-String
$report | Out-File -Encoding utf8 .verification/archive/cleanup-review-20260930/inventory.txt
$report
