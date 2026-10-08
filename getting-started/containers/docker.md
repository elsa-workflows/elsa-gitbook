# Docker

Use an exact Elsa 3 image version, and run Server and Studio at the same version. The examples below use `3.9.0`; `3.8.4` is also available for applications on that release line. See [Upgrading to Elsa 3.9](../upgrading-to-3.9.md) before upgrading an existing installation.

The [Elsa Apps repository](https://github.com/elsa-workflows/elsa-apps) builds these application images from the matching released Core, Studio and Extensions packages:

| Application | Docker Hub repository |
| --- | --- |
| Server | `elsaworkflows/elsa-server-app` |
| Studio, hosted Blazor WebAssembly | `elsaworkflows/elsa-studio-blazor-wasm-app` |
| Studio, Blazor Server | `elsaworkflows/elsa-studio-blazor-server-app` |
| Studio, standalone Blazor WebAssembly | `elsaworkflows/elsa-studio-blazor-wasm-standalone-app` |
| Server + Studio, Blazor WebAssembly | `elsaworkflows/elsa-server-studio-blazor-wasm-app` |
| Server + Studio, Blazor Server | `elsaworkflows/elsa-server-studio-blazor-server-app` |

The exact version tags `elsaworkflows/elsa-server:3.9.0` and `elsaworkflows/elsa-studio:3.9.0` are aliases for the Server and hosted WebAssembly images respectively. The corresponding `3.8.4` aliases are available too. Images support Linux on AMD64 and ARM64.

{% hint style="warning" %}
Do not use `elsaworkflows/elsa-server:latest` or `elsaworkflows/elsa-studio:latest` for an Elsa 3 deployment: those tags follow Elsa 4 preview builds. The older `elsa-server-v3`, `elsa-studio-v3` and `elsa-server-and-studio-v3` repositories are legacy images; their `latest` tags do not track the current Elsa 3 release. Older Compose examples using those names need their image and configuration references reviewed before use.
{% endhint %}

### Elsa Server + Studio <a href="#elsa-server-and-studio" id="elsa-server-and-studio"></a>

For a local combined host, start both the workflow server and the WebAssembly Studio on port `13000`:

Set `ELSA_ADMIN_PASSWORD` in your shell to a password of your choice before running the command. The host does not enable an admin login until you explicitly supply a user name and password.

```bash
docker run --rm --name elsa-combined -p 13000:8080 \
  -e HTTP__BASEURL=http://localhost:13000 \
  -e Backend__Url=http://localhost:13000/elsa/api \
  -e Identity__AdminUser__UserName=admin \
  -e Identity__AdminUser__Password="${ELSA_ADMIN_PASSWORD:?Set ELSA_ADMIN_PASSWORD first}" \
  elsaworkflows/elsa-server-studio-blazor-wasm-app:3.9.0
```

Open [http://localhost:13000](http://localhost:13000). Studio connects to the workflow API at `/elsa/api`.

### Elsa Server <a href="#elsa-server" id="elsa-server"></a>

Start the Server on port `13000`:

```bash
docker run --rm --name elsa-server -p 13000:8080 \
  -e HTTP__BASEURL=http://localhost:13000 \
  -e Identity__AdminUser__UserName=admin \
  -e Identity__AdminUser__Password="${ELSA_ADMIN_PASSWORD:?Set ELSA_ADMIN_PASSWORD first}" \
  elsaworkflows/elsa-server-app:3.9.0
```

The health endpoint is [http://localhost:13000/](http://localhost:13000/) and the workflow API base URL is `http://localhost:13000/elsa/api`.

### Elsa Studio <a href="#elsa-studio" id="elsa-studio"></a>

With the Server above running, start the hosted WebAssembly Studio on port `14000`:

```bash
docker run --rm --name elsa-studio -p 14000:8080 \
  -e Backend__Url=http://localhost:13000/elsa/api \
  elsaworkflows/elsa-studio-blazor-wasm-app:3.9.0
```

Open [http://localhost:14000](http://localhost:14000). `Backend__Url` must be reachable from the user's browser, because WebAssembly sends API requests from the browser. For the Blazor Server image, the configured backend must be reachable from the Studio container as well.

The standalone WebAssembly image serves static files through nginx. Its configuration is supplied through its served `appsettings.json`; ASP.NET configuration environment variables apply to the ASP.NET hosted variants.

Sign in with `admin` and the password you supplied to the Server or combined host. These examples use SQLite and the built-in admin provider. Configure signing keys, users, database persistence and HTTPS for your deployment; see [Elsa Identity](../../guides/authentication/elsa-identity.md). The sample Server permits all CORS origins; restrict that policy in a custom host for your deployment. Removing a container without a persistent database volume removes its local SQLite data.

{% hint style="warning" %}
The release smoke tests use SQLite. The included MySQL provider currently resolves Pomelo 9 alongside EF Core 10, outside Pomelo's declared EF Core version range. MySQL compatibility has not been verified for these images; use a host with a compatible provider dependency set for a MySQL deployment.
{% endhint %}

### Verify a published image

Pull the exact version, or pin a verified manifest digest in your deployment:

```bash
docker pull elsaworkflows/elsa-studio:3.9.0
docker buildx imagetools inspect elsaworkflows/elsa-studio:3.9.0
```

The [Apps image workflow](https://github.com/elsa-workflows/elsa-apps/actions) records the source commit, resolved Elsa package versions, platform digests and smoke-test results for each release. A matching NuGet release alone does not establish that a container was published.
