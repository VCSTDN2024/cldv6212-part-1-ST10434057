
# ABC Retail Storage App (Razor Pages, .NET 8)

This web app demonstrates Azure Table Storage, Blob Storage, Queue Storage, and Azure Files.
It includes a startup seeder that ensures resources exist and pre-populates:
- 5 Customers
- 5 Products
- 5 Queue messages
- 5 Contracts (.txt files)

## Prerequisites
- .NET 8 SDK
- Visual Studio 2022
- Azure subscription
- An Azure Storage account (standard is fine)

## Configuration
Open `appsettings.json` and set:
```
"AzureStorage": {
  "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=YOUR_ACCOUNT_NAME;AccountKey=YOUR_ACCOUNT_KEY;EndpointSuffix=core.windows.net",
  "TableNames": {
    "Customers": "Customers",
    "Products": "Products"
  },
  "BlobContainer": "product-images",
  "QueueName": "retail-events",
  "FileShare": "contracts"
}
```

## Run Locally
1. `dotnet restore`
2. `dotnet run`
3. Navigate to https://localhost:5001 (or shown URL).

## Deploy to Azure App Service
1. In Visual Studio: Right-click the project → **Publish** → **Azure** → **Azure App Service (Windows or Linux)**.
2. Create/Select a Resource Group and App Service.
3. In the App Service **Configuration** blade, add the same settings as in `appsettings.json` (or at least `AzureStorage:ConnectionString`). 
4. Publish and browse your site. Your URL should look like `http://<studentnumber>.azurewebsites.net`.
