# Reads the Windows media session (Spotify, browser, etc.) through WinRT.
# Mode "watch": prints one JSON line whenever what's playing changes. Mode toggle|next|prev: sends that command once.
param([string]$Mode = 'watch')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$asTask = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
  $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'
})[0]
function Await($op, [Type]$type) {
  $task = $asTask.MakeGenericMethod($type).Invoke($null, @($op))
  $task.Wait(-1) | Out-Null
  $task.Result
}
$null = [Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager, Windows.Media.Control, ContentType = WindowsRuntime]
$mgrType = [Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager]
$mgr = Await ($mgrType::RequestAsync()) $mgrType

if ($Mode -ne 'watch') {
  $s = $mgr.GetCurrentSession()
  if ($s) {
    switch ($Mode) {
      'toggle' { $null = Await ($s.TryTogglePlayPauseAsync()) ([bool]) }
      'next' { $null = Await ($s.TrySkipNextAsync()) ([bool]) }
      'prev' { $null = Await ($s.TrySkipPreviousAsync()) ([bool]) }
    }
  }
  exit
}

$propsType = [Windows.Media.Control.GlobalSystemMediaTransportControlsSessionMediaProperties]
$null = [Windows.Storage.Streams.IRandomAccessStreamWithContentType, Windows.Storage.Streams, ContentType = WindowsRuntime]
$streamType = [Windows.Storage.Streams.IRandomAccessStreamWithContentType]

# Album art as a data URL; only fetched when the track changes.
function Get-Art($props) {
  try {
    if (-not $props.Thumbnail) { return '' }
    $ras = Await ($props.Thumbnail.OpenReadAsync()) $streamType
    $ms = New-Object System.IO.MemoryStream
    [System.IO.WindowsRuntimeStreamExtensions]::AsStreamForRead($ras).CopyTo($ms)
    $type = if ($ras.ContentType) { $ras.ContentType } else { 'image/png' }
    return "data:$type;base64," + [Convert]::ToBase64String($ms.ToArray())
  } catch { return '' }
}

$last = ''
$artKey = ''
$art = ''
while ($true) {
  $out = @{ playing = $false; title = ''; artist = ''; app = ''; art = '' }
  try {
    $s = $mgr.GetCurrentSession()
    if ($s) {
      $p = Await ($s.TryGetMediaPropertiesAsync()) $propsType
      $out.title = $p.Title
      $out.artist = $p.Artist
      $out.app = $s.SourceAppUserModelId
      $out.playing = $s.GetPlaybackInfo().PlaybackStatus.ToString() -eq 'Playing'
      $key = "$($p.Title)|$($p.Artist)"
      if ($key -ne $artKey) { $artKey = $key; $art = Get-Art $p }
      $out.art = $art
    }
  } catch {}
  $json = $out | ConvertTo-Json -Compress
  if ($json -ne $last) { [Console]::Out.WriteLine($json); [Console]::Out.Flush(); $last = $json }
  Start-Sleep -Milliseconds 1000
}
