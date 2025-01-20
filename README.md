> [!NOTE]  
> Due to changes to YouTube this plugin no longer functions and is no longer maintained.

<h1 align="center">Jellyfin TMDb Trailers Plugin</h1>
<h3 align="center">Part of the <a href="https://jellyfin.media">Jellyfin Project</a></h3>

<p align="center">
This plugin is built with .NET to watch trailers sourced from TMDb
</p>

TODO:
- Some trailers will fail to play for unknown reasons
- Implement Vimeo playback

## Build Process

1. Clone or download this repository

2. Ensure you have .NET Core SDK setup and installed

3. Build plugin with following command.

```sh
dotnet publish --configuration Release --output bin
```
4. Place the resulting file in the `plugins` folder under the program data directory or inside the portable install directory

Repo: https://raw.githubusercontent.com/crobibero/jellyfin-plugin-tmdb-trailers/master/manifest.json
