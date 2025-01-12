# Designer

## Before you start﻿ <a href="#before-you-start" id="before-you-start"></a>

For this guide, we will need the following:

* An [Elsa Server](https://elsa-workflows.github.io/elsa-documentation/elsa-server.html?section=Designer) project
*   An [Elsa Studio](https://elsa-workflows.github.io/elsa-documentation/docker.html?section=Designer#elsa-studio) instance

    ```bash
    docker pull elsaworkflows/elsa-studio-v3:latest
    docker run -t -i -e ASPNETCORE_ENVIRONMENT='Development' -e HTTP_PORTS=8080 -e ELSASERVER__URL=https://localhost:5001/elsa/api -p 14000:8080 elsaworkflows/elsa-studio-v3:latest
    ```



{% hint style="info" %}
**Port Numbers**

When starting Elsa Studio, make sure you provide it with the correct URL to the Elsa Server application.

For example, if Elsa Server runs on https://localhost:5001, the Docker command should look like this:

`docker run -t -i -e ASPNETCORE_ENVIRONMENT='Development' -e HTTP_PORTS=8080 -e ELSASERVER__URL=https://localhost:5001/elsa/api -p 14000:8080 elsaworkflows/elsa-studio-v3:latest`
{% endhint %}

Please return here when you are ready.

