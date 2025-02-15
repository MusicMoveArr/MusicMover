Music Mover, Move missing music to directories to complete your collection

Move music "From" to your "Target" directory by only moving to the target directory what music you're missing

Sometimes we download too much from torrents, nzb etc and it's a real pain to organize it all

This tool will make it almost too easy to organize your entire collection

I made this tool mainly because Lidarr's behavior is just a pain, I have too many unmapped files, it deletes stuff, overwrites files because it was unmapped (causing flac/mp3 versions) etc oh well if you used Lidarr you'd know

Loving the work I do? buy me a coffee https://buymeacoffee.com/musicmovearr

# Features
Move music missing from-directory to target-directory (Check below how)

Reading the target-directory is memory-cached for performance

Run the process job in parallel for performance (scanning multiple files at once)

with --extrascan or --extrascans you can scan besides the target-directory in other locations if you already have the music (in case you have usb harddisks, NFS/SMB shares etc)

Create the Artist directory if it's missing using --create-artist-directory (if Artist directory is missing and the argument is not given the song is skipped moving)

Create the Album directory if it's missing using --create-album-directory (if Album directory is missing and the argument is not given the song is skipped moving)

With --extra-dir-must-exist, the Artist/Album directory must as well already exist in the extra scan directories

You can delete the already owned music from the "from directory" if it already exists in the target directory

Skip the first 5 directories of /home/aaa/Downloads (in case temp folders etc you don't want to move) using "--skip-directories 5"

Rename the file name that constains "Various Artists" to the first performer using --various-artists, it will rename it for example from "Various Artists - Jane Album - 01 - My Song.mp3" to "John Doe - Jane Album - 01 - My Song.mp3"

Update the Artist and Performers tags by using "--update-Artist-Tags", this will cleanup the tags (so to say) by saving the "Various Artists" to it's real Artist name, this includes as well changing "John Doe feat Jane Doe" in the Artist tags to just "John Doe"

Apply media tags from MusicBrainz by fingerprinting using AcoustId
Argument "--always-check-acoust-id" will always force reading from MusicBrainz even with tags available already in the media file

Rename filenames with a file format, most used standard is "Artist - Album - Disc-TrackNumber - Title
Note: Discnumber in this example is only applied if discnumber is higher then 1
Format: {Artist} - {Album} - {Disc:cond:<=1?{Track:00}|{Disc:00}-{Track:00}} - {Title}

Fix possible file corruption by re-writing the file using FFMpeg if MusicMover is unable to read the media tags
FFMpeg arguments: ffmpeg -i "[filepath]" -c copy -movflags +faststart "[temp_filepath]"

# Usage
```
dotnet "MusicMover/bin/Debug/net8.0/MusicMover.dll" \
--from "~/Downloads" \
--extrascans "[\"~/Some/Directory/Music\", \"~/Some/Directory2/Music\", \"~/Some/Directory3/Music\", \"~/Some/Directory4/Music\"]", \
--artist-dirs-must-not-exist "[\"~/Some/Directory/Music\", \"~/Some/Directory2/Music\", \"~/Some/Directory3/Music\", \"~/Some/Directory4/Music\"]", \
--target "~/Music" \
--create-album-directory \
--create-artist-directory \
--parallel \
--delete-duplicate-from \
--skip-directories 5 \
--various-artists \
--update-Artist-Tags \
--fix-file-corruption \
--acoustid-api-key "xxxxxxxx" \
--file-format "{Artist} - {Album} - {Disc:cond:<=1?{Track:00}|{Disc:00}-{Track:00}} - {Title}" \
--always-check-acoust-id \
--continue-scan-error
```
```
dotnet "MusicMover/bin/Debug/net8.0/MusicMover.dll" \
--from "~/Downloads" \
--extrascans "[\"~/Some/Directory/Music\", \"~/Some/Directory2/Music\", \"~/Some/Directory3/Music\", \"~/Some/Directory4/Music\"]", \
--artist-dirs-must-not-exist "[\"~/Some/Directory/Music\", \"~/Some/Directory2/Music\", \"~/Some/Directory3/Music\", \"~/Some/Directory4/Music\"]", \
--target "~/Music" \
--create-album-directory \
--create-artist-directory \
--extra-dir-must-exist \
--parallel \
--delete-duplicate-from \
--skip-directories 5 \
--various-artists \
--update-Artist-Tags \
--fix-file-corruption
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
