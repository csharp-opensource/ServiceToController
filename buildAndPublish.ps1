#Get Path to csproj
$path = ".\ServiceToController.csproj"

#Read csproj (XML)
$xml = [xml](Get-Content $path)

#Split the Version Numbers
$avMajor, $avMinor, $avBuild  = $xml.Project.PropertyGroup.Version.Split(".")

#Increment
$avBuild = [Convert]::ToInt32($avBuild,10) + 1

#Put new version back into csproj (XML)
$version = "$avMajor.$avMinor.$avBuild"
$xml.Project.PropertyGroup.Version = $version

#Save csproj (XML)
$xml.Save($path)

git add .
git commit -m "upload version $version"
git push

Remove-Item './pack' -Recurse
Remove-Item './bin' -Recurse
Remove-Item './obj' -Recurse
dotnet restore
dotnet build -c Release
dotnet pack -c Release -o ./pack
dotnet nuget push "./pack/ServiceToController.$version.nupkg" --api-key $env:NugetApiKey --source https://api.nuget.org/v3/index.json
