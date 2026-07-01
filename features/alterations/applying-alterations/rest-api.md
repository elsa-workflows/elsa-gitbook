# REST API

The Alterations module exposes `POST /alterations/run` for immediate execution against explicit workflow instance IDs.

Use this endpoint when you already know the target instance IDs and want the results immediately. If you want Elsa to select instances from a filter and process them in the background, use [alteration plans](../alteration-plans/rest-api.md) instead.

All endpoints on this page require the `run:alterations` permission.

For example, to apply an alteration that modifies a variable, migrates the workflow instance to a new version, and schedules an activity, use the following request:

```http
POST /alterations/run HTTP/1.1
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
    "workflowInstanceIds": [
        "88ce68d00e824c78a53af04f16d276ea"
    ]
}
```

Unlike `/alterations/submit`, this endpoint does not accept a filter. It requires explicit `workflowInstanceIds`.

The response includes one result per targeted workflow instance:

```json
{
  "results": [
    {
      "workflowInstanceId": "88ce68d00e824c78a53af04f16d276ea",
      "workflowHasScheduledWork": true,
      "log": {
        "logEntries": [
          {
            "message": "ModifyVariable succeeded",
            "logLevel": 2,
            "timestamp": "2023-10-05T12:35:23.197167+00:00"
          },
          {
            "message": "Migrate succeeded",
            "logLevel": 2,
            "timestamp": "2023-10-05T12:35:23.202805+00:00"
          },
          {
            "message": "ScheduleActivity succeeded",
            "logLevel": 2,
            "timestamp": "2023-10-05T12:35:23.205629+00:00"
          }
        ]
      },
      "isSuccessful": true
    }
  ]
}
```

After the runner finishes, the endpoint automatically dispatches any successful workflow instance that still has scheduled work.

## Retry faulted activities

`release/3.8.0` also exposes `POST /alterations/workflows/retry` for retrying faulted activities on specified workflow instances.

If you omit `activityIds`, Elsa retries all incident activity IDs recorded on each specified workflow instance.

```http
POST /alterations/workflows/retry HTTP/1.1
Host: localhost:5001

{
  "workflowInstanceIds": [
    "88ce68d00e824c78a53af04f16d276ea"
  ]
}
```

To retry only specific activities, include `activityIds`:

```http
POST /alterations/workflows/retry HTTP/1.1
Host: localhost:5001

{
  "workflowInstanceIds": [
    "88ce68d00e824c78a53af04f16d276ea"
  ],
  "activityIds": [
    "ShipOrder",
    "CapturePayment"
  ]
}
```

The response shape matches `/alterations/run` by returning one result per targeted workflow instance.
