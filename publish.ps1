# e.g. v0.1-0-g6861947-dirty
# git describe --long --tags --dirty --always
# git rev-list HEAD | wc -l
# git rev-parse HEAD
$gitVersion=git describe --long --tags --dirty --always --match 'v[0-9]*\.[0-9]*'

if ($gitVersion -match 'v(\d+\.\d+)-(\d+)-(.*)') {
    $fileVersion = "$($Matches[1]).$($Matches[2])"
    $informationalVersion = "${fileVersion}.$($Matches[3])"
} else {
    $fileVersion = "0.0"
    $informationalVersion = $gitVersion
}

#dotnet restore
dotnet publish /p:DeployOnBuild=true /p:PublishProfile=FolderProfile /p:FileVersion=$fileVersion /p:InformationalVersion=$informationalVersion