using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventBus.Extensions
{
    public class AzureServiceBusOptions
    {
        public string SubscriptionClientName { get; set; }
        public string EventBusConnection { get; set; }
    }

    public class RabbitMQServiceBusOptions
    {
        public string SubscriptionClientName { get; set; }
        public string EventBusRetryCount { get; set; }
        public string EventBusConnection { get; set; }
        public string EventBusUserName { get; set; }
        public string EventBusPassword { get; set; }
    }
}
