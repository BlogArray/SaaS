# BlogArray.SaaS Platform

## Project Description

BlogArray.SaaS is an open-source multi-tenant SaaS platform designed to empower .NET developers to build, deploy, and manage scalable SaaS applications effortlessly. Built with ASP.NET Core 9, this platform leverages OpenIddict for identity management and Finbuckle.MultiTenant for multi-tenant support. It provides developers with a foundational solution for handling tenant management, authentication, authorization, and tenant-specific functionality, saving time and reducing complexity.

The platform consists of three main applications:

1. **BlogArray.SaaS.Identity**: An identity server built on top of OpenIddict.
2. **BlogArray.SaaS.TenantSuite**: A management application for tenants, users, roles, and scopes.
3. **BlogArray.SaaS.App**: A demonstration of multi-tenant functionality. This application only supports **Multiple Database - Complete Data Isolation**. Feel free to customize the app for your desired approach.

> **Note:** The project is currently in Proof of Concept (PoC) mode, so there may be occasional mistakes. Contributions and feedback are welcome.

---

## Key Features

- **Multi-Tenant Support**: Seamlessly manage multiple tenants using Finbuckle.MultiTenant.
- **Identity Management**: Built-in identity server leveraging OpenIddict for authentication and authorization.
- **Scalability**: Designed to support scalable SaaS applications.
- **Flexibility**: Easily customizable for different business needs.
- **Caching**: Supports SQL Server or Redis for optimized performance.
- **Tenant-Specific Media Storage**: Save tenant-specific media files securely using Azure Blob Storage.

---

## Technologies Used

- **ASP.NET Core 9**
- **OpenIddict**
- **Finbuckle.MultiTenant**
- **Entity Framework Core**
- **SQL Server / Redis** (for caching)
- **Azure Blob Storage** (for tenant-specific media storage)

---

## Getting Started

To get started with BlogArray.SaaS, follow these steps:

### Prerequisites

Ensure you have the following installed:

- [.NET SDK 9](https://dotnet.microsoft.com/)
- [SQL Server](https://www.microsoft.com/en-us/sql-server/) or [Redis](https://redis.io/)
- [Azure Storage Account](https://azure.microsoft.com/en-us/services/storage/blobs/) (for media storage)
- [Node.js](https://nodejs.org/) (if using frontend integrations)
- Git
- [Visual Studio 2022](https://visualstudio.microsoft.com/) with the ASP.NET and web development workload installed

### Installation Steps

1. **Clone the Repository**

   ```bash
   git clone https://github.com/BlogArray/SaaS.git
   ```

2. **Navigate to the Project Directory**

   ```bash
   cd BlogArray/SaaS
   ```

3. **Restore Dependencies**

   ```bash
   dotnet restore
   ```

4. **Configure the Application**
   Update the `appsettings.json` file in each application directory with the following configurations:

   ```json
   {
     "AllowedHosts": "*",
     "IPSafeList": "127.0.0.1;192.168.1.5;::1",
     "ConnectionStrings": {
       "IdentityContext": "Data Source=.;Initial Catalog=BlogArray.SaaS.Identity;User Id=sa; Password=welcome;TrustServerCertificate=True;MultipleActiveResultSets=True"
     },
     "AzureBlobStorage": {
       "ConnectionString": "UseDevelopmentStorage=true",
       "ContainerName": "multi-tenant"
     },
     "Cache": {
       "Type": "SqlServer", //SqlServer or Redis
       "ConnectionString": "Data Source=.;Initial Catalog=DistCache;User Id=sa; Password=welcome;TrustServerCertificate=True;MultipleActiveResultSets=True",
       "SlidingExpirationInMinutes": 30,
       "AbsoluteExpirationInHours": 6
     },
     "Links": {
       "Suite": "https://www.console.blogarray.dev/",
       "Identity": "https://www.id.blogarray.dev/",
       "Admin": "https://www.admin.blogarray.dev/",
       "Issuer": "https://www.id.blogarray.dev/",
       "Authority": "https://www.id.blogarray.dev/"
     },
     "SMTP": {
       "FromEmail": "noreply@app.com",
       "FromName": "App Development",
       "Username": "localhost",
       "Password": "ttczmtxemkinbzxv",
       "Host": "localhost",
       "Port": 587,
       "EnableSsl": false
     },
     "Defaults": {
       "DefaultLogoUrl": "https://www.id.blogarray.dev/_content/BlogArray.SaaS.Resources/resources/images/blogarray-full-logo.png",
       "DefaultFaviconUrl": "https://www.id.blogarray.dev/_content/BlogArray.SaaS.Resources/resources/images/blogarray-icon.png"
     }
   }
   ```

5. **Apply Migrations**
   Run the following command in each application directory that uses a database:

   ```bash
   dotnet ef database update
   ```

6. **Update Hosts File**
   To enable a real-time experience, update the `hosts` file at `C:\Windows\System32\drivers\etc\hosts` with the following entries:

   ```plaintext
   127.0.0.1 blogarray.dev
   127.0.0.1 www.blogarray.dev
   127.0.0.1 app.blogarray.dev
   127.0.0.1 www.app.blogarray.dev
   127.0.0.1 id.blogarray.dev
   127.0.0.1 www.id.blogarray.dev
   127.0.0.1 console.blogarray.dev
   127.0.0.1 www.console.blogarray.dev
   127.0.0.1 admin.blogarray.dev
   127.0.0.1 www.admin.blogarray.dev
   127.0.0.1 auth.blogarray.dev
   127.0.0.1 www.auth.blogarray.dev
   ```

7. **Run Multiple Applications in Visual Studio**

   - Open the `BlogArray.SaaS.sln` solution in Visual Studio.
   - Set multiple startup projects by:
     1. Right-click the solution in Solution Explorer and select **Properties**.
     2. In the **Common Properties** -> **Startup Project** tab, choose **Multiple startup projects**.
     3. Set the **Action** to `Start` for `BlogArray.SaaS.Identity`, `BlogArray.SaaS.TenantSuite`, and `BlogArray.SaaS.App`.
     4. Click **OK**.
   - Press **F5** to run all applications simultaneously. Each application will launch in its configured domain.

---

## Configuration

- **Multi-Tenant Configuration**: Define tenants in the `appsettings.json` of the `TenantSuite` application.
- **Identity Server**: Configure client secrets and scopes in `BlogArray.SaaS.Identity`.
- **Caching**: Enable and configure either SQL Server or Redis for caching in the `appsettings.json`.
- **Azure Blob Storage**: Configure the Azure Blob Storage connection string and container name in `appsettings.json` for tenant-specific media storage.
- **Multi-Tenant Strategy Configuration**: BlogArray.SaaS uses Finbuckle.MultiTenant's `Route` strategy for tenant identification by default. You can customize the strategy as per your requirements by referring to the [Finbuckle.MultiTenant Documentation](https://www.finbuckle.com/MultiTenant/Docs/v9.0.0/Introduction). Example of switching to the `Host` strategy:

   ```csharp
   builder.Services.AddMultiTenant<AppTenantInfo>()
       .WithHostStrategy()
       .WithDistributedCacheStore(TimeSpan.FromMinutes(5))
       .WithPerTenantAuthentication();
   ```

   Refer to the documentation for more details and supported strategies.

---

## Running the Applications

- **BlogArray.SaaS.Identity**: Provides authentication and token issuance.
- **BlogArray.SaaS.TenantSuite**: Manage tenants, users, roles, and scopes.
- **BlogArray.SaaS.App**: Demonstrates tenant-specific functionality. This application only supports **Multiple Database - Complete Data Isolation**.

Run each application individually or all together using Visual Studio.

---

## Contributing

We welcome contributions to improve BlogArray.SaaS! To contribute:

1. Fork the repository.
2. Create a new branch: `git checkout -b feature/your-feature-name`.
3. Commit your changes: `git commit -m 'Add your feature'`.
4. Push the branch: `git push origin feature/your-feature-name`.
5. Open a pull request.

For detailed guidelines, see the [CONTRIBUTING.md](CONTRIBUTING.md).

---

## License

This project is licensed under the [MIT License](LICENSE).

---

## Acknowledgments

Special thanks to the creators and maintainers of:

- [ASP.NET Core](https://dotnet.microsoft.com/)
- [OpenIddict](https://github.com/openiddict/openiddict-core)
- [Finbuckle.MultiTenant](https://github.com/Finbuckle/Finbuckle.MultiTenant)
- [Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/)
- [Azure Blob Storage](https://azure.microsoft.com/en-us/services/storage/blobs/)

---

We hope BlogArray.SaaS helps you kickstart your SaaS development journey. If you have any questions or encounter issues, feel free to open an issue in the repository!

