# cratedig

> **This is a modified version of slskd.  It is not maintained by, endorsed by, or affiliated with the slskd project or its author(s).**
>
> It is based on [slskd/slskd](https://github.com/slskd/slskd). What this fork changes is listed in [FORK.md](FORK.md).

cratedig is a client-server application for the [Soulseek](https://www.slsknet.org/news/) file-sharing network, built on slskd and extended for running it as the front of a music collection: search watches, table views of results and downloads, a built-in player, comparison of downloaded copies, share links, notifications and a phone layout.

Please do not report problems with cratedig to the slskd project. They are this fork's, not upstream's.

## Running it

No images or binaries of cratedig are published. Build the image from this repository:

```shell
docker build --build-arg VERSION=<upstream version>.65534-fork -t cratedig:local .
```

and run it the way slskd is run, with the image name changed:

```shell
docker run -d \
  -p 5030:5030 \
  -p 5031:5031 \
  -p 50300:50300 \
  -e SLSKD_REMOTE_CONFIGURATION=true \
  -v <path/to/application/data>:/app \
  --name cratedig \
  --user 1000:1000 \
  cratedig:local
```

Once it is running, log in to the web UI with the default username `slskd` and password `slskd` (these are slskd's defaults, kept so an existing setup carries over) and complete the configuration.

Configuration keys, environment variables (`SLSKD_*`) and the configuration file (`slskd.yml`) keep slskd's names for the same reason.

## Configuration

[`docs/config.md`](docs/config.md) is the reference: slskd's own options, plus the ones this fork adds. [`config/slskd.example.yml`](config/slskd.example.yml) is an example configuration file, and [`docs/reverse_proxy.md`](docs/reverse_proxy.md) covers running behind a reverse proxy.

## License

cratedig is licensed under the [GNU Affero General Public License v3.0](LICENSE), with the Additional Terms that slskd is distributed under. See [LICENSE](LICENSE) for the complete terms.
