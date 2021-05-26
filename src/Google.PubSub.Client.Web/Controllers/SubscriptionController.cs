using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.PubSub.V1;
using Google.PubSub.Client.Web.Configs;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Google.PubSub.Client.Web.Controllers
{
    public class SubscriptionController : Controller
    {
        private readonly ILogger<SubscriptionController> _logger;
        private readonly PubSubConfig _pubSubConfig;

        public SubscriptionController(
            ILogger<SubscriptionController> logger,
            PubSubConfig pubSubConfig)
        {
            _logger = logger;
            _pubSubConfig = pubSubConfig;
        }

        [HttpGet("project/subscription/create")]
        public ActionResult Create(string projectId, string topicName)
        {
            var model = new SubscriptionCreateModel
            {
                ProjectId = projectId,
                TopicName = topicName
            };

            return View(model);
        }

        [HttpPost("project/subscription/create")]
        public async Task<ActionResult> Create(string projectId, string topicName, string subscriptionId,
            CancellationToken cancellationToken)
        {
            var client = await new SubscriberServiceApiClientBuilder
            {
                Endpoint = _pubSubConfig.EmulatorHost,
                ChannelCredentials = ChannelCredentials.Insecure
            }.BuildAsync(cancellationToken);

            await CreateSubscriptionAsync(client, subscriptionId, topicName, projectId, cancellationToken);

            return RedirectToAction("Detail", "Topic", new {projectId, topicName});
        }

        [HttpPost("project/subscription/delete")]
        public async Task<ActionResult> Delete(string projectId, string topicName, string subscriptionName,
            CancellationToken cancellationToken)
        {
            var client = await new SubscriberServiceApiClientBuilder
            {
                Endpoint = _pubSubConfig.EmulatorHost,
                ChannelCredentials = ChannelCredentials.Insecure
            }.BuildAsync(cancellationToken);

            await DeleteSubscriptionAsync(client, subscriptionName, cancellationToken);

            return RedirectToAction("Detail", "Topic", new {projectId, topicName});
        }

        [HttpGet("project/subscription/messages")]
        public async Task<ActionResult> ViewMessages(string projectId, string topicName, string subscriptionName,
            CancellationToken cancellationToken)
        {
            var model = new ViewMessageModel
            {
                ProjectId = projectId,
                TopicName = topicName,
                SubscriptionName = subscriptionName,
            };
            var client = await new SubscriberServiceApiClientBuilder
            {
                Endpoint = _pubSubConfig.EmulatorHost,
                ChannelCredentials = ChannelCredentials.Insecure
            }.BuildAsync(cancellationToken);

            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                var pullResponse = await client.PullAsync(subscriptionName, false, int.MaxValue, cts.Token);
                model.Messages = pullResponse.ReceivedMessages.Select(m =>
                {
                    var dataStr = m.Message.Data.ToStringUtf8();
                    try
                    {
                        dataStr = JObject.Parse(dataStr).ToString(Formatting.Indented);
                    }
                    catch
                    {
                        // ignored
                    }

                    return new ViewMessageModel.Message
                    {
                        MessageId = m.Message.MessageId,
                        AckId = m.AckId,
                        Data = dataStr,
                    };
                }).ToList();
            }
            catch (Exception)
            {
                // ignored
            }

            return View(model);
        }

        [HttpPost("project/subscription/message/ack")]
        public async Task<ActionResult> AcknowledgeMessage(string projectId, string topicName, string subscriptionName,
            string ackId, CancellationToken cancellationToken)
        {
            var client = await new SubscriberServiceApiClientBuilder
            {
                Endpoint = _pubSubConfig.EmulatorHost,
                ChannelCredentials = ChannelCredentials.Insecure
            }.BuildAsync(cancellationToken);

            await client.AcknowledgeAsync(subscriptionName, new[] {ackId}, cancellationToken);

            return RedirectToAction("ViewMessages", new {projectId, topicName, subscriptionName});
        }

        [HttpPost("project/subscription/messages/purge")]
        public async Task<ActionResult> PurgeMessages(string projectId, string topicName, string subscriptionName,
            CancellationToken cancellationToken)
        {
            var client = await new SubscriberServiceApiClientBuilder
            {
                Endpoint = _pubSubConfig.EmulatorHost,
                ChannelCredentials = ChannelCredentials.Insecure
            }.BuildAsync(cancellationToken);
            var subscriptionId = SubscriptionName.Parse(subscriptionName).SubscriptionId;

            await DeleteSubscriptionAsync(client, subscriptionName, cancellationToken);
            await CreateSubscriptionAsync(
                client,
                subscriptionId,
                topicName,
                projectId,
                cancellationToken);

            return RedirectToAction("Detail", "Topic", new {projectId, topicName});
        }

        private static async Task<Subscription> CreateSubscriptionAsync(SubscriberServiceApiClient client,
            string subscriptionId,
            string topicName,
            string projectId,
            CancellationToken cancellationToken)
        {
            var name = SubscriptionName.FromProjectSubscription(projectId, subscriptionId);
            var topic = TopicName.Parse(topicName);
            var subscription =
                await client.CreateSubscriptionAsync(name, topic, new PushConfig(), 10, cancellationToken);

            return subscription;
        }

        private static async Task DeleteSubscriptionAsync(SubscriberServiceApiClient client, string subscriptionName,
            CancellationToken cancellationToken)
        {
            await client.DeleteSubscriptionAsync(subscriptionName, cancellationToken);
        }
    }

    public class ViewMessageModel
    {
        public string ProjectId { get; set; }

        public string TopicName { get; set; }

        public string SubscriptionName { get; set; }

        public IReadOnlyCollection<Message> Messages { get; set; } = Array.Empty<Message>();

        public class Message
        {
            public string MessageId { get; set; }

            public string AckId { get; set; }

            public string Data { get; set; }
        }
    }

    public class SubscriptionCreateModel
    {
        public string ProjectId { get; set; }

        public string TopicName { get; set; }
    }
}