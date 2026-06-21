$lines = Get-Content 'C:\Users\Laith Al-Shamasneh\.claude\projects\E--Development--NET-Projects-MyMoney\bc39a098-4586-4b2b-9ee1-d3f5db79ff94.jsonl'
$found = $lines | Where-Object { $_ -match 'Financial Calendar System' -and $_ -match 'queue-operation' }
if ($found.Count -gt 0) {
    $obj = $found[0] | ConvertFrom-Json
    $text = $obj.content
    Write-Output "TOTAL_LEN=$($text.Length)"
    Write-Output $text.Substring(4400, [Math]::Min(6500, $text.Length - 4400))
}
