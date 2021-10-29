using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos.Table;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Web;

namespace SprintRetroServer
{
    public class SprintRetro
    {
        private static readonly CloudStorageAccount account = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("azureStorageAccount", EnvironmentVariableTarget.Process));

        private static readonly CloudTableClient client = account.CreateCloudTableClient();

        private static readonly CloudTable table = client.GetTableReference("retros");

        private static readonly string keyString = Environment.GetEnvironmentVariable("keyString", EnvironmentVariableTarget.Process);

        public SprintRetro()
        {
            table.CreateIfNotExists();
        }


        [FunctionName("CreateRetro")]
        public async Task<IActionResult> CreateRetro([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "sprintRetro")] HttpRequest req, ILogger log)
        {
            log.LogInformation("Creating a new sprint retro item");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var retroEntity = JsonConvert.DeserializeObject<RetroEntity>(requestBody);

            var message = retroEntity.message;
            var decryptMessage = HelperUtil.DecryptStringAES(message, keyString);
            retroEntity.message = decryptMessage;

            var headerData = retroEntity.headerData;
            var decryptHeaderData = HelperUtil.DecryptHeaderDataPk(headerData);
            retroEntity.headerData = decryptHeaderData;

            TableOperation insertOperation = TableOperation.Insert(retroEntity.ToRetroTableEntity());
            var result = await table.ExecuteAsync(insertOperation);

            var status = result.HttpStatusCode;
            if (status > 200 && status < 300)
            {
                return new OkObjectResult(retroEntity);
            }

            return new ConflictResult();
        }

        [FunctionName("GetRetroItemsById")]
        public async Task<IActionResult> GetRetroItemsById([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sprintRetro/{pk}")] HttpRequest req, ILogger log, string pk)
        {
            log.LogInformation("Getting retro item by header data");

            var decryptPk = HelperUtil.DecryptHeaderDataPk(pk);

            if (decryptPk.Equals("keyError"))
            {
                log.LogInformation($"keyError");
                return new ConflictResult();
            }

            var condition = TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, decryptPk);
            var query = new TableQuery<RetroTableEntity>().Where(condition);

            var tableResults = await table.ExecuteQuerySegmentedAsync<RetroTableEntity>(query, null);

            if (tableResults.Results == null || tableResults.Results.Count == 0)
            {
                log.LogInformation($"Item {pk} not found");
                return new NotFoundResult();
            }

            var retroEntities = new List<RetroEntity>();
            tableResults.Results.ForEach(result => retroEntities.Add(result.ToRetroEntity()));

            foreach (var retro in retroEntities)
            {
                retro.message = HelperUtil.EncryptStringAES(retro.message, keyString);
                retro.headerData = HelperUtil.EncryptHeaderDataPk(retro.headerData);
            }

            return new OkObjectResult(retroEntities);
        }


        [FunctionName("UpdateRetro")]
        public static async Task<IActionResult> UpdateRetro([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "sprintRetro")] HttpRequest req, ILogger log)
        {
            log.LogInformation("updating retro item by header data");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var retroEntity = JsonConvert.DeserializeObject<RetroEntity>(requestBody);

            var message = retroEntity.message;
            var decryptMessage = HelperUtil.DecryptStringAES(message, keyString);
            retroEntity.message = decryptMessage;

            var headerData = retroEntity.headerData;
            var decryptHeaderData = HelperUtil.DecryptHeaderDataPk(headerData);
            retroEntity.headerData = decryptHeaderData;

            TableOperation insertOperation = TableOperation.InsertOrReplace(retroEntity.ToRetroTableEntity());
            var result = await table.ExecuteAsync(insertOperation);

            var status = result.HttpStatusCode;
            if (status > 200 && status < 300)
            {
                return new OkObjectResult(retroEntity);
            }

            log.LogInformation($"Item not found");
            return new ConflictResult();
        }

        [FunctionName("DeleteRetro")]
        public async Task<IActionResult> DeleteRetro([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "sprintRetro/{pk}/{key}")] HttpRequest req,
            ILogger log, string pk, string key)
        {
            log.LogInformation("deleting retro item by header data");

            var decryptPk = HelperUtil.DecryptHeaderDataPk(pk);

            RetroEntity retroEntity = new RetroEntity
            {
                id = HttpUtility.HtmlDecode(key),
                headerData = decryptPk
            };

            var retroTableEntity = retroEntity.ToRetroTableEntity();
            retroTableEntity.ETag = "*";

            TableOperation deleteOperation = TableOperation.Delete(retroTableEntity);
            var result = await table.ExecuteAsync(deleteOperation);

            var status = result.HttpStatusCode;
            if (status > 200 && status < 300)
            {
                return new OkResult();
            }

            log.LogInformation($"Item {decryptPk} not found");
            return new NotFoundResult();
        }
    }
}
