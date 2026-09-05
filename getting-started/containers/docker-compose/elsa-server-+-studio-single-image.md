---
description: >-
  This guide demonstrates how to set up Elsa Server and Studio using Docker
  Compose, enabling you to run both components from a single Docker image.
---

# Elsa Server + Studio - Single Image

{% hint style="warning" %}
This example uses the independently published, unversioned
`elsaworkflows/elsa-server-and-studio-v3:latest` image. Verify its digest and
release metadata before treating it as an Elsa 3.8.0 deployment; use [Upgrade
to Elsa 3.8.0](../../upgrading-to-3.8.md) for stable source and package hosts.
{% endhint %}

### Docker Compose Configuration﻿ <a href="#docker-compose-configuration" id="docker-compose-configuration"></a>

Below is an example Docker Compose configuration that sets up the Elsa Server + Studio application:

```yaml
services:

    # Elsa Studio and Server from a single image.
    elsa-server-and-studio:
        image: elsaworkflows/elsa-server-and-studio-v3:latest
        pull_policy: always
        environment:
            ASPNETCORE_ENVIRONMENT: Development
            HTTP_PORTS: 8080
            HTTP__BASEURL: http://localhost:14000
        ports:
            - "14000:8080"
```

### Steps to Set Up﻿ <a href="#steps-to-set-up" id="steps-to-set-up"></a>

* Create a `docker-compose.yml` file in your project directory with the above configuration.
* Ensure that Docker and Docker Compose are installed on your machine. Refer to the [prerequisites documentation](https://elsa-workflows.github.io/elsa-documentation/prerequisites.html#docker) for installation guidance.
* Open a terminal in the directory containing the `docker-compose.yml` file.
*   Run the following command to start the container:

    ```bash
    docker-compose up
    ```

### Accessing Elsa﻿ <a href="#accessing-elsa" id="accessing-elsa"></a>

Once the container is running, you can access Elsa Studio in your browser at: [http://localhost:14000](http://localhost:14000/).

Configure the server's `Identity` section and
`Identity:Tokens:SigningKey` through the image's published configuration
before signing in. Because the image tag is unversioned, do not infer its
login defaults from the Elsa 3.8.0 package documentation. For a 3.8.0 source
or package host, see [Elsa Identity](../../../guides/authentication/elsa-identity.md).
