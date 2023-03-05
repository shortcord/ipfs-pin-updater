# IPFS Pin Updater
Small Dotnet application that updates IPFS and IPNS pins.

## Installation (Binaries)
To install from binaries, donwload the latest main artifact [here](https://gitlab.shortcord.com/api/v4/projects/196/jobs/artifacts/main/download?job=build-standalone) and extract and run.

## Installation (Debian Repo)
1) Import the GPG key from [keys.openpgp.org](https://keys.openpgp.org/search?q=short%2Bpackaging%40shortcord.com)
    - `gpg --keyserver keys.openpgp.org --recv-keys 84BD5723FBDAE2D0`
    - You can also just download the key directly from [here](https://keys.openpgp.org/search?q=short%2Bpackaging%40shortcord.com) and import it via `gpg --import ./filename.gpg`.
2) Export GPG into a directory `apt` can read, example being `/etc/apt/keyrings/`
    - `mkdir -p /etc/apt/keyrings/ && gpg --export 84BD5723FBDAE2D0 > /etc/apt/keyrings/shortcord.gpg`
3) Add `sources.list.d/shortcord.list`
    - `echo "deb [signed-by=/etc/apt/keyrings/shortcord.gpg] https://shortcord-public-owo-solutions.s3.us-west-000.backblazeb2.com stable main" > /etc/apt/sources.list.d/shortcord.list`
4) Update Apt
    - `apt update`
5) Install the package
    - `apt install -y ipfs-pin-updater`

## Configuration
The application is configured by Json `appsettings.json` though it is recommended to use `appsettings.custom.json` instead as `appsettings.json` is managed via your package manager (if it is installed with that).  

If you installed the program via `apt` then the configuration is located `/etc/ipfs-pin-updater`, otherwise configuration is done via the `.json` files next to the executable.

## Running
If you installed this program via `apt` then currently only root can run the application, this is due to where it stores the database `/var/ipfs-pin-updater.litedb` being owned by root. You can however change this location via `appsettings.custom.json` which would allow whoever you wish to run it.  
This will be fixed in the future via a dedicated user and such, please see this [issue](https://gitlab.shortcord.com/shortcord/ipfs-pin-updater/-/issues/2) for status updates.