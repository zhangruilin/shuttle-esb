using System;
using System.Threading;
using NUnit.Framework;
using Shuttle.ESB.Core;

namespace Shuttle.ESB.Test.Integration.Core
{
	public class PipelineExceptionHandlingTest : IntegrationFixture
	{
		[Test]
		public void
			Should_be_able_to_roll_back_any_database_and_queue_changes_when_an_exception_occurs_in_the_receive_pipeline()
		{
			var configuration = DefaultConfiguration(true);

			var inboxWorkQueue = configuration.QueueManager.GetQueue("msmq://./test-inbox-work");
			var inboxErrorQueue = configuration.QueueManager.GetQueue("msmq://./test-error");

			configuration.Inbox =
				new InboxQueueConfiguration
					{
						WorkQueue = inboxWorkQueue,
						ErrorQueue = inboxErrorQueue,
						DurationToSleepWhenIdle = new[] {TimeSpan.FromMilliseconds(5)},
						DurationToIgnoreOnFailure = new[] {TimeSpan.FromMilliseconds(5)},
						MaximumFailureCount = 100,
						ThreadCount = 1
					};


			inboxWorkQueue.Drop();
			inboxErrorQueue.Drop();

			configuration.QueueManager.CreatePhysicalQueues(configuration);

			var module = new ReceivePipelineExceptionModule(inboxWorkQueue);

			configuration.Modules.Add(module);

			using (var bus = new ServiceBus(configuration))
			{
				var message = bus.CreateTransportMessage(new ReceivePipelineCommand());

				inboxWorkQueue.Enqueue(message.MessageId, configuration.Serializer.Serialize(message));

				Assert.IsFalse(inboxWorkQueue.IsEmpty());

				bus.Start();

				while (module.ShouldWait())
				{
					Thread.Sleep(10);
				}
			}
		}
	}
}