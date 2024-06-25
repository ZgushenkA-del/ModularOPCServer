sc.exe create "WebOpcServer" binpath="%~dp0OPCWebApi.exe" start=auto
net start "WebOpcServer"