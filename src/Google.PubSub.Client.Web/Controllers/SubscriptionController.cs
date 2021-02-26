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
        public async Task<ActionResult> Create(string projectId, string topicName, string subscriptionName,
            CancellationToken token)
        {
            var client = await new SubscriberServiceApiClientBuilder
            {
                Endpoint = _pubSubConfig.EmulatorHost,
                ChannelCredentials = ChannelCredentials.Insecure
            }.BuildAsync(token);

            var name = SubscriptionName.FromProjectSubscription(projectId, subscriptionName);
            var topic = TopicName.Parse(topicName);

            await client.CreateSubscriptionAsync(name, topic, new PushConfig(), 10, token);

            return RedirectToAction("Detail", "Topic", new {projectId, topicName});
        }

        [HttpPost("project/subscription/delete")]
        public async Task<ActionResult> Delete(string projectId, string topicName, string subscriptionName,
            CancellationToken token)
        {
            var client = await new SubscriberServiceApiClientBuilder
            {
                Endpoint = _pubSubConfig.EmulatorHost,
                ChannelCredentials = ChannelCredentials.Insecure
            }.BuildAsync(token);

            await client.DeleteSubscriptionAsync(subscriptionName, token);

            return RedirectToAction("Detail", "Topic", new {projectId, topicName});
        }

        [HttpGet("project/subscription/messages")]
        public async Task<ActionResult> ViewMessages(string projectId, string topicName, string subscriptionName,
            CancellationToken token)
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
            }.BuildAsync(token);

            var cts = CancellationTokenSource.CreateLinkedTokenSource(token);

            try
            {
                var pullResponse = await client.PullAsync(subscriptionName, false, int.MaxValue, cts.Token);
                model.Messages = pullResponse.ReceivedMessages.Select(m => new ViewMessageModel.Message
                {
                    MessageId = m.Message.MessageId,
                    AckId = m.AckId,
                    Data = m.Message.Data.ToStringUtf8()
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
            string ackId, CancellationToken token)
        {
            var client = await new SubscriberServiceApiClientBuilder
            {
                Endpoint = _pubSubConfig.EmulatorHost,
                ChannelCredentials = ChannelCredentials.Insecure
            }.BuildAsync(token);

            await client.AcknowledgeAsync(subscriptionName, new[] {ackId}, token);

            return RedirectToAction("ViewMessages", new {projectId, topicName, subscriptionName});
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