---
description: >-
  This section provides a comprehensive overview of fundamental principles and
  key elements that form the foundation of Elsa.
---

# Concepts

### Workflow

A workflow is a sequence of steps called **activities** that represents a process. Workflows can be created visually or programmatically.

### Activity

An activity represents an individual step on a workflow. Examples are the `WriteLine` and `SendHttpRequest` activities.

### Correlation ID

A Correlation ID is a flexible identifier linking related workflows and external entities. It aids in tracing workflows in distributed, asynchronous, or hierarchical systems. Assigning a Correlation ID allows tracking of related workflows and ties them to specific business objects like documents, customers, or orders.

### Outcome

Activities in a flowchart are connected that defines the logic of the workflow. Each activity can have one ore more _potential_ results, which are referred to as _outcomes_. These outcomes are visually displayed as "ports" on the activity. For example, the `Decision` activity has two potential outcomes: `True` and `False`. When using the designer, the user can connect a subsequent activity to these outcomes. This powerful mechanism simplifies workflows by eliminating the need for separate decision activities to evaluate an activity's result.

### Output

In a workflow, activities can create _output_ data for use in later steps. Outputs help pass information, like numbers, text, or objects, to the next activity for further use or storage. Activities can yield both outcomes and outputs. While you can only generate outputs, such as a boolean, for decision-making in future steps, it's simpler to use outcomes for these cases. Use outputs for data like database results when the workflow doesn’t need to make decisions based.
