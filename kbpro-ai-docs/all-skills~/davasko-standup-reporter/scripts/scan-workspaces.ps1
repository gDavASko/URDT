param(
    [string]$BaseDir = "e:\UnityProjects\IRI",
    [string]$Author = "DavASko\|gDavASko\|DavASko01"
)

$now = Get-Date
$isMonday = ($now.DayOfWeek -eq [System.DayOfWeek]::Monday)

if ($isMonday) {
    # Пятница 14:00 мск (11:00 UTC)
    $sinceStr = ($now.AddDays(-3).ToString("yyyy-MM-dd")) + " 11:00:00 +0000"
} else {
    # Вчера 14:00 мск (11:00 UTC)
    $sinceStr = ($now.AddDays(-1).ToString("yyyy-MM-dd")) + " 11:00:00 +0000"
}

$untilStr = $now.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss +0000")
$results = @()

Write-Host "Сканирование проектов в $BaseDir с $sinceStr до $untilStr..."

Get-ChildItem -Directory $BaseDir | ForEach-Object {
    $dir = $_.FullName
    $name = $_.Name
    if (Test-Path "$dir\.git") {
        $log = git -C $dir log --since="$sinceStr" --until="$untilStr" --author="$Author" --pretty=format:"%h - %s" --stat 2>$null
        $status = git -C $dir status -s 2>$null
        $hasSubmodules = Test-Path "$dir\.gitmodules"
        
        $submoduleData = ""
        if ($hasSubmodules) {
            $submoduleData = git -C $dir submodule foreach --quiet "
                log=`$(git log --since=`"$sinceStr`" --until=`"$untilStr`" --author=`"$Author`" --pretty=format:`"%h - %s`" --stat 2>/dev/null)
                status=`$(git status -s 2>/dev/null)
                if [ -n `"`$log`" ] || [ -n `"`$status`" ]; then
                    echo `"  [Submodule: `$name]`"
                    if [ -n `"`$log`" ]; then echo `"  COMMITS:`"; echo `"`$log`"; fi
                    if [ -n `"`$status`" ]; then echo `"  UNCOMMITTED:`"; echo `"`$status`"; fi
                fi
            " 2>$null
        }

        if ($log -or $status -or $submoduleData) {
            $projectResult = "=== PROJECT: $name ===`n"
            if ($log) { $projectResult += "COMMITS:`n$log`n" }
            if ($status) { $projectResult += "UNCOMMITTED:`n$status`n" }
            if ($submoduleData) { $projectResult += $submoduleData + "`n" }
            
            $results += $projectResult
        }
    }
}

if ($results.Count -eq 0) {
    Write-Host "Изменений за указанный период не найдено."
} else {
    $results | ForEach-Object { Write-Host $_ }
}
