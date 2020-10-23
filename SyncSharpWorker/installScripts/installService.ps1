Function Set-ServiceAcctCreds([string]$strCompName,[string]$strServiceName,[string]$newAcct,[string]$newPass){
  $filter = 'Name=' + "'" + $strServiceName + "'" + ''
  $service = Get-WMIObject -ComputerName $strCompName -namespace "root\cimv2" -class Win32_Service -Filter $filter
  $service.Change($null,$null,$null,$null,$null,$null,$newAcct,$newPass)
  $service.StopService()
  while ($service.Started){
    sleep 2
    $service = Get-WMIObject -ComputerName $strCompName -namespace "root\cimv2" -class Win32_Service -Filter $filter
  }
  $service.StartService()
}

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

$name = Read-Host 'What is your domain username? This is used to allow the service to see network drives. Put .\Username for accounts on this PC'

$pass = Read-Host 'What is your password?'

#run the 
Set-ServiceAcctCreds -strCompName "." -strServiceName "SyncSharp" -newAcct $name -newPass $pass