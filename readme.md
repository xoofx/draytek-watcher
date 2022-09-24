# draytek-watcher  [![Build Status](https://github.com/xoofx/draytek-watcher/workflows/ci/badge.svg?branch=main)](https://github.com/xoofx/draytek-watcher/actions)

This a small daemon to install on an Linux/Ubuntu/Windows box to check the status of the WAN connection of a Draytek router and to reenable it if it seems to be down.

> **DISCLAIMER**
>
> I will provide no support for this project. I'm using it personally to reboot my WAN connection when it is stuck in a dangling state.
>
> This project is only tested with:
> - a [Draytek Vigor 2927ax](https://www.draytek.com/products/vigor2927/) and a Ubuntu server (systemd).
> - a [Draytek Vigor 2927](https://www.draytek.com/products/vigor2927/) and a Windows 10 workstation (service).
>
> So it might not work with a different version without some tweaking.
> It is using the telnet protocol to interact with the router.

## Build and Install (for Linux)

Clone this repo and install [.NET SDK 6.0+](https://dotnet.microsoft.com/en-us/download/dotnet/6.0).


Then install [dotnet-releaser](https://github.com/xoofx/dotnet-releaser):

```
dotnet tool install --global dotnet-releaser
```

Edit the configuration file in `draytek-watcher/draytek-watcher.toml` to update the admin password:

```toml
# -------------------------------------------------
# Router configuration
# -------------------------------------------------
# Connection settings
address = "192.168.1.1"
port = 23
type = "DrayTek"
protocol = "telnet"
username = "admin"
password = "REPLACE_WITH_YOUR_PASSWORD"  # <<<< HERE: Put your admin password
# WAN number
wan = 1
# How often do we check for the router state?
verify_delay_in_seconds = 300
# How many times do we try to reconnect the WAN in case of offline?
retry_count = 4
# How much time to wait after a retry and before checking if the WAN is back online?
sleep_in_millis_between_retry_count = 10000
```

Then run the command:

```
cd src
dotnet-releaser build --force dotnet-releaser.toml
```

This should generate a `deb` package at `artifacts-dotnet-releaser\draytek-watcher.0.1.0.linux-x64.deb` that you can install on an Ubuntu server

```
sudo apt install ./artifacts-dotnet-releaser\draytek-watcher.0.1.0.linux-x64.deb
```

The watcher is logging all its activity to systemd logger. You can look at the logs:

```
sudo journalctl -u draytek-watcher
```

You should see logs like this:

```
Jan 18 01:46:12 ak1-linux draytek-watcher[21335]: Microsoft.Hosting.Lifetime[0] Application started. Hosting environment: Production; Content root path: /
Jan 18 01:46:12 ak1-linux draytek-watcher[21335]: draytek-watcher[0] DrayTekWatcher.ConfigurationFile = /home/xoofx/code/DrayTekWatcher/src/DrayTekWatcher/bin/Release/net6.0/linux-x64/publish/draytek-watcher.toml
Jan 18 01:46:12 ak1-linux draytek-watcher[21335]: draytek-watcher[0] Configuration = Type: DrayTek, Address: 192.168.1.1, Port: 23, Protocol: telnet, Username: admin, Password: ********************************, VerifyDelayInSeconds: 300v
Jan 18 01:46:12 ak1-linux draytek-watcher[21335]: draytek-watcher[0] => WAN1 - is online.
Jan 18 01:46:12 ak1-linux draytek-watcher[21335]: draytek-watcher[0] => WAN1 - Verifying every 300s.
Jan 18 08:42:16 ak1-linux draytek-watcher[21335]: draytek-watcher[0] => WAN1 - is offline.
Jan 18 08:42:16 ak1-linux draytek-watcher[21335]: draytek-watcher[0] => WAN1 - Trying [1/4] to restart it.
Jan 18 08:42:16 ak1-linux draytek-watcher[21335]: draytek-watcher[0] => WAN1 - Stopping WAN.
Jan 18 08:42:18 ak1-linux draytek-watcher[21335]: draytek-watcher[0] => WAN1 - Waiting WAN for 2s before trying to start .
Jan 18 08:42:20 ak1-linux draytek-watcher[21335]: draytek-watcher[0] => WAN1 - Starting WAN.
Jan 18 08:42:21 ak1-linux draytek-watcher[21335]: draytek-watcher[0] => WAN1 - Waiting to restart for 10s.
Jan 18 08:42:31 ak1-linux draytek-watcher[21335]: draytek-watcher[0] => WAN1 - Restarted successfully.
```

## Build and Install (for Windows)

Clone this repo and install [.NET SDK 6.0+](https://dotnet.microsoft.com/en-us/download/dotnet/6.0).

Then run:

```
dotnet build
```

Currently, only systemd services are supported by [dotnet-releaser](https://github.com/xoofx/dotnet-releaser) for deb and rpm packages. But the tool is working as well as a windows service. Here is how to use the Windows `sc` (Service Control) to register the executable as a service.

```batch
# create (copy your toml configuration along with the draytek-watcher.exe file)
sc create DrayTekWatcherService DisplayName="DrayTek-Watcher Service" binPath="full\path\to\\draytek-watcher.exe"

# start
sc start DrayTekWatcherService

# stop
sc start DrayTekWatcherService

# delete
sc delete DrayTekWatcherService
```

## License

This software is released under the [BSD-Clause 2 license](https://opensource.org/licenses/BSD-2-Clause). 

## Author

Alexandre Mutel aka [xoofx](http://xoofx.com).
