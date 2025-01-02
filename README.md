---
description: Introducing Elsa Workflows
---

# Elsa Workflows

Elsa Workflows is a powerful and flexible **execution** engine, encapsulated as a set of open-source .NET libraries designed to infuse .NET applications with workflow capabilities. With Elsa, developers can weave **logic** directly into their **systems**, enhancing functionality and **automation** and align seamlessly with the application’s core functionality.

Workflows in Elsa can be defined in two ways:

* Programmatically: By writing .NET code, developers can define complex workflows tailored to specific business needs.
* Visually: Using the built-in designer, non-developers or those who prefer a visual approach can create and modify workflows with ease.

```csharp
// Define workflows directly from code.
var workflow = new Sequence
{
    Activities =
    {
        new WriteLine("Hello World!"),
        new Delay(TimeSpan.FromSeconds(1)),
        new WriteLine("It is nice to meet you!")
    }
};
```

<figure><img src=".gitbook/assets/hello-world-designer.png" alt=""><figcaption><p>Design workflows visually</p></figcaption></figure>

### Use Cases

1. **Integrate Workflow Execution in .NET Applications**: By embedding Elsa Workflows directly into your .NET applications, you can enhance the app's capabilities by managing complex logic and automation tasks seamlessly within the app’s existing infrastructure.
2. **Standalone Workflow Server**: Deploy Elsa as an independent workflow server to manage various business processes across your organization, providing a centralized solution for handling workflows without integrating directly into existing applications.
3. **Microservices Architecture**: Utilize Elsa Workflows within a microservice platform to coordinate and execute workflows across different distributed services, ensuring efficient communication and process automation among microservices.
4. **Business Process Automation**: Automate repetitive business processes such as approval workflows, notifications, and scheduling tasks, thereby improving productivity and reducing manual intervention.
5. **Task Automation**: Automate repetitive tasks, from data entry to report generation.
6. **Integration Workflows**: Connect disparate systems and ensure smooth data flow between them.
7. Alerts & Monitoring: Set up workflows to monitor systems and send alerts or take corrective actions automatically.

### Key Features

1. **Long & Short Running Workflows**: Whether you require a workflow that spans over days, waiting for user input, or one that completes in milliseconds, Elsa robustly manages both scenarios with ease.
2. **Activity Library**: Elsa offers a rich set of out-of-the-box activities, providing essential building blocks to construct flexible and effective workflows tailored to your business needs.
3. **Triggers**: Workflows can be initiated automatically based on specific events or conditions, enabling seamless automation and integration with existing processes.
4. **Dynamic Expressions**: Utilizing C#, JavaScript, or Liquid expressions, Elsa supports dynamic evaluation of values during runtime, offering adaptability and precision in workflow logic. You can even bring your own, since pretty much every aspect of Elsa is modular and extensible.
5. **Extensibility**: For unique requirements, Elsa is designed with extensibility in mind, allowing you to introduce custom activities or integrate effortlessly with other systems.
6. **Web-Based Designer**: With the web-based drag & drop designer hosted in Elsa Studio, users can visually create workflows, leveraging a modular and extensible framework built with Blazor.
7. **Distributed Execution**: Elsa is built for scalability, enabling massive throughput by running on multiple nodes within a cluster.

### Glossary

Here is a glossary of commonly used terms in Elsa:

* **Workflow**: A workflow is a sequence of one or more activities that can be executed. In Elsa, it is represented by an instance of the `Workflow` class and is also considered an activity. The Workflow class features a `Root` property of type `IActivity`, which is scheduled for execution when the workflow starts.
* **Workflow Instance**: This represents a database-persisted instance of a workflow in execution, encapsulated by the `WorkflowInstance` class.
* **Activity**: An activity is a unit of work executed by the workflow engine. In Elsa, these are classes implementing the `IActivity` interface and can be linked to form a workflow.
* **Bookmark**: A bookmark signifies a pause point in a workflow, enabling the workflow to be resumed later. It is typically created by blocking activities such as the `Event` or `Delay` activity.
* **Burst of Execution**: This term describes the period during which the workflow runner actively executes activities. A workflow executing continuously from start to finish occurs in a single burst, whereas a workflow interrupted by a blocking activity results in multiple bursts, resuming on subsequent triggers.
* **Blocking Activity**: Blocking activities are those which do not complete execution immediately upon initiation. They often create bookmarks, halting the workflow's progress until resumed. This halting nature coins the term "blocking."

### Known Limitations

Elsa is continually evolving, and while it offers powerful capabilities, there are some known limitations and ongoing work:

* Documentation is still a work in progress.
* The designer is not yet fully embeddable in other applications; this feature is planned for a future release.
* C# and Python expressions are not yet fully tested.
* Starting workflows from the designer is currently supported only for workflows that do not require input and do not start with a trigger; this is planned for a future release.
* The designer currently only supports Flowchart activities. Support for Sequence and StateMachine activities is planned for a future release.
* UI input validation is not yet implemented.

