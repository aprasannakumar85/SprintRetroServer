using System;
using Microsoft.Azure.Cosmos.Table;

namespace SprintRetroServer
{
    public class RetroEntity
    {
        public DateTimeOffset createdDate { get; set; } = DateTime.Now;

        public string id { get; set; } = Guid.NewGuid().ToString("n");

        public string headerData { get; set; }  // combine employer, team and sprintNumber

        public string message { get; set; }

        public MessageType messageType { get; set; }
    }

    public class RetroTableEntity : TableEntity
    {
        public string message { get; set; }

        public string messageType { get; set; }
    }

    public static class Mappings
    {
        public static RetroTableEntity ToRetroTableEntity(this RetroEntity retroEntity)
        {
            var messageTypeInput = ParseEnum<MessageType>(retroEntity.messageType.ToString());

            return new RetroTableEntity()
            {
                PartitionKey = retroEntity.headerData,
                RowKey = retroEntity.id,
                Timestamp = retroEntity.createdDate,
                message = retroEntity.message,
                messageType = messageTypeInput.ToString(),
            };
        }
        
        public static RetroEntity ToRetroEntity(this RetroTableEntity retroTableEntity)
        {
            var messageTypeInput = ParseEnum<MessageType>(retroTableEntity.messageType.ToString());

            return new RetroEntity()
            {
                id = retroTableEntity.RowKey,
                createdDate = retroTableEntity.Timestamp,
                message = retroTableEntity.message,
                messageType = messageTypeInput,
                headerData = retroTableEntity.PartitionKey
            };
        }

        public static T ParseEnum<T>(string value)
        {
            return (T)Enum.Parse(typeof(T), value, true);
        }
    }

    public enum MessageType
    {
        WentWell = 0,
        Improve = 1,
        Kudos = 2,
        Action = 3
    }
}
