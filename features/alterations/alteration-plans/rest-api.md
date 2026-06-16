# REST API

The Alterations module exposes a REST API for submitting, inspecting, and dry-running alteration plans.

## Submit a plan

Send `POST /alterations/submit` with `alterations` plus a `filter`:

```http
POST /alterations/submit HTTP/1.1
Host: localhost:5001

{
  "alterations": [
    {
      "type": "ModifyVariable",
      "variableId": "83fde420b5794bc39a0a7db725405511",
      "value": "Hello world!"
    },
    {
      "type": "Migrate",
      "targetVersion": 9
    },
    {
      "type": "ScheduleActivity",
      "activityId": "mY1rb4GRjkW3urm8dcNSog"
    }
  ],
  "filter": {
    "workflowInstanceIds": [
      "88ce68d00e824c78a53af04f16d276ea"
    ]
  }
}
```

The response includes the generated or accepted plan ID:

```json
{
  "planId": "6cdc459867a94027a6f237417acf398f"
}
```

## Dry-run a filter

Use `POST /alterations/dry-run` to see which workflow instances a filter would target without creating a plan:

```http
POST /alterations/dry-run HTTP/1.1
Host: localhost:5001

{
  "definitionIds": ["order-processing"],
  "statuses": ["Running"],
  "isSystem": false
}
```

Example response:

```json
{
  "workflowInstanceIds": [
    "88ce68d00e824c78a53af04f16d276ea",
    "23f2c2585cb14f5bb7da4e2cc2d6f0cb"
  ]
}
```

## Get plan and job status

Use the plan ID to query the current plan and its jobs:

```bash
GET /alterations/6cdc459867a94027a6f237417acf398f HTTP/1.1
Host: localhost:5001
```

The response includes the stored plan and any generated jobs:

```json
{
  "plan": {
    "alterations": [
      {
        "type": "ModifyVariable",
        "variableId": "9b4cecbe82204102813ee968d517bc8a",
        "value": "Hello world!"
      },
      {
        "type": "ScheduleActivity",
        "activityId": "BK2-RkUrgkmMj3RIkKfh9g"
      }
    ],
    "workflowInstanceFilter": {
      "workflowInstanceIds": [
        "5d87afa152e54f88ac22e5d69ead6b69"
      ],
      "isSystem": false
    },
    "status": 2,
    "createdAt": "2023-10-04T22:34:31.28188+00:00",
    "startedAt": "2023-10-04T22:34:31.30000+00:00",
    "completedAt": "2023-10-04T22:34:31.44371+00:00",
    "id": "6cdc459867a94027a6f237417acf398f"
  },
  "jobs": [
    {
      "planId": "6cdc459867a94027a6f237417acf398f",
      "workflowInstanceId": "5d87afa152e54f88ac22e5d69ead6b69",
      "status": 2,
      "log": [
        {
          "message": "ModifyVariable succeeded",
          "logLevel": 2,
          "timestamp": "2023-10-04T22:34:31.407518+00:00"
        },
        {
          "message": "ScheduleActivity succeeded",
          "logLevel": 2,
          "timestamp": "2023-10-04T22:34:31.415783+00:00"
        }
      ],
      "createdAt": "2023-10-04T22:34:31.28188+00:00",
      "startedAt": "2023-10-04T22:34:31.39000+00:00",
      "completedAt": "2023-10-04T22:34:31.426614+00:00",
      "id": "92062c77cbcd419a87ac621886e5f60a"
    }
  ]
}
```

`status` values are serialized from Elsa's plan and job status enums. Use the plan timestamps and per-job logs to understand whether Elsa found matching instances, whether jobs have started, and which alterations succeeded or failed.
