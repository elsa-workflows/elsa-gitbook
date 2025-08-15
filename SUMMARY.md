# Table of contents

* [Elsa Workflows 3](README.md)

## Getting Started

* [Concepts](getting-started/concepts/README.md)
  * [Outcomes](getting-started/concepts/outcomes.md)
  * [Correlation ID](getting-started/concepts/correlation-id.md)
* [Hello World](getting-started/hello-world.md)
* [Prerequisites](getting-started/prerequisites.md)
* [Packages](getting-started/packages.md)
* [Containers](getting-started/containers/README.md)
  * [Docker](getting-started/containers/docker.md)
  * [Docker Compose](getting-started/containers/docker-compose/README.md)
    * [Elsa Server + Studio](getting-started/containers/docker-compose/elsa-server-+-studio.md)
    * [Elsa Server + Studio - Single Image](getting-started/containers/docker-compose/elsa-server-+-studio-single-image.md)
    * [Persistent Database](getting-started/containers/docker-compose/persistent-database.md)
    * [Traefik](getting-started/containers/docker-compose/traefik.md)

## Application Types

* [Elsa Server](application-types/elsa-server.md)
* [Elsa Studio](application-types/elsa-studio.md)
* [Elsa Server + Studio (WASM)](application-types/elsa-server-+-studio-wasm.md)

## Guides

* [HTTP Workflows](guides/http-workflows/README.md)
  * [Programmatic](guides/http-workflows/programmatic.md)
  * [Designer](guides/http-workflows/designer.md)
* [External Application Interaction](guides/external-application-interaction.md)
* [Loading Workflows from JSON](guides/loading-workflows-from-json.md)
* [Running Workflows](guides/running-workflows/README.md)
  * [Using Elsa Studio](guides/running-workflows/using-elsa-studio.md)
  * [Using a Trigger](guides/running-workflows/using-a-trigger.md)
  * [Dispatch Workflow Activity](guides/running-workflows/dispatch-workflow-activity.md)

## Activities

* [Control Flow](activities/control-flow/README.md)
  * [Decision](activities/control-flow/decision.md)
* [MassTransit](activities/masstransit/README.md)
  * [Tutorial](activities/masstransit/tutorial.md)

## Expressions

* [C#](expressions/c.md)
* [JavaScript](expressions/javascript.md)
* [Python](expressions/python.md)
* [Liquid](expressions/liquid.md)

## Extensibility

* [Custom Activities](extensibility/custom-activities.md)

***

* [Reusable Triggers](reusable-triggers-3.5-preview.md)

## Multitenancy

* [Introduction](multitenancy/introduction.md)
* [Setup](multitenancy/setup.md)

## Operate

* [Variables](operate/workflow-instance-variables.md)
* [Activation Strategies](operate/workflow-activation-strategies.md)
* [Incidents](operate/incidents/README.md)
  * [Strategies](operate/incidents/strategies.md)
  * [Configuration](operate/incidents/configuration.md)
* [Alterations](operate/alterations/README.md)
  * [Alteration Plans](operate/alterations/alteration-plans/README.md)
    * [REST API](operate/alterations/alteration-plans/rest-api.md)
  * [Applying Alterations](operate/alterations/applying-alterations/README.md)
    * [REST API](operate/alterations/applying-alterations/rest-api.md)
    * [Extensibility](operate/alterations/applying-alterations/extensibility.md)

## Optimize

* [Log Persistence](optimize/log-persistence.md)
* [Retention](optimize/retention.md)

## Hosting

* [Distributed Hosting](hosting/distributed-hosting.md)

## Studio

* [Design](studio/design/README.md)
  * [Workflow Editor (3.5-preview)](studio/design/workflow-editor-3.5-preview.md)
  * [Activity Pickers (3.7-preview)](studio/design/activity-pickers-3.7-preview.md)
* [Workflow Editor](studio/workflow-editor/README.md)
  * [UI Hints](studio/workflow-editor/ui-hints.md)
  * [Content Visualisers (3.6-preview)](studio/workflow-editor/content-visualisers-3.6-preview.md)
  * [Field Extensions](studio/workflow-editor/field-extensions.md)
* [Localization](studio/localization.md)
