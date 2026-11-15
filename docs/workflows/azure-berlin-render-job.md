# Run a Berlin render as an Azure Container Apps Job

This pilot runs the existing Linux renderer image once, on demand, in Azure dev.
It renders only Berlin, publishes SQLite and PMTiles to the private `regions`
container, and writes the catalog and Berlin manifest to the private `metadata`
container. The job uses the image's full region configuration, so the catalog
continues to include the other configured regions.

## Prerequisites

- Use the dev subscription and resource group (normally `rg-poineer-dev`). The
  Bicep template expects an existing workload profiles Container Apps environment
  with the Consumption profile, a dev Azure Container Registry (ACR), and the
  dev storage account, all in the same resource group. Use the storage region
  for the environment. See [Azure dataset storage](azure-dataset-storage.md).
- The dev storage account needs private `regions` and `metadata` containers.
  If `metadata` does not exist yet, create it with
  `az storage container create --account-name poineerstoragedev --name metadata --auth-mode login`.
  The dev API from [Server #67](https://github.com/christian-hofmeister/poineer-server/issues/67)
  should read that same metadata container.
- The template uses `AcrPull` for registry access. Use a registry configured for
  RBAC Registry Permissions and with ARM audience authentication enabled for
  managed identity pulls. The operator needs deployment and role assignment
  permissions for the dev resources.
- Keep the job as the only writer to these dev publication targets during the
  pilot. Start one execution at a time. The API can read the same target.

## Push the existing image

From a machine that has the versioned renderer image, push it to the private
dev registry. The example uses the release image from the VPS.

```bash
registry='<dev-registry-name>'
tag='v0.4.1'
server=$(az acr show --name "$registry" --query loginServer -o tsv)
az acr login --name "$registry"
docker tag "poineer-render:$tag" "$server/poineer-render:$tag"
docker push "$server/poineer-render:$tag"
az acr repository show --name "$registry" --image "poineer-render:$tag" --query digest -o tsv
```

Use the returned registry digest for deployment. If Docker requires root on
the VPS, run Docker and the registry login with consistent Docker credential
configuration.

## Deploy and start the job

Run from the repository root with Azure CLI authenticated to dev:

```bash
group='rg-poineer-dev'
environment='<dev-container-apps-environment>'
registry='<dev-registry-name>'
digest='sha256:<digest-from-acr>'
location=$(az containerapp env show --name "$environment" --resource-group "$group" --query location -o tsv)
job='poineer-render-berlin-dev'

az deployment group what-if --resource-group "$group" \
  --template-file infra/azure/berlin-render-job.bicep \
  --parameters environmentName="$environment" registryName="$registry" \
    location="$location" rendererImage="poineer-render@$digest"

az deployment group create --resource-group "$group" \
  --template-file infra/azure/berlin-render-job.bicep \
  --parameters environmentName="$environment" registryName="$registry" \
    location="$location" rendererImage="poineer-render@$digest"

execution=$(az containerapp job start --name "$job" --resource-group "$group" --query name -o tsv)
az containerapp job logs show --name "$job" --resource-group "$group" \
  --execution "$execution" --container renderer --format text --follow
az containerapp job execution show --name "$job" --resource-group "$group" \
  --job-execution-name "$execution" \
  --query '{status:properties.status,started:properties.startTime,finished:properties.endTime}'
```

The job starts with 4 vCPUs, 8 GiB RAM, one replica, a 30-minute timeout, and
no automatic retries. The identity can pull the image and write to the two
dev containers. If the first pull or Blob request is denied immediately after
deployment, allow time for the role assignments to propagate before retrying.

## Verify and measure

- Confirm the execution reaches `Succeeded` and logs show only Berlin being
  rendered, verification of both artifacts, and successful manifest
  publication. The manifest is at
  `metadata/geofabrik/europe/germany/berlin/manifest.json`. Check that both
  `objectKey` paths exist under `regions`, with the byte sizes and SHA-256
  checksums recorded in the manifest.
- Once Server #67 is deployed, query `GET /api/datasets` on the dev API and
  confirm it exposes the same Berlin release. The render and Blob checks can
  be completed before that API is available.
- Record execution start and finish times. In Azure Monitor Metrics for the
  job, filter by the execution name and record Average and Max
  `UsageNanoCores` and Max `UsageBytes`. Divide nanocores by 1,000,000,000 to
  get vCPUs. The one-minute samples can miss short peaks.
  [Azure Monitor job metrics](https://learn.microsoft.com/en-us/azure/azure-monitor/reference/supported-metrics/microsoft-app-jobs-metrics)
- Record the largest `scratch_kib` value in the console log. This is a sampled
  size of the job's scratch directory, including the downloaded PBF, Planetiler
  sources, and intermediate files. Each execution starts with empty scratch.
  The Consumption profile has an ephemeral storage limit as well as a RAM
  limit. If the job runs out of disk, assess a larger scratch volume before
  scaling to other regions.
  [Azure Container Apps storage](https://learn.microsoft.com/en-us/azure/container-apps/storage-mounts)
- Report the image digest, execution name, output sizes, runtime, CPU, RAM, and
  scratch high-water mark in [renderer #237](https://github.com/christian-hofmeister/poineer-render/issues/237).
  Label this as a cold run when comparing it with the warm VPS baseline.

After recording the results, the manual job can remain idle for a rerun. If
retiring the pilot, remove the job and its dedicated identity and role
assignments. Preserve the shared dev environment, registry, and blobs that
are still used by the API.
