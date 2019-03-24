nuget restore
msbuild EurekaBot.sln -p:DeployOnBuild=true -p:PublishProfile=EurekaBot-Web-Deploy.pubxml -p:Password=fnAM6QHjLGckbSbZEbBHaNAx1nhmgc0gxQBKJW071mcmMY7m3fKJiyipYwYm

