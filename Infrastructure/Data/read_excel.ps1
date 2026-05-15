$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false
$wb = $excel.Workbooks.Open("g:\Graduate_Project_Backend\Infrastructure\Data\Data.xlsx")
$ws = $wb.Sheets.Item(1)
$rows = $ws.UsedRange.Rows.Count
$cols = $ws.UsedRange.Columns.Count
Write-Host "Rows: $rows, Cols: $cols"

for ($r = 1; $r -le [Math]::Min($rows, 105); $r++) {
    $line = ""
    for ($c = 1; $c -le $cols; $c++) {
        $val = $ws.Cells.Item($r, $c).Text
        if ($c -gt 1) { $line += "|||" }
        $line += $val
    }
    Write-Host $line
}

$wb.Close($false)
$excel.Quit()
[System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
