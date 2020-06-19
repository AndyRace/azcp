powershell -file %~dp0..\publish.ps1

pushd %~dp0
    set publishedExe=..\bin\Release\netcoreapp3.1\publish\AzCp.*
    if exist %publishedExe% copy %publishedExe% . /y >nul
popd
