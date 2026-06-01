---
description: >-
  Comprehensive guide to integrating Elsa Studio into different host frameworks including React, Angular, Blazor, and MVC/Razor Pages. Covers hosting patterns, configuration, and authentication.
---

# Studio Designer Integration by Host Framework

Elsa Studio is a flexible, framework-agnostic web application that can be integrated into various host frameworks. This guide covers integration patterns for React, Angular, Blazor, and ASP.NET Core MVC/Razor Pages, along with common configuration considerations.

## Overview

Elsa Studio provides a visual workflow designer that connects to Elsa Server (the backend API). Depending on your application architecture and technology stack, you can integrate Studio in several ways:

- **Separate Application**: Studio runs as a standalone app (recommended for most scenarios)
- **Iframe Integration**: Studio is loaded in an iframe with cross-origin communication
- **Blazor Server Host**: Studio runs from an ASP.NET Core Blazor Server host and connects to an Elsa Server backend
- **Custom Elements Host**: Studio workflow components are exposed as web components for host pages that can provide backend configuration

## Common Integration Approaches

Before diving into framework-specific details, let's understand the common patterns:

### 1. Separate App with Reverse Proxy

Studio and your application run as separate services, with a reverse proxy routing requests:

```
┌──────────────────────────────────────────┐
│          Reverse Proxy (nginx)           │
│                                          │
│  /studio/* ──> Elsa Studio (Port 5001)  │
│  /api/*    ──> Elsa Server (Port 5000)  │
│  /*        ──> Your App (Port 3000)     │
└──────────────────────────────────────────┘
```

**Advantages:**
- Clean separation of concerns
- Independent deployment and scaling
- No framework coupling
- Easy to update Studio independently

**Disadvantages:**
- Requires reverse proxy configuration
- CORS considerations for API calls
- Shared authentication must be configured

### 2. Iframe Embedding

Studio is loaded in an iframe within your application:

```
┌───────────────────────────────────────────┐
│         Your Application                  │
│                                           │
│  ┌─────────────────────────────────────┐  │
│  │  <iframe src="https://studio">     │  │
│  │      Elsa Studio                    │  │
│  │  </iframe>                          │  │
│  └─────────────────────────────────────┘  │
└───────────────────────────────────────────┘
```

**Advantages:**
- Simple integration
- Studio updates don't affect your app
- Clear security boundary

**Disadvantages:**
- Iframe restrictions (sizing, navigation)
- PostMessage required for communication
- CORS and authentication complexity

### 3. Blazor Server Host

Studio can be hosted by an ASP.NET Core Blazor Server application:

```
┌────────────────────────────────────────┐
│      ASP.NET Core Application          │
│                                        │
│  ┌──────────────┐  ┌────────────────┐ │
│  │ Your Pages   │  │ Elsa Studio    │ │
│  │ /dashboard   │  │ Blazor Server  │ │
│  └──────────────┘  └────────────────┘ │
│                                        │
│  Backend:Url ──> Elsa Server /elsa/api │
└────────────────────────────────────────┘
```

**Advantages:**
- Single deployment unit
- Shared authentication/authorization
- Server-side Blazor hosting for Studio
- Configuration through the same application settings

**Disadvantages:**
- Tighter coupling
- Shared resources (memory, CPU)
- Updates require full app deployment

## Configuration Common to All Patterns

Regardless of integration approach, you'll need to configure:

### Base API URL

Studio needs to know where Elsa Server's API is located:

```json
{
  "Backend": {
    "Url": "https://your-api.example.com/elsa/api"
  }
}
```

Or via the equivalent environment variable:

```bash
Backend__Url=https://your-api.example.com/elsa/api
```

### Authentication

Studio 3.7.0 selects the client-side authentication integration through `Authentication:Provider`.

#### Elsa Identity

Use Elsa Identity when users should sign in with credentials accepted by the Elsa backend:

```json
{
  "Backend": {
    "Url": "https://your-api.example.com/elsa/api"
  },
  "Authentication": {
    "Provider": "ElsaIdentity"
  }
}
```

#### OpenID Connect

Use OpenID Connect when Studio should acquire tokens from an external identity provider:

```json
{
  "Backend": {
    "Url": "https://your-api.example.com/elsa/api"
  },
  "Authentication": {
    "Provider": "OpenIdConnect",
    "OpenIdConnect": {
      "Authority": "https://login.microsoftonline.com/{tenant-id}/v2.0",
      "ClientId": "{client-id}",
      "AuthenticationScopes": [
        "openid",
        "profile",
        "offline_access"
      ],
      "BackendApiScopes": [
        "api://{backend-api-client-id}/elsa-server-api"
      ]
    }
  }
}
```

### Localization

Studio 3.7.0 hosts wire localization through the `Localization` section:

```json
{
  "Localization": {
    "DefaultCulture": "en-US",
    "SupportedCultures": [
      "en-GB",
      "nl-NL"
    ]
  }
}
```

### CORS Configuration

If Studio and Server are on different origins, configure CORS on the server:

```csharp
// Program.cs (Elsa Server)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowStudio", policy =>
    {
        policy.WithOrigins("https://studio.example.com")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();
app.UseCors("AllowStudio");
```

## React Integration

React applications typically integrate Studio as a separate service or via iframe.

### Pattern 1: Separate Service

**Studio Setup (Standalone Blazor):**
```bash
# Run Studio on port 5001
docker run -d -p 5001:8080 \
  -e Backend__Url=https://api.example.com/elsa/api \
  elsaworkflows/elsa-studio:latest
```

**React App:**
```tsx
// WorkflowsPage.tsx
import React from 'react';

export const WorkflowsPage: React.FC = () => {
  return (
    <div style={{ height: '100vh', width: '100%' }}>
      <h1>Workflow Designer</h1>
      <iframe
        src="https://studio.example.com"
        style={{
          width: '100%',
          height: 'calc(100vh - 60px)',
          border: 'none'
        }}
        title="Elsa Studio"
      />
    </div>
  );
};
```

**Navigation Integration:**
```tsx
// App.tsx
import { BrowserRouter, Routes, Route, Link } from 'react-router-dom';
import { WorkflowsPage } from './pages/WorkflowsPage';

function App() {
  return (
    <BrowserRouter>
      <nav>
        <Link to="/">Dashboard</Link>
        <Link to="/workflows">Workflows</Link>
      </nav>
      
      <Routes>
        <Route path="/" element={<Dashboard />} />
        <Route path="/workflows" element={<WorkflowsPage />} />
      </Routes>
    </BrowserRouter>
  );
}
```

### Pattern 2: Reverse Proxy

**Vite Config (Development):**
```typescript
// vite.config.ts
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/workflows': {
        target: 'http://localhost:5001',
        changeOrigin: true
      },
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true
      }
    }
  }
});
```

**Nginx (Production):**
```nginx
server {
    listen 80;
    server_name app.example.com;

    location / {
        proxy_pass http://react-app:3000;
    }

    location /workflows {
        proxy_pass http://elsa-studio:5001;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }

    location /api {
        proxy_pass http://elsa-server:5000;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
```

## Angular Integration

Angular follows similar patterns to React.

### Pattern 1: Iframe Integration

**Component:**
```typescript
// workflows.component.ts
import { Component } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';

@Component({
  selector: 'app-workflows',
  template: `
    <div class="workflows-container">
      <h1>Workflow Designer</h1>
      <iframe
        [src]="studioUrl"
        class="studio-frame"
        title="Elsa Studio">
      </iframe>
    </div>
  `,
  styles: [`
    .workflows-container { height: 100vh; display: flex; flex-direction: column; }
    .studio-frame { flex: 1; border: none; width: 100%; }
  `]
})
export class WorkflowsComponent {
  studioUrl: SafeResourceUrl;

  constructor(private sanitizer: DomSanitizer) {
    this.studioUrl = this.sanitizer.bypassSecurityTrustResourceUrl(
      'https://studio.example.com'
    );
  }
}
```

**Routing:**
```typescript
// app-routing.module.ts
import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { WorkflowsComponent } from './workflows/workflows.component';

const routes: Routes = [
  { path: '', component: DashboardComponent },
  { path: 'workflows', component: WorkflowsComponent }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
```

### Pattern 2: Proxy Configuration

**angular.json (Development):**
```json
{
  "projects": {
    "your-app": {
      "architect": {
        "serve": {
          "options": {
            "proxyConfig": "proxy.conf.json"
          }
        }
      }
    }
  }
}
```

**proxy.conf.json:**
```json
{
  "/workflows": {
    "target": "http://localhost:5001",
    "secure": false,
    "changeOrigin": true
  },
  "/api": {
    "target": "http://localhost:5000",
    "secure": false,
    "changeOrigin": true
  }
}
```

## Blazor Integration

Blazor offers the tightest hosting option because Elsa Studio itself is a Blazor application. In 3.7.0, Studio is wired through Blazor host services and normal ASP.NET Core endpoints, not through `AddElsaStudio` or `MapElsaStudio` helper APIs.

### Pattern 1: Blazor Server Studio Host

The Blazor Server host registers Razor Pages, Server-Side Blazor, Studio root custom elements, Studio modules, a remote backend, and the Blazor fallback page:

**Program.cs:**
```csharp
using Elsa.Studio.Authentication.ElsaIdentity.BlazorServer.Extensions;
using Elsa.Studio.Authentication.ElsaIdentity.HttpMessageHandlers;
using Elsa.Studio.Authentication.ElsaIdentity.UI.Extensions;
using Elsa.Studio.Contracts;
using Elsa.Studio.Core.BlazorServer.Extensions;
using Elsa.Studio.Dashboard.Extensions;
using Elsa.Studio.Extensions;
using Elsa.Studio.Localization.BlazorServer.Extensions;
using Elsa.Studio.Localization.Models;
using Elsa.Studio.Models;
using Elsa.Studio.Shell.Extensions;
using Elsa.Studio.Workflows.Designer.Extensions;
using Elsa.Studio.Workflows.Extensions;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor(options =>
{
    options.RootComponents.RegisterCustomElsaStudioElements();
    options.RootComponents.MaxJSRootComponents = 1000;
});

builder.Services.AddElsaIdentity();
builder.Services.AddElsaIdentityUI();

var backendApiConfig = new BackendApiConfig
{
    ConfigureBackendOptions = options => configuration.GetSection("Backend").Bind(options),
    ConfigureHttpClientBuilder = options =>
    {
        options.AuthenticationHandler = typeof(ElsaIdentityAuthenticatingApiHttpMessageHandler);
    }
};

var localizationConfig = new LocalizationConfig
{
    ConfigureLocalizationOptions = options => configuration.GetSection("Localization").Bind(options)
};

builder.Services.AddCore();
builder.Services.AddShell(options => configuration.GetSection("Shell").Bind(options));
builder.Services.AddRemoteBackend(backendApiConfig);
builder.Services.AddDashboardModule();
builder.Services.AddWorkflowsModule();
builder.Services.AddLocalizationModule(localizationConfig);

var app = builder.Build();

app.UseElsaLocalization();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
```

**appsettings.json:**

```json
{
  "Shell": {
    "DisableAuthorization": false
  },
  "Backend": {
    "Url": "https://localhost:5001/elsa/api"
  },
  "Authentication": {
    "Provider": "ElsaIdentity"
  },
  "Localization": {
    "DefaultCulture": "en-US",
    "SupportedCultures": [
      "en-US"
    ]
  }
}
```

**Navigation Integration:**
```razor
<!-- Shared/NavMenu.razor -->
<div class="nav-menu">
    <nav>
        <ul>
            <li>
                <NavLink href="/" Match="NavLinkMatch.All">
                    Dashboard
                </NavLink>
            </li>
            <li>
                <NavLink href="/workflows">
                    Workflows
                </NavLink>
            </li>
        </ul>
    </nav>
</div>
```

### Pattern 2: Workflow Custom Elements

The 3.7.0 custom-elements host registers workflow-level elements for pages that need to embed specific Studio surfaces:

```csharp
builder.RootComponents.RegisterCustomElsaStudioElements();
builder.RootComponents.RegisterCustomElement<BackendProvider>("elsa-backend-provider");
builder.RootComponents.RegisterCustomElement<WorkflowDefinitionEditorWrapper>("elsa-workflow-definition-editor");
builder.RootComponents.RegisterCustomElement<WorkflowInstanceViewerWrapper>("elsa-workflow-instance-viewer");
builder.RootComponents.RegisterCustomElement<WorkflowInstanceListWrapper>("elsa-workflow-instance-list");
builder.RootComponents.RegisterCustomElement<WorkflowDefinitionListWrapper>("elsa-workflow-definition-list");
```

These are specific workflow components. Studio 3.7.0 does not expose a generic `<ElsaStudioRoot />` component for arbitrary host pages.

## MVC / Razor Pages Integration

ASP.NET Core MVC and Razor Pages applications can link to a Studio Blazor host, put Studio behind the same reverse proxy, or display Studio in an iframe. If the same ASP.NET Core process hosts Studio, it still needs the Blazor Server setup shown above.

### Pattern 1: Link or Reverse Proxy to Studio

**View Integration:**
```cshtml
<!-- Views/Shared/_Layout.cshtml -->
<!DOCTYPE html>
<html>
<head>
    <title>@ViewData["Title"]</title>
</head>
<body>
    <nav>
        <ul>
            <li><a asp-controller="Home" asp-action="Index">Home</a></li>
            <li><a href="/workflows">Workflows</a></li>
        </ul>
    </nav>
    
    <main>
        @RenderBody()
    </main>
</body>
</html>
```

Configure the web server or reverse proxy so `/workflows` routes to the Studio host.

### Pattern 2: Iframe in View

```cshtml
<!-- Views/Workflows/Index.cshtml -->
@{
    ViewData["Title"] = "Workflows";
}

<h1>Workflow Designer</h1>

<div style="height: calc(100vh - 100px);">
    <iframe
        src="/workflows"
        style="width: 100%; height: 100%; border: none;"
        title="Elsa Studio">
    </iframe>
</div>
```

## Authentication Integration

### Elsa Identity

For username/password authentication against the Elsa backend, register the Elsa Identity Studio package for the host type and select the provider in configuration:

```csharp
builder.Services.AddElsaIdentity();
builder.Services.AddElsaIdentityUI();
```

```json
{
  "Authentication": {
    "Provider": "ElsaIdentity"
  }
}
```

### OpenID Connect

For an external identity provider, register OpenID Connect support and configure the authority, client, and backend API scopes:

```csharp
builder.Services.AddOpenIdConnectAuth(options =>
{
    configuration.GetSection("Authentication:OpenIdConnect").Bind(options);
});
```

```json
{
  "Authentication": {
    "Provider": "OpenIdConnect",
    "OpenIdConnect": {
      "Authority": "https://login.microsoftonline.com/{tenant-id}/v2.0",
      "ClientId": "{client-id}",
      "AuthenticationScopes": [
        "openid",
        "profile",
        "offline_access"
      ],
      "BackendApiScopes": [
        "api://{backend-api-client-id}/elsa-server-api"
      ]
    }
  }
}
```

**Client-Side Token Passing:**
> ⚠️ **Security Warning:**  
> Do **not** store authentication tokens in `localStorage` or `sessionStorage`, as they are accessible to JavaScript and vulnerable to XSS attacks.  
>
> **Recommended approaches:**  
> - Use HttpOnly, Secure cookies for authentication/session tokens.  
> - For OAuth2/OIDC, use the Authorization Code flow with PKCE and keep tokens only in memory.  
> - If using cookies, ensure CSRF protections are in place.  
>
> For more information, see [OWASP SPA Authentication Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/SPA_Authentication_Cheat_Sheet.html).
## Production Deployment Patterns

### Docker Compose

```yaml
version: '3.8'
services:
  elsa-server:
    image: your-elsa-server:latest
    environment:
      - ConnectionStrings__Default=...
      - ASPNETCORE_URLS=http://+:5000
    ports:
      - "5000:5000"

  elsa-studio:
    image: elsaworkflows/elsa-studio:latest
    environment:
      - Backend__Url=http://elsa-server:5000/elsa/api
      - Authentication__Provider=ElsaIdentity
    ports:
      - "5001:8080"

  your-app:
    image: your-app:latest
    ports:
      - "3000:3000"
    depends_on:
      - elsa-server

  nginx:
    image: nginx:alpine
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf
    ports:
      - "80:80"
    depends_on:
      - elsa-server
      - elsa-studio
      - your-app
```

### Kubernetes

```yaml
# elsa-studio-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: elsa-studio
spec:
  replicas: 2
  selector:
    matchLabels:
      app: elsa-studio
  template:
    metadata:
      labels:
        app: elsa-studio
    spec:
      containers:
      - name: studio
        image: elsaworkflows/elsa-studio:latest
        env:
        - name: Backend__Url
          value: "http://elsa-server-service/elsa/api"
        - name: Authentication__Provider
          value: "ElsaIdentity"
        ports:
        - containerPort: 8080
---
apiVersion: v1
kind: Service
metadata:
  name: elsa-studio-service
spec:
  selector:
    app: elsa-studio
  ports:
  - port: 80
    targetPort: 8080
```

## Troubleshooting

### CORS Issues

**Symptom:** API requests from Studio fail with CORS errors.

**Solution:**
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowStudio", policy =>
    {
        policy.WithOrigins("https://studio.example.com")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});
```

### Authentication Not Working

**Symptom:** Studio shows "Unauthorized" when loading workflows.

**Solution:** Check that:
1. Studio is configured with correct API URL
2. Authentication scheme matches server configuration
3. Tokens/cookies are being sent with requests
4. Server authentication middleware is properly configured

### Iframe Not Loading

**Symptom:** Studio doesn't load in iframe.

**Solution:** Check Content Security Policy:
```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.Add(
        "Content-Security-Policy",
        "frame-ancestors 'self' https://your-app.example.com");
    await next();
});
```

## Summary

Integrating Elsa Studio into your application depends on your framework and requirements:

- **React/Angular**: Use separate service with reverse proxy or iframe
- **Blazor**: Use the Blazor Server host setup with `AddCore`, `AddShell`, `AddRemoteBackend`, Studio modules, `MapBlazorHub`, and `MapFallbackToPage`
- **MVC/Razor Pages**: Link, reverse proxy, or iframe a Studio host; use the Blazor Server setup when hosting Studio in the same process
- **All Frameworks**: Configure `Backend:Url`, `Authentication:Provider`, localization, and CORS appropriately

For more detailed framework-specific guidance:
- **[Blazor Dashboard Integration](../../integration/blazor-dashboard.md)** - Comprehensive Blazor guide
- **[Custom UI Components](../custom-ui-components.md)** - Extending Studio UI
- **[Authentication & Authorization](../../authentication.md)** - Security configuration

Choose the pattern that best fits your architecture, deployment requirements, and team expertise.
