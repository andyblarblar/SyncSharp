sc.exe delete "SyncSharp"

 $strServiceName = "SyncSharp"
 $filter = 'Name=' + "'" + $strServiceName + "'" + ''
 $service = Get-WMIObject -ComputerName "." -namespace "root\cimv2" -class Win32_Service -Filter $filter
 $service.StopService()
