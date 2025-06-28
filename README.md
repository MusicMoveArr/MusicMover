Music Mover, Move missing music to directories to complete your collection

Move music "From" to your "Target" directory by only moving to the target directory what music you're missing

Sometimes we download too much from torrents, nzb etc and it's a real pain to organize it all

This tool will make it almost too easy to organize your entire collection

I made this tool mainly because Lidarr's behavior is just a pain, I have too many unmapped files, it deletes stuff, overwrites files because it was unmapped (causing flac/mp3 versions) etc oh well if you used Lidarr you'd know

Loving the work I do? buy me a coffee https://buymeacoffee.com/musicmovearr

# Features
1. Move music missing from-directory to target-directory (Check below how)
2. Reading the target-directory is memory-cached for performance
3. Run the process job in parallel for performance (scanning multiple files at once)
4. with --extra-scan or --extra-scans you can scan besides the target-directory in other locations if you already have the music (in case you have usb harddisks, NFS/SMB shares etc)
5. Create the Artist directory if it's missing using --create-artist-directory (if Artist directory is missing and the argument is not given the song is skipped moving)
6. Create the Album directory if it's missing using --create-album-directory (if Album directory is missing and the argument is not given the song is skipped moving)
7. With --extra-dir-must-exist, the Artist/Album directory must as well already exist in the extra scan directories
8. You can delete the already owned music from the "from directory" if it already exists in the target directory
9. Skip the first 5 directories of /home/aaa/Downloads (in case temp folders etc you don't want to move) using "--skip-directories 5"
10. Rename the file name that constains "Various Artists" to the first performer using --various-artists, it will rename it for example from "Various Artists - Jane Album - 01 - My Song.mp3" to "John Doe - Jane Album - 01 - My Song.mp3"
11. Update the Artist and Performers tags by using "--update-artist-tags", this will cleanup the tags (so to say) by saving the "Various Artists" to it's real Artist name, this includes as well changing "John Doe feat Jane Doe" in the Artist tags to just "John Doe"
12. Apply media tags from MusicBrainz by fingerprinting using AcoustId
13. Apply media tags from Tidal
14. Argument "--always-check-acoustid" will always force reading from MusicBrainz even with tags available already in the media file
15. Rename filenames with a file format, most used standard is "Artist - Album - Disc-TrackNumber - Title, Note: Discnumber in this example is only applied if discnumber is higher then 1

Format: {Artist} - {Album} - {Disc:cond:<=1?{Track:00}|{Disc:00}-{Track:00}} - {Title}

16. Fix possible file corruption by re-writing the file using FFMpeg if MusicMover is unable to read the media tags
FFMpeg arguments: ffmpeg -i "[filepath]" -c copy -movflags +faststart "[temp_filepath]"
17. Apply media tags from MusicBrainz, Tidal using the self-hosted MiniMedia Metadata API 

# Description of arguments
| Longname Argument  | Description | Example |
| ------------- | ------------- | ------------- |
| --from | From the directory. | ~/Downloads |
| --target | directory to move/copy files to. | ~/Music |
| --dryrun | Dry run, no files are moved/copied. | [no value required] |
| --create-artist-directory | Create Artist directory if missing on target directory. | [no value required] |
| --create-album-directory | Create Album directory if missing on target directory. | [no value required] |
| --parallel | multi-threaded processing. | [no value required] |
| --skip-directories | Skip X amount of directories in the From directory to process. | 5 |
| --delete-duplicate-from | Delete the song in From Directory if already found at Target. | [no value required] |
| --delete-duplicate-to | Delete the song in To Directory if already found at Target (duplicates). | [no value required] |
| --extra-scans | Scan extra directories, usage, ["a","b"], besides the target directory. | "\~/Some/Directory/Music" "\~/Some/Directory2/Music" "\~/Some/Directory3/Music" "\~/Some/Directory4/Music" |
| --extra-scan| Scan a extra directory, besides the target directory. | ~/nfs_share/Music |
| --various-artists | Rename "Various Artists" in the file name with First Performer. | [no value required] |
| --extra-dir-must-exist | Artist folder must already exist in the extra scanned directories. | [no value required] |
| --artist-dirs-must-not-exist | Artist folder must not exist in the extra scanned directories, only meant for --createArtistDirectory, -g. | [no value required] |
| --update-artist-tags | Update Artist metadata tags. | [no value required] |
| --fix-file-corruption | Attempt fixing file corruption by using FFMpeg for from/target/scan files. | [no value required] |
| --acoustid-api-key | When AcoustId API Key is set, try getting the artist/album/title when needed. | "xxxxxxxxxxx" |
| --file-format | rename file format {Artist} {SortArtist} {Title} {Album} {Track} {TrackCount} {AlbumArtist} {AcoustId} {AcoustIdFingerPrint} {BitRate} | {Artist} - {Album} - {Disc:cond:<=1?{Track:00}|{Disc:00}-{Track:00}} - {Title} |
| --directory-seperator | Directory Seperator replacer, replace '/' '\' to .e.g. '_'. | _ |
| --always-check-acoustid | Always check & Write to media with AcoustId for missing tags. | [no value required] |
| --continue-scan-error | Continue on scan errors from the Music Libraries. | [no value required] |
| --overwrite-artist | Overwrite the Artist name when tagging from MusicBrainz. | [no value required] |
| --overwrite-album-artist | Overwrite the Album Artist name when tagging from MusicBrainz. | [no value required] |
| --overwrite-album | Overwrite the Album name when tagging from MusicBrainz. | [no value required] |
| --overwrite-track | Overwrite the Track name when tagging from MusicBrainz. | [no value required] |
| --only-move-when-tagged | Only process/move the media after it was MusicBrainz or Tidal tagged (-AI must be used). | [no value required] |
| --only-filename-matching | Only filename matching when trying to find duplicates. | [no value required] |
| --search-by-tag-names | Search MusicBrainz from media tag-values if AcoustId matching failed. | [no value required] |
| --tidal-client-id | The Client Id used for Tidal's API. | "xxxxxxxxxxx" |
| --tidal-client-secret | The Client Client used for Tidal's API. | "xxxxxxxxxxx" |
| --tidal-country-code | Tidal's CountryCode (e.g. US, FR, NL, DE etc). | "US" |
| --metadata-api-base-url | MiniMedia's Metadata API Base Url. | http://localhost:8080 |
| --metadata-api-providers | MiniMedia's Metadata API Provider (Any, Spotify, Tidal, MusicBrainz). | "Tidal" "MusicBrainz" |


# Usage
```
dotnet "MusicMover/bin/Debug/net8.0/MusicMover.dll" \
--from "~/Downloads" \
--extra-scans "~/Some/Directory/Music" "~/Some/Directory2/Music" "~/Some/Directory3/Music" "~/Some/Directory4/Music" \
--artist-dirs-must-not-exist "~/Some/Directory/Music" "~/Some/Directory2/Music" "~/Some/Directory3/Music" "~/Some/Directory4/Music" \
--target "~/Music" \
--create-album-directory \
--create-artist-directory \
--delete-duplicate-from \
--parallel \
--skip-directories 5 \
--various-artists \
--update-artist-tags \
--fix-file-corruption \
--acoustid-api-key "xxxxxxxx" \
--file-format "{Artist} - {Album} - {Disc:cond:<=1?{Track:00}|{Disc:00}-{Track:00}} - {Title}" \
--always-check-acoustid \
--continue-scan-error \
--only-move-when-tagged \
--only-filename-matching \
--overwrite-artist \
--overwrite-album-artist \
--overwrite-album \
--overwrite-track \
--tidal-country-code "US" \
--metadata-api-base-url "http://localhost:8080" \
--metadata-api-providers "Tidal" "MusicBrainz"

```
```
dotnet "MusicMover/bin/Debug/net8.0/MusicMover.dll" \
--from "~/Downloads" \
--extrascans "~/Some/Directory/Music" "~/Some/Directory2/Music" "~/Some/Directory3/Music" "~/Some/Directory4/Music" \
--artist-dirs-must-not-exist "~/Some/Directory/Music" "~/Some/Directory2/Music" "~/Some/Directory3/Music" "~/Some/Directory4/Music" \
--target "~/Music" \
--create-album-directory \
--create-artist-directory \
--extra-dir-must-exist \
--parallel \
--delete-duplicate-from \
--skip-directories 5 \
--various-artists \
--update-artist-tags \
--fix-file-corruption
```

# Docker Compose example
Keep note of multie-value seperator ":"

This process will run every 6 hours based on the quartz cronjob format which includes seconds

Environment variables with a missing "=" are a boolean, no value is needed, you set the "true" without "=true"

```
services:
  musicmover:
    image: musicmovearr/musicmover:latest
    container_name: musicmover
    restart: unless-stopped
    environment:
      - PUID=1000
      - PGID=1000
      - CRON=0 0 */6 ? * *
      - MOVE_FROM=/Downloads
      - MOVE_TARGET=/Music
      - MOVE_DRYRUN
      - MOVE_CREATEARTISTDIRECTORY
      - MOVE_CREATEALBUMDIRECTORY
      - MOVE_PARALLEL
      - MOVE_SKIPDIRECTORIES=0
      - MOVE_DELETEDUPLICATEFROM
      - MOVE_DELETEDUPLICATETO
      - MOVE_EXTRASCANS=/nfs_share/music1:/nfs_share/music2
      - MOVE_EXTRASCAN=/nfs_share/music
      - MOVE_VARIOUSARTISTS
      - MOVE_EXTRADIRMUSTEXIST
      - MOVE_EXTRADIRMUSTNOTEXIST=/nfs_share/music1:/nfs_share/music2
      - MOVE_UPDATEARTISTTAGS
      - MOVE_FIXFILECORRUPTION
      - MOVE_ACOUSTIDAPIKEY=xxxxxxxxxxxxxxxxx
      - MOVE_FILEFORMAT={Artist} - {Album} - {Disc:cond:<=1?{Track:00}|{Disc:00}-{Track:00}} - {Title}
      - MOVE_DIRECTORYSEPERATOR=_
      - MOVE_ALWAYSCHECKACOUSTID
      - MOVE_CONTINUESCANERROR
      - MOVE_OVERWRITEARTIST
      - MOVE_OVERWRITEALBUMARTIST
      - MOVE_OVERWRITEALBUM
      - MOVE_OVERWRITETRACK
      - MOVE_ONLYMOVEWHENTAGGED
      - MOVE_ONLYFILEMATCHING
      - MOVE_SEARCHBYTAGNAMES
      - MOVE_TIDALCLIENTID=xxxxxxxxxxxxxxxxx
      - MOVE_TIDALCLIENTSECRET=xxxxxxxxxxxxxxxxx
      - MOVE_TIDALCOUNTRYCODE=US
      - MOVE_METADATAAPIBASEURL=http://192.168.1.1:8080
      - MOVE_METADATAAPIPROVIDERS=MusicBrainz:Deezer:Tidal:Spotify
    volumes:
      - ~/Music:/Music
      - ~/Downloads:/Downloads
      - ~/nfs_share:/nfs_share
```

# Build
## ArchLinux
```
sudo pacman -Syy dotnet-sdk-8.0 git
git clone https://github.com/MusicMoveArr/MusicMover.git
cd MusicMover
dotnet restore
dotnet build
cd MusicMover/bin/Debug/net8.0
dotnet MusicMover.dll --help
```
