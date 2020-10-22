#Service needs absolute path
$binPath = Resolve-Path ".\SyncSharpWorker\SyncSharpWorker.exe"

$params = @{
  Name = "SyncSharp"
  BinaryPathName = $binPath.Path
  DisplayName = "SyncSharp"
  StartupType = "Automatic"
  Description = "Background worker for SyncSharp."
}
New-Service @params