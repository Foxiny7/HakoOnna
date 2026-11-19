param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('host','server')]
    [string]$Mode,
    [switch]$Containers
)
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../../..')).ProviderPath
$executable = Join-Path $project 'Builds/PhaseOne/HakoOnna.exe'
$runDirectory = Join-Path (Join-Path $project 'Temp') ('network-' + $Mode + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $runDirectory | Out-Null
$processes = @()
$reports = @()
function Start-Peer([string]$Role, [int]$Index) {
    $report = Join-Path $runDirectory ($Role + $Index + '.json')
    $log = Join-Path $runDirectory ($Role + $Index + '.log')
    $arguments = @('-batchmode', '-nographics', '-hakoMode', $Role,
        '-hakoSmoke', ('"{0}"' -f $report), '-hakoSmokePlayers', '2',
        '-logFile', ('"{0}"' -f $log))
    if ($Containers) { $arguments += '-hakoContainerSmoke' }
    $process = Start-Process -FilePath $executable -ArgumentList $arguments -WindowStyle Hidden -PassThru
    return [pscustomobject]@{ Process = $process; Report = $report; Role = $Role }
}
try {
    $peers = @(Start-Peer $Mode 0)
    Start-Sleep -Seconds 3
    $peers += Start-Peer 'client' 1
    if ($Mode -eq 'server') { $peers += Start-Peer 'client' 2 }
    $processes = @($peers | ForEach-Object Process)
    $deadline = (Get-Date).AddSeconds(70)
    do {
        $complete = @($peers | Where-Object { Test-Path -LiteralPath $_.Report }).Count -eq $peers.Count
        if (!$complete) { Start-Sleep -Seconds 1 }
    } until ($complete -or (Get-Date) -gt $deadline)
    if (!$complete) { throw "Peers did not produce all reports. Logs: $runDirectory" }
    foreach ($peer in $peers) {
        $report = Get-Content -Raw -LiteralPath $peer.Report | ConvertFrom-Json
        if ($report.errors.Count -ne 0) { throw ($report.errors -join [Environment]::NewLine) }
        if ($report.peakPlayers -ne 2) { throw "$($peer.Role): expected two players." }
        if ($Containers) {
            if (!$report.sawOpen -or !$report.sawClose -or !$report.doorClosed -or ($report.actor -and !$report.exchanged)) {
                throw "Container interaction failed on $($peer.Role). See $($peer.Report)"
            }
        } elseif ($report.client -and ($report.localDistance -lt 0.5 -or !$report.jumped -or !$report.tired -or !$report.recovered)) {
            throw "$($peer.Role): jump, movement or stamina check failed. See $($peer.Report)"
        }
        foreach ($player in $report.players) {
            if ($player.serverController -ne $report.server) { throw 'CharacterController authority mismatch.' }
            if (!$Containers -and ($player.position.x -lt 0 -or $player.position.x -gt 10.92 -or
                $player.position.z -lt 0 -or $player.position.z -gt 10.01 -or
                [math]::Abs($player.position.y) -gt 0.2)) {
                throw "Player left the expected room or crossed a floor/wall: $($player | ConvertTo-Json -Compress)"
            }
        }
        $reports += $report
    }
    $authority = @($reports | Where-Object server)[0]
    foreach ($observer in @($reports | Where-Object { !$_.server })) {
        foreach ($player in $observer.players) {
            $expected = @($authority.players | Where-Object owner -eq $player.owner)[0]
            $dx = $expected.position.x - $player.position.x
            $dy = $expected.position.y - $player.position.y
            $dz = $expected.position.z - $player.position.z
            $distance = [math]::Sqrt($dx*$dx + $dy*$dy + $dz*$dz)
            if ($distance -gt 0.15) { throw "Player $($player.owner) observer differs by $distance metres." }
        }
    }
    if ($Containers) { "PASS $Mode with 2 players: container opened, exchanged item, closed on all peers." }
    else { "PASS $Mode with 2 players: owner input, server movement, room collision, replicated positions." }
    "Reports: $runDirectory"
} finally {
    foreach ($process in $processes) {
        if (!$process.HasExited) {
            $null = $process.WaitForExit(10000)
        }
        if (!$process.HasExited) { Stop-Process -Id $process.Id }
    }
}
