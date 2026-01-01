# ForEach

The **ForEach** activity iterates over a collection of items, executing its body activity for each item in the collection. It is analogous to C#'s `foreach` statement and is useful for processing collections of data, performing batch operations, or handling multiple items in a workflow.

## Properties

| Name           | Type                 | Description                                                                                                       |
| -------------- | -------------------- | ----------------------------------------------------------------------------------------------------------------- |
| `Items`        | `Input<ICollection>` | The collection to iterate over. Can be a list, array, or any enumerable collection.                              |
| `Body`         | `IActivity`          | The activity (or activities) to execute for each item in the collection. Can be a single activity or a Sequence. |
| `CurrentValue` | `Output<object>`     | A variable reference that receives the current item value during each iteration.                                  |
| `Mode`         | `ForEachMode`        | Determines execution mode: `Sequential` (default) or `Parallel` for concurrent processing.                       |

## Usage

### Sequential ForEach

The following workflow demonstrates basic sequential iteration over a collection of order IDs:

{% code fullWidth="false" %}
```csharp
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Contracts;

namespace ElsaServer.Workflows;

public class ProcessOrdersWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Name = "Process Orders Workflow";

        // Define a collection of order IDs
        var orderIds = builder.WithVariable<List<string>>()
            .WithValue(new List<string> { "ORD-001", "ORD-002", "ORD-003" });
        
        // Define a variable to hold the current order ID
        var currentOrderId = builder.WithVariable<string>();

        builder.Root = new Sequence
        {
            Activities =
            {
                new WriteLine("Starting order processing..."),
                
                new ForEach<string>
                {
                    Items = new(orderIds),
                    CurrentValue = new(currentOrderId),
                    Body = new Sequence
                    {
                        Activities =
                        {
                            new WriteLine(context => $"Processing order: {currentOrderId.Get(context)}"),
                            // Add more activities here to process each order
                            new WriteLine(context => $"Order {currentOrderId.Get(context)} completed")
                        }
                    }
                },
                
                new WriteLine("All orders processed successfully")
            }
        };
    }
}
```
{% endcode %}

### Parallel ForEach

For improved performance when processing independent items, use parallel mode:

{% code fullWidth="false" %}
```csharp
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Contracts;

namespace ElsaServer.Workflows;

public class ParallelOrderProcessingWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Name = "Parallel Order Processing";

        var orderIds = builder.WithVariable<List<string>>()
            .WithValue(new List<string> { "ORD-001", "ORD-002", "ORD-003", "ORD-004" });
        
        var currentOrderId = builder.WithVariable<string>();

        builder.Root = new Sequence
        {
            Activities =
            {
                new WriteLine("Processing orders in parallel..."),
                
                new ForEach<string>
                {
                    Items = new(orderIds),
                    CurrentValue = new(currentOrderId),
                    Mode = ForEachMode.Parallel, // Enable parallel execution
                    Body = new Sequence
                    {
                        Activities =
                        {
                            new WriteLine(context => $"[Parallel] Processing order: {currentOrderId.Get(context)}"),
                            new Delay(TimeSpan.FromSeconds(1)), // Simulate processing time
                            new WriteLine(context => $"[Parallel] Order {currentOrderId.Get(context)} completed")
                        }
                    }
                },
                
                new WriteLine("All orders processed in parallel")
            }
        };
    }
}
```
{% endcode %}

### Using ForEach with Custom Activities

The ForEach activity works seamlessly with custom activities. Here's an example that processes entities from a custom activity:

{% code fullWidth="false" %}
```csharp
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Contracts;

namespace ElsaServer.Workflows;

public class CodeGenerationWorkflow : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.Name = "Code Generation Workflow";

        // Variable to hold the current entity being processed
        var currentEntity = builder.WithVariable<EntityDefinition>();
        
        // Variable to store the list of entities (populated by custom activity)
        var entities = builder.WithVariable<List<EntityDefinition>>();

        builder.Root = new Sequence
        {
            Activities =
            {
                // Custom activity that returns a list of entities
                new LoadEntitiesActivity
                {
                    Result = new(entities)
                },
                
                // Iterate over each entity
                new ForEach<EntityDefinition>
                {
                    Items = new(entities),
                    CurrentValue = new(currentEntity),
                    Body = new Sequence
                    {
                        Activities =
                        {
                            // Pass the current entity to custom activities
                            new GenerateCodeActivity
                            {
                                Entity = new(currentEntity)
                            },
                            
                            new ValidateCodeActivity
                            {
                                Entity = new(currentEntity)
                            },
                            
                            new WriteLine(context => 
                                $"Generated and validated code for entity: {currentEntity.Get(context)?.Name}")
                        }
                    }
                },
                
                new WriteLine("Code generation completed for all entities")
            }
        };
    }
}
```
{% endcode %}

### Alternative Syntax Using Lambda Expressions

You can also use lambda expressions to define the items collection inline:

{% code fullWidth="false" %}
```csharp
new ForEach<int>
{
    Items = new(context => new List<int> { 1, 2, 3, 4, 5 }),
    CurrentValue = new(currentValue),
    Body = new WriteLine(context => 
        $"Processing item: {currentValue.Get(context)}")
}
```
{% endcode %}

Or pass the variable directly:

{% code fullWidth="false" %}
```csharp
new ForEach<string>
{
    Items = new(context => myItemsVariable.Get(context)),
    CurrentValue = new(currentItem),
    Body = new WriteLine(context => 
        $"Current item: {currentItem.Get(context)}")
}
```
{% endcode %}

## Using ForEach in Designer

When working with ForEach in Elsa Studio's designer:

1. **Add the ForEach Activity**: 
   - From the activity picker, select **Control Flow** → **ForEach**
   - Drag it onto your workflow canvas

2. **Configure the Items Collection**:
   - In the properties panel, configure the **Items** property
   - You can use an expression (C#, JavaScript, or Liquid) to specify the collection
   - Example: `Variables.OrderIds` or `new List<string> { "A", "B", "C" }`

3. **Set CurrentValue Variable**:
   - Create or select a workflow variable to hold the current item
   - The variable's type should match the collection's element type
   - Assign it to the **CurrentValue** property

4. **Design the Body**:
   - The **Body** property defines what activities run for each item
   - Add a **Sequence** activity to the body if you need multiple activities per iteration
   - Use the CurrentValue variable in expressions within the body activities

5. **Configure Execution Mode** (Optional):
   - Set the **Mode** property to `Sequential` (default) or `Parallel`
   - Use parallel mode when items can be processed independently

## Common Patterns

### Processing with Conditional Logic

Combine ForEach with Decision activities to implement conditional processing:

{% code fullWidth="false" %}
```csharp
new ForEach<Order>
{
    Items = new(orders),
    CurrentValue = new(currentOrder),
    Body = new Sequence
    {
        Activities =
        {
            new Decision
            {
                Condition = new(context => 
                    currentOrder.Get(context).Total > 1000),
                Then = new WriteLine("High-value order - applying premium processing"),
                Else = new WriteLine("Standard order - applying normal processing")
            }
        }
    }
}
```
{% endcode %}

### Accumulating Results

Collect results from each iteration into a list:

{% code fullWidth="false" %}
```csharp
var items = builder.WithVariable<List<string>>()
    .WithValue(new List<string> { "Item1", "Item2", "Item3" });

var currentItem = builder.WithVariable<string>();
var results = builder.WithVariable<List<string>>()
    .WithValue(new List<string>());

builder.Root = new Sequence
{
    Activities =
    {
        new ForEach<string>
        {
            Items = new(items),
            CurrentValue = new(currentItem),
            Body = new Sequence
            {
                Activities =
                {
                    // Process the item
                    new WriteLine(context => $"Processing: {currentItem.Get(context)}"),
                    
                    // Add result to collection
                    new SetVariable
                    {
                        Variable = results,
                        Value = new(context =>
                        {
                            var list = results.Get(context);
                            list.Add($"Processed: {currentItem.Get(context)}");
                            return list;
                        })
                    }
                }
            }
        },
        
        new WriteLine(context => 
            $"Total processed: {results.Get(context).Count}")
    }
};
```
{% endcode %}

### Nested ForEach Loops

Process hierarchical data structures:

{% code fullWidth="false" %}
```csharp
var departments = builder.WithVariable<List<Department>>();
var currentDepartment = builder.WithVariable<Department>();
var currentEmployee = builder.WithVariable<Employee>();

builder.Root = new ForEach<Department>
{
    Items = new(departments),
    CurrentValue = new(currentDepartment),
    Body = new Sequence
    {
        Activities =
        {
            new WriteLine(context => 
                $"Processing department: {currentDepartment.Get(context).Name}"),
            
            new ForEach<Employee>
            {
                Items = new(context => 
                    currentDepartment.Get(context).Employees),
                CurrentValue = new(currentEmployee),
                Body = new WriteLine(context => 
                    $"  - Employee: {currentEmployee.Get(context).Name}")
            }
        }
    }
};
```
{% endcode %}

## Best Practices

1. **Choose the Right Execution Mode**:
   - Use `Sequential` mode when order matters or items share state
   - Use `Parallel` mode for independent operations (API calls, file processing)
   - Be aware that parallel mode uses the .NET thread pool

2. **Variable Scope**:
   - The `CurrentValue` variable is scoped to the ForEach body
   - To use values outside the loop, store them in workflow-level variables

3. **Performance Considerations**:
   - For large collections (1000+ items), consider batching
   - Parallel mode may not improve performance for CPU-bound operations
   - Monitor resource usage when using parallel mode

4. **Error Handling**:
   - If an iteration throws an exception, the workflow enters a faulted state
   - Use try-catch patterns in custom activities within the body
   - Configure [incident handling strategies](../../operate/incidents/) for automatic retry

5. **Empty Collections**:
   - If the Items collection is empty or null, the body never executes
   - The workflow continues to the next activity after the ForEach
   - Use validation before ForEach if empty collections are problematic

6. **Accessing Collection Metadata**:
   - To access the current index, use a counter variable that increments in the body
   - To access the total count, get the count before entering the loop

## Troubleshooting

### CurrentValue is Always the First Item

**Symptom**: The CurrentValue variable shows the same (first) item in every iteration.

**Possible Causes**:
- Incorrect variable reference in the CurrentValue property
- Captured closure in lambda expressions

**Solution**:
```csharp
// ❌ Wrong: Incorrect usage without context
Body = new WriteLine(() => $"Item: {currentItem}")

// ✅ Correct: Use context to get current value
Body = new WriteLine(context => $"Item: {currentItem.Get(context)}")
```

### Parallel Mode Causing Race Conditions

**Symptom**: Shared variables have incorrect values after parallel execution.

**Solution**:
- Avoid modifying shared variables from parallel iterations
- Use separate variables for each iteration's results
- Aggregate results after the ForEach completes

### ForEach Not Executing

**Symptom**: The body activities never execute.

**Possible Causes**:
- Items collection is null or empty
- Items property not configured correctly

**Solution**:
- Add a WriteLine before ForEach to check the collection count
- Verify the expression evaluates to a valid collection
- Check that the collection variable is populated

## See Also

- [Parallel Execution](../parallel-execution.md) - Detailed guide on parallel processing patterns
- [Decision Activity](./decision.md) - Conditional logic within loops
- [Common Properties](../common-properties.md) - Properties shared by all activities
- [Workflow Patterns](../../guides/patterns/README.md) - Advanced patterns including fan-out/fan-in
- [Variables](../../operate/workflow-instance-variables.md) - Working with workflow variables
