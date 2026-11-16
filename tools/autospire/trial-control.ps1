param(
    [ValidateSet('state','action')][string]$Mode='state',
    [string]$Body='{}',
    [int]$Port=8770
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Net.Http
$handler=New-Object System.Net.Http.HttpClientHandler
$handler.UseProxy=$false
$client=New-Object System.Net.Http.HttpClient($handler)
$client.Timeout=[TimeSpan]::FromSeconds(8)
$watch=[Diagnostics.Stopwatch]::StartNew()
try {
    if($Mode -eq 'action') {
        $content=New-Object System.Net.Http.StringContent($Body,[Text.Encoding]::UTF8,'application/json')
        $response=$client.PostAsync("http://localhost:$Port/action",$content).GetAwaiter().GetResult()
    } else {
        $response=$client.GetAsync("http://localhost:$Port/state").GetAwaiter().GetResult()
    }
    $json=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    $watch.Stop()
    $entry=[ordered]@{utc=[DateTime]::UtcNow.ToString('o');mode=$Mode;body=$Body;status=[int]$response.StatusCode;elapsed_ms=$watch.ElapsedMilliseconds;result=($json|ConvertFrom-Json)} | ConvertTo-Json -Depth 40 -Compress
    [IO.File]::AppendAllText((Join-Path $PSScriptRoot 'live-trial.jsonl'),$entry+[Environment]::NewLine,[Text.Encoding]::UTF8)
    [ordered]@{mode=$Mode;status=[int]$response.StatusCode;elapsed_ms=$watch.ElapsedMilliseconds;result=($json|ConvertFrom-Json)} | ConvertTo-Json -Depth 40 -Compress
} finally { $client.Dispose(); $handler.Dispose() }
