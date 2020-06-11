@echo off

pushd %~dp0

set getSecretsCmd=get-secrets.cmd
if not exist %getSecretsCmd% goto :noSecrets
call get-secrets

set publishedExe=..\bin\Release\netcoreapp3.1\publish\AzCp.exe
if exist %publishedExe% copy %publishedExe% . /y >nul
if not exist AzCp.exe goto :noExe

if not exist upload goto :noUpload

AzCp

pause

popd

exit /b 0

:noExe
echo.Unable to find AzCp executable! >&2
exit /b 1

:noSecrets
echo.Unable to find '%%' >&2
echo.Please create this file to set the connection string to use >&2
echo.e.g. >&2
echo.  set AzCp_ConnectionStrings__StorageConnectionString=DefaultEndpointsProtocol=https;AccountName={account name};AccountKey={key};BlobEndpoint=https://{blob container}.blob.core.windows.net/ >&2
exit /b 1

:noUpload
echo.Unable to find 'upload' folder >&2
echo.Please create this sub-folder and copy files to be uploaded into it >&2
exit /b 1