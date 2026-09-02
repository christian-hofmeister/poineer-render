# Azure Dataset Storage

Issue #133 introduces the first cloud storage target for published POIneer datasets. The
renderer can still run on the VPS; Azure Blob Storage provides an independently managed
destination for validated dataset artifacts.

```text
VPS Renderer
     |
     | upload validated dataset artifact
     v
Azure Blob Storage
```

## Current Scope

This workflow provisions only storage:

- one Azure Storage Account
- one private Blob container for dataset artifacts
- cost-conscious local-redundant storage defaults
- secure access defaults suitable for a future automated publisher

It does not provision Azure compute, CDN, Azure Front Door, mobile direct-download behavior,
cleanup jobs, geo-redundant disaster recovery, or production secrets.

## Naming Strategy

The Bicep template derives the storage account name from:

```text
st{projectName}{environmentName}{uniqueSuffix}
```

For the production parameters this becomes a globally unique name shaped like:

```text
stpoineerprodxxxxxxxx
```

The suffix is generated from the subscription, resource group, project name, and environment.
This keeps names deterministic inside the same resource group while satisfying Azure Storage
Account global uniqueness requirements.

The dataset container is named:

```text
datasets
```

## Region

The production parameter file uses `germanywestcentral`. This keeps the initial dataset
storage geographically close to the expected European deployment and users while staying
simple for the MVP. The region is a parameter and can be changed before deployment if the
target subscription or organization prefers another Azure region.

## Security Defaults

The template configures the storage account and container with conservative defaults:

- anonymous Blob public access disabled at the storage account level
- container public access set to `None`
- HTTPS-only traffic
- minimum TLS version `TLS1_2`
- shared-key access disabled
- OAuth authentication preferred by default
- cross-tenant replication disabled
- 7-day soft delete for blobs and containers

The public network endpoint remains enabled for the initial hybrid architecture because the
renderer may run from the VPS outside Azure. Authorization should use Microsoft Entra ID and
RBAC rather than account keys. A future hardening pass can add firewall rules or private
networking once the upload identity and network path are known.

## Cost-Conscious Defaults

The initial template uses:

- `StorageV2`
- `Standard_LRS`
- Hot access tier
- no geo-replication
- no CDN or Front Door

Regional SQLite datasets are expected to be reproducible from OSM input, so locally redundant
storage is acceptable for the first cloud distribution target. This can be revisited when
dataset availability requirements become stricter.

## Deploy

Create or choose a resource group, then deploy the template:

```bash
az group create \
  --name rg-poineer-datasets-prod \
  --location germanywestcentral
```

```bash
az deployment group create \
  --resource-group rg-poineer-datasets-prod \
  --template-file infra/azure/storage.bicep \
  --parameters infra/azure/storage.prod.bicepparam
```

The deployment outputs:

- `storageAccountName`
- `datasetContainerName`
- `blobEndpoint`

Do not commit credentials, account keys, SAS tokens, connection strings, or tenant-specific
secrets. Publisher authentication should be configured outside source control.

## Verify

Confirm that the storage account and container exist:

```bash
az storage account show \
  --resource-group rg-poineer-datasets-prod \
  --name <storage-account-name> \
  --query "{name:name, location:location, allowBlobPublicAccess:allowBlobPublicAccess, allowSharedKeyAccess:allowSharedKeyAccess, httpsOnly:supportsHttpsTrafficOnly}" \
  --output table
```

```bash
az storage container show \
  --account-name <storage-account-name> \
  --name datasets \
  --auth-mode login \
  --query "{name:name, publicAccess:properties.publicAccess}" \
  --output table
```

The expected container public access value is `None`.

## Future Publisher Use

A future Azure Blob publisher can use this storage account as its target behind the existing
`IDatasetPublisher` abstraction. It should upload only validated dataset artifacts and should
pair with a blob-specific `IPublishedDatasetVerifier` implementation so a dataset is not
considered successfully published until the destination has been verified.

The renderer compute environment remains independent. The same storage target can receive
artifacts from the VPS renderer now and from an Azure-based renderer later.
